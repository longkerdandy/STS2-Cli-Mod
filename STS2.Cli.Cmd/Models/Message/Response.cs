using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace STS2.Cli.Cmd.Models.Message;

/// <summary>
///     Response model from the mod.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class Response
{
    /// <summary>
    ///     Indicates whether the command was executed successfully.
    ///     true = success, false = error occurred.
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    /// <summary>
    ///     Response data payload. Structure varies by command:
    ///     - ping: { connected: true }
    ///     - state: { screen, player, enemies, hand, etc. }
    ///     - play_card: { damage, block, powers, etc. }
    ///     - end_turn: { enemy_damage, block, etc. }
    ///     Null when Ok is false.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>
    ///     Error code when Ok is false.
    ///     Common codes: NOT_IN_COMBAT, TARGET_REQUIRED, INVALID_CARD, INVALID_INDEX, etc.
    ///     Null when Ok is true.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    ///     Human-readable error message providing additional context.
    ///     Null when Ok is true.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}