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

    /// <summary>
    /// Card ID for play_card command (e.g., "STRIKE_IRONCLAD").
    /// Use with nth to disambiguate when multiple copies exist.
    /// </summary>
    [JsonPropertyName("card_id")]
    public string? CardId { get; set; }

    /// <summary>
    /// Potion ID for use_potion command (e.g., "FIRE_POTION").
    /// Use with nth to disambiguate when multiple copies exist.
    /// </summary>
    [JsonPropertyName("potion_id")]
    public string? PotionId { get; set; }

    /// <summary>
    /// N-th occurrence (0-based) when multiple cards/potions with same ID exist.
    /// Optional, defaults to 0 if not specified.
    /// </summary>
    [JsonPropertyName("nth")]
    public int? Nth { get; set; }
}
