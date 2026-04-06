using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for accessing event-related game data.
///     Centralizes reflection access to private fields on <see cref="NEventRoom" />
///     so that action handlers and state builders share a single cached <see cref="FieldInfo" />.
/// </summary>
public static class EventUtils
{
    /// <summary>
    ///     Cached reflection field for <see cref="NEventRoom" />._event (private).
    /// </summary>
    private static readonly FieldInfo? EventField =
        typeof(NEventRoom).GetField("_event", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Gets the <see cref="EventModel" /> from an <see cref="NEventRoom" /> instance
    ///     using cached reflection on the private <c>_event</c> field.
    /// </summary>
    /// <param name="eventRoom">The event room to extract the model from.</param>
    /// <returns>The event model, or <c>null</c> if the field is missing or the value is not an <see cref="EventModel" />.</returns>
    public static EventModel? GetEventModel(NEventRoom eventRoom)
    {
        return EventField?.GetValue(eventRoom) as EventModel;
    }
}