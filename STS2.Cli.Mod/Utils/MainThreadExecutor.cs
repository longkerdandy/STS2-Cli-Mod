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
        
        if (processed > 0)
        {
            Logger.Info($"Processed {processed} actions from main thread queue");
        }
    }

    /// <summary>
    ///     Executes a function on the main thread and returns the result.
    ///     Always defers to next frame to ensure Godot scene tree safety.
    /// </summary>
    public static T RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        
        Logger.Info($"Enqueuing action for main thread execution (queue size: {_mainThreadQueue.Count + 1})");
        
        _mainThreadQueue.Enqueue(() =>
        {
            try
            {
                Logger.Info("Executing action on main thread");
                var result = func();
                Logger.Info("Action completed successfully");
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                Logger.Error($"Action failed: {ex.Message}");
                tcs.SetException(ex);
            }
        });
        
        return tcs.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Executes an action on the main thread.
    ///     Always defers to next frame to ensure Godot scene tree safety.
    /// </summary>
    public static void RunOnMainThread(Action action)
    {
        var tcs = new TaskCompletionSource<object?>();
        
        _mainThreadQueue.Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        
        tcs.Task.GetAwaiter().GetResult();
    }
}
