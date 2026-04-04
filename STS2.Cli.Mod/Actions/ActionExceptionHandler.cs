using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Centralized exception handling for action handlers.
///     Provides consistent error logging and response generation.
/// </summary>
public static class ActionExceptionHandler
{
    private static readonly ModLogger Logger = new("ActionExceptionHandler");

    /// <summary>
    ///     Handles exceptions in action handlers, logs the error, and returns a standardized response.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="context">Description of what was being attempted (e.g., "buy card", "play card").</param>
    /// <param name="handlerName">Name of the handler class (auto-detected if not provided).</param>
    /// <returns>A standardized error response.</returns>
    public static ActionResponse Handle(Exception ex, string context, string? handlerName = null)
    {
        var source = handlerName ?? "Unknown";
        Logger.Error($"[{source}] Failed to {context}: {ex.Message}");
        
        return ActionResponse.InternalError($"Failed to {context}: {ex.Message}");
    }

    /// <summary>
    ///     Executes an async action with centralized exception handling.
    /// </summary>
    /// <typeparam name="T">Return type of the action.</typeparam>
    /// <param name="action">The async action to execute.</param>
    /// <param name="context">Description of what is being attempted.</param>
    /// <param name="handlerName">Name of the handler class.</param>
    /// <returns>The action result, or an error response if an exception occurs.</returns>
    public static async Task<object> ExecuteAsync<T>(Func<Task<T>> action, string context, string? handlerName = null)
    {
        try
        {
            var result = await action();
            return result!;
        }
        catch (Exception ex)
        {
            return Handle(ex, context, handlerName);
        }
    }

    /// <summary>
    ///     Executes a synchronous action with centralized exception handling.
    /// </summary>
    /// <typeparam name="T">Return type of the action.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="context">Description of what is being attempted.</param>
    /// <param name="handlerName">Name of the handler class.</param>
    /// <returns>The action result, or an error response if an exception occurs.</returns>
    public static object Execute<T>(Func<T> action, string context, string? handlerName = null)
    {
        try
        {
            var result = action();
            return result!;
        }
        catch (Exception ex)
        {
            return Handle(ex, context, handlerName);
        }
    }
}
