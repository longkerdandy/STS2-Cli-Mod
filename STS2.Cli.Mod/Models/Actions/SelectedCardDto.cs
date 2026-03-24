namespace STS2.Cli.Mod.Models.Actions;

/// <summary>
///     DTO representing a card that has been selected from a potion selection screen.
/// </summary>
public class SelectedCardDto
{
    /// <summary>
    ///     Selection index (0-based order in which the card was selected).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card model ID (e.g., "STRIKE_IRONCLAD").
    /// </summary>
    public required string CardId { get; set; }
}
