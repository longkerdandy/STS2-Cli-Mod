namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Standardized response structure for all action handlers.
///     Provides a consistent format for success and error responses.
/// </summary>
public class ActionResponse
{
    /// <summary>
    ///     Indicates whether the action was successful.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    ///     Error code for failed actions (e.g., "NOT_IN_COMBAT", "ITEM_NOT_FOUND").
    ///     Null when Ok is true.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Human-readable error message.
    ///     Null when Ok is true.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Response data payload. Structure varies by action type.
    ///     Null when Ok is false.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    ///     Current game screen after action completion (e.g., "COMBAT", "MAP", "SHOP").
    ///     Helps clients track state transitions.
    /// </summary>
    public string? Screen { get; set; }

    /// <summary>
    ///     Creates a success response.
    /// </summary>
    public static ActionResponse Success(object? data = null, string? screen = null)
    {
        return new ActionResponse
        {
            Ok = true,
            Data = data,
            Screen = screen
        };
    }

    /// <summary>
    ///     Creates an error response.
    /// </summary>
    public static ActionResponse Failure(string errorCode, string message, object? details = null)
    {
        return new ActionResponse
        {
            Ok = false,
            Error = errorCode,
            Message = message,
            Data = details
        };
    }

    /// <summary>
    ///     Creates a validation error response for missing arguments.
    /// </summary>
    public static ActionResponse MissingArgument(string parameterName)
    {
        return Failure("MISSING_ARGUMENT", $"{parameterName} is required");
    }

    /// <summary>
    ///     Creates an error response for not being in the expected location/screen.
    /// </summary>
    public static ActionResponse NotInLocation(string location)
    {
        return Failure($"NOT_IN_{location.ToUpperInvariant()}", $"Not in {location}");
    }

    /// <summary>
    ///     Creates an error response for item not found.
    /// </summary>
    public static ActionResponse NotFound(string itemType, string itemId)
    {
        return Failure("NOT_FOUND", $"{itemType} '{itemId}' not found");
    }

    /// <summary>
    ///     Creates an error response for invalid operation.
    /// </summary>
    public static ActionResponse InvalidOperation(string reason)
    {
        return Failure("INVALID_OPERATION", reason);
    }

    /// <summary>
    ///     Creates an internal error response.
    /// </summary>
    public static ActionResponse InternalError(string message)
    {
        return Failure("INTERNAL_ERROR", message);
    }
}
