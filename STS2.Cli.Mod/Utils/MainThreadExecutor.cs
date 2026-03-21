using System.Collections.Concurrent;
using Godot;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Marshals work from the pipe-server background thread to the Godot main thread.
///     Each frame, all queued delegates are drained and executed synchronously.
///     The caller (<see cref="RunOnMainThread{T}"/>) blocks until its delegate completes.
/// </summary>
public static class MainThreadExecutor
{
    private static readonly ConcurrentQueue<Action> PendingActions = new();
    private static readonly ModLogger Logger = new("MainThreadExecutor");
    private static bool _initialized;

    /// <summary>
    ///     Connects to <see cref="SceneTree.SignalName.ProcessFrame"/> so queued work
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
    ///     Executes <paramref name="func"/> on the Godot main thread and returns the result.
    ///     The calling thread (pipe-server) blocks until the next <c>ProcessFrame</c> drains
    ///     the queue and the delegate completes.
    /// </summary>
    /// <remarks>
    ///     This is the only public entry point for marshalling work. Game state reads
    ///     (<c>state</c>) and game actions (<c>play_card</c>, <c>end_turn</c>) all go
    ///     through this method so they execute on the main thread where Godot and the
    ///     game's <c>ActionQueueSynchronizer</c> expect to be called.
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
    ///     Called every frame by <c>SceneTree.ProcessFrame</c>.
    ///     Drains all queued delegates synchronously on the main thread.
    /// </summary>
    private static void DrainQueue()
    {
        while (PendingActions.TryDequeue(out var action))
        {
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
}
