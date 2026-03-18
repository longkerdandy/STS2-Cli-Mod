using Godot;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     A Godot Node that provides CallDeferred functionality for C# code.
///     This allows safe deferred execution on the main thread.
/// </summary>
public partial class DeferredExecutor : Node
{
    private static DeferredExecutor? _instance;
    private static readonly ModLogger Logger = new("DeferredExecutor");

    public static DeferredExecutor Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new DeferredExecutor();
                // Add to scene tree on the main thread
                var tree = (SceneTree)Engine.GetMainLoop();
                tree.Root.CallDeferred(Node.MethodName.AddChild, _instance);
                Logger.Info("DeferredExecutor instance created");
            }
            return _instance;
        }
    }

    /// <summary>
    ///     Executes an action on the main thread at the end of the current frame.
    ///     This is safe for Godot node operations.
    /// </summary>
    public void ExecuteDeferred(Action action)
    {
        CallDeferred(nameof(ExecuteInternal), Callable.From(action));
    }

    private void ExecuteInternal(Callable callable)
    {
        try
        {
            callable.Call();
        }
        catch (Exception ex)
        {
            Logger.Error($"Deferred execution error: {ex.Message}");
        }
    }
}
