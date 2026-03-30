using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace STS2.Cli.Cmd.Models.Messages;

/// <summary>
///     Request model for pipe communication.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class Request
{
    /// <summary>
    ///     The command to execute (e.g., "ping", "state", "play_card").
    /// </summary>
    [JsonPropertyName("cmd")]
    public required string Cmd { get; set; }

    /// <summary>
    ///     Optional array of integer arguments for the command (e.g., reward_index for claim_reward).
    /// </summary>
    [JsonPropertyName("args")]
    public int[]? Args { get; set; }

    /// <summary>
    ///     Optional target ID for targeted commands.
    ///     For targeted cards/potions: enemy combat_id.
    /// </summary>
    [JsonPropertyName("target")]
    public int? Target { get; set; }

    /// <summary>
    ///     Item ID for commands that reference game objects by ID.
    ///     - play_card: Card ID (e.g., "STRIKE_IRONCLAD")
    ///     - use_potion: Potion ID (e.g., "FIRE_POTION")
    ///     Use with nth to disambiguate when multiple copies exist.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    ///     N-th occurrence (0-based) when multiple items with the same ID exist.
    ///     Optional, defaults to 0 if not specified.
    /// </summary>
    [JsonPropertyName("nth")]
    public int? Nth { get; set; }

    /// <summary>
    ///     Reward type for reward commands (e.g., "gold", "potion", "relic", "card", "special_card").
    /// </summary>
    [JsonPropertyName("reward_type")]
    public string? RewardType { get; set; }

    /// <summary>
    ///     Card ID for choose_card command (the card to select from the card reward).
    /// </summary>
    [JsonPropertyName("card_id")]
    public string? CardId { get; set; }

    /// <summary>
    ///     Card IDs for tri_select_card, grid_select_card, and hand_select_card commands (multiple cards to select).
    /// </summary>
    [JsonPropertyName("card_ids")]
    public string[]? CardIds { get; set; }

    /// <summary>
    ///     N-th values for each card in CardIds (for tri_select_card, grid_select_card, hand_select_card).
    /// </summary>
    [JsonPropertyName("nth_values")]
    public int[]? NthValues { get; set; }

    /// <summary>
    ///     The skip flag for commands that support skipping.
    /// </summary>
    [JsonPropertyName("skip")]
    public bool? Skip { get; set; }
}