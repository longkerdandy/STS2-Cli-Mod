using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Bundle selection screen state DTO. Represents the "choose a bundle" overlay
///     that appears when the player obtains the Scroll Boxes relic.
///     Each bundle contains multiple cards; the player previews and picks one bundle.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class BundleSelectStateDto
{
    /// <summary>
    ///     List of available bundles, each containing a group of cards.
    /// </summary>
    public List<BundleDto> Bundles { get; set; } = [];

    /// <summary>
    ///     Whether a bundle preview is currently showing (a bundle has been clicked).
    ///     When true, the player must confirm or cancel before selecting a different bundle.
    /// </summary>
    public bool PreviewShowing { get; set; }

    /// <summary>
    ///     Cards shown in the preview panel when <see cref="PreviewShowing" /> is true.
    ///     Empty when no preview is active.
    /// </summary>
    public List<SelectableCardDto> PreviewCards { get; set; } = [];

    /// <summary>
    ///     Whether the confirm button is currently enabled (requires a bundle to be previewed).
    /// </summary>
    public bool CanConfirm { get; set; }

    /// <summary>
    ///     Whether the cancel button is currently enabled (to go back to bundle selection).
    /// </summary>
    public bool CanCancel { get; set; }
}

/// <summary>
///     A single bundle in the bundle selection screen.
///     Contains multiple cards that will all be added to the deck if selected.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class BundleDto
{
    /// <summary>
    ///     0-based index of the bundle in the selection screen.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Number of cards in this bundle.
    /// </summary>
    public int CardCount { get; set; }

    /// <summary>
    ///     Cards contained in this bundle.
    /// </summary>
    public List<SelectableCardDto> Cards { get; set; } = [];
}