using System.Collections.Concurrent;
using Godot;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Marshals work from the pipe-server background thread to the Godot main thread.
///     Each frame, all queued delegates are drained and executed on the main thread.
///     Provides both synchronous (<see cref="RunOnMainThread{T}" />) and asynchronous
///     (<see cref="RunOnMainThreadAsync{T}" />) entry points.
/// </summary>
public static class MainThreadExecutor
{
    private static readonly ConcurrentQueue<Action> PendingActions = new();
    private static readonly ModLogger Logger = new("MainThreadExecutor");
    private static bool _initialized;

    /// <summary>
    ///     Connects to <see cref="SceneTree.SignalName.ProcessFrame" /> so queued work
    ///     is drained every frame. Must be called once from the main thread at mod startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(DrainQueue));
            _initialized = true;
            Logger.Info("Main thread executor initialized");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize main thread executor: {ex.Message}");
        }
    }

    /// <summary>
    ///     Executes <paramref name="func" /> on the Godot main thread and returns the result.
    ///     The calling thread (pipe-server) blocks until the next <c>ProcessFrame</c> drains
    ///     the queue and the delegate completes.
    /// </summary>
    /// <remarks>
    ///     Use for immediate, single-frame work (e.g., reading game state).
    ///     For work that spans multiple frames (e.g., awaiting action completion), use
    ///     <see cref="RunOnMainThreadAsync{T}" /> instead.
    /// </remarks>
    public static T RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        PendingActions.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Starts <paramref name="asyncFunc" /> on the Godot main thread and returns a
    ///     <see cref="Task{T}" /> the caller can await from the pipe-server background thread.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="RunOnMainThread{T}" />, this method does <b>not</b> block the
    ///     main thread or the calling thread synchronously. The async function is kicked off
    ///     during the next <c>ProcessFrame</c> drain; its continuations naturally run on the
    ///     main thread (via Godot's <c>SynchronizationContext</c>) across subsequent frames.
    ///     The returned task completes when the entire async chain finishes.
    ///     Use for work that spans multiple frames (e.g., awaiting <c>GameAction</c> completion).
    /// </remarks>
    public static Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> asyncFunc)
    {
        var tcs = new TaskCompletionSource<T>();

        PendingActions.Enqueue(() =>
        {
            // Fire-and-forget on the main thread: kick off the async chain.
            // Continuations run on the main thread via Godot's SynchronizationContext.
            // When the chain completes, propagate the result (or exception) to the TCS,
            // which unblocks the pipe-server thread awaiting the returned Task<T>.
            asyncFunc().ContinueWith(task =>
            {
                if (task.IsFaulted)
                    tcs.SetException(task.Exception!.InnerExceptions);
                else if (task.IsCanceled)
                    tcs.SetCanceled();
                else
                    tcs.SetResult(task.Result);
            }, TaskScheduler.Default);
        });

        return tcs.Task;
    }

    /// <summary>
    ///     Called every frame by <c>SceneTree.ProcessFrame</c>.
    ///     Drains all queued delegates synchronously on the main thread.
    /// </summary>
    private static void DrainQueue()
    {
        while (PendingActions.TryDequeue(out var action))
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error($"Main thread action error: {ex.Message}");
            }
    }
}