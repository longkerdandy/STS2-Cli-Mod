using System.Text.Json.Serialization;

namespace STS2.Cli.Cmd.Models.Message;

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
    /// Optional array of integer arguments for the command (e.g., reward_index for claim_reward).
    /// </summary>
    [JsonPropertyName("args")]
    public int[]? Args { get; set; }

    /// <summary>
    /// Optional target ID for targeted commands.
    /// For targeted cards/potions: enemy combat_id.
    /// </summary>
    [JsonPropertyName("target")]
    public int? Target { get; set; }

    /// <summary>
    /// Item ID for commands that reference game objects by ID.
    /// - play_card: Card ID (e.g., "STRIKE_IRONCLAD")
    /// - use_potion: Potion ID (e.g., "FIRE_POTION")
    /// Use with nth to disambiguate when multiple copies exist.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// N-th occurrence (0-based) when multiple items with same ID exist.
    /// Optional, defaults to 0 if not specified.
    /// </summary>
    [JsonPropertyName("nth")]
    public int? Nth { get; set; }
}
