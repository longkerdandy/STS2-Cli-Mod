using System.Collections.Concurrent;
using Godot;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Executes actions on the Godot main thread.
///     Required because Godot engine operations must run on the main thread.
/// </summary>
public static class MainThreadExecutor
{
    private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    private static readonly ModLogger Logger = new("MainThreadExecutor");
    private static bool _initialized;

    /// <summary>
    ///     Initializes the main thread executor by connecting to ProcessFrame signal.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(ProcessMainThreadQueue));
            _initialized = true;
            Logger.Info("Main thread executor initialized");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize main thread executor: {ex.Message}");
        }
    }

    /// <summary>
    ///     Processes queued actions on the main thread (called every frame).
    /// </summary>
    private static void ProcessMainThreadQueue()
    {
        int processed = 0;
        while (_mainThreadQueue.TryDequeue(out var action) && processed < 10)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error($"Main thread action error: {ex.Message}");
            }
            processed++;
        }
    }

    /// <summary>
    ///     Enqueues an action to be executed on the main thread.
    /// </summary>
    public static void Enqueue(Action action)
    {
        if (!_initialized)
        {
            Logger.Warning("Main thread executor not initialized, initializing now...");
            Initialize();
        }
        _mainThreadQueue.Enqueue(action);
    }

    /// <summary>
    ///     Executes a function on the main thread and returns the result.
    /// </summary>
    public static T RunOnMainThread<T>(Func<T> func)
    {
        if (Thread.CurrentThread.Name == "Main Thread" || Engine.GetMainLoop() is SceneTree)
        {
            // Already on main thread, execute directly
            return func();
        }

        var tcs = new TaskCompletionSource<T>();
        Enqueue(() =>
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
}
