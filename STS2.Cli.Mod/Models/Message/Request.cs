using System.Text.Json.Serialization;

namespace STS2.Cli.Mod.Models.Message;

/// <summary>
/// Request model for pipe communication.
/// </summary>
public class Request
{
    /// <summary>
    /// The command to execute (e.g., "ping", "state", "play_card").
    /// </summary>
    [JsonPropertyName("cmd")]
    public required string Cmd { get; set; }

    /// <summary>
    /// Optional array of integer arguments for the command.
    /// </summary>
    [JsonPropertyName("args")]
    public int[]? Args { get; set; }

    /// <summary>
    /// Optional target combat ID for targeted commands (from EnemyStateDto.CombatId).
    /// </summary>
    [JsonPropertyName("target")]
    public int? Target { get; set; }
}
