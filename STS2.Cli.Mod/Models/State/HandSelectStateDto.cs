using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Hand card selection state DTO.
///     Contains information about the current hand selection mode (e.g., discard, exhaust, upgrade).
///     This represents inline hand selection, NOT overlay-based deck/potion selection screens.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class HandSelectStateDto
{
    /// <summary>
    ///     Selection mode: "SimpleSelect" or "UpgradeSelect".
    /// </summary>
    public required string Mode { get; set; }

    /// <summary>
    ///     Prompt text shown to the player (e.g., "Choose 1 card to discard.").
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    ///     Minimum number of cards that must be selected.
    /// </summary>
    public int MinSelect { get; set; }

    /// <summary>
    ///     Maximum number of cards that can be selected.
    /// </summary>
    public int MaxSelect { get; set; }

    /// <summary>
    ///     Whether the selection can be cancelled.
    /// </summary>
    public bool Cancelable { get; set; }

    /// <summary>
    ///     Whether manual confirmation is required (confirm button must be clicked).
    ///     When false, selection auto-completes when MaxSelect cards are selected.
    /// </summary>
    public bool RequireManualConfirmation { get; set; }

    /// <summary>
    ///     Whether the confirm button is currently enabled (enough cards selected).
    /// </summary>
    public bool CanConfirm { get; set; }

    /// <summary>
    ///     Number of cards currently selected.
    /// </summary>
    public int SelectedCount { get; set; }

    /// <summary>
    ///     Cards available for selection in the hand (visible, pass the filter).
    /// </summary>
    public List<HandSelectCardDto> SelectableCards { get; set; } = [];

    /// <summary>
    ///     Cards already selected (moved to the selected container).
    /// </summary>
    public List<HandSelectCardDto> SelectedCards { get; set; } = [];
}

/// <summary>
///     Individual card in hand selection mode.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class HandSelectCardDto
{
    /// <summary>
    ///     Index of the card among the selectable/selected cards (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card model ID (e.g., "STRIKE_IRONCLAD").
    /// </summary>
    public required string CardId { get; set; }

    /// <summary>
    ///     Localized card name.
    /// </summary>
    public required string CardName { get; set; }

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse.
    /// </summary>
    public string? CardType { get; set; }

    /// <summary>
    ///     Energy cost to play the card.
    /// </summary>
    public int? Cost { get; set; }

    /// <summary>
    ///     Card description text.
    /// </summary>
    public string? Description { get; set; }
}
