using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Root game state DTO containing overall game information.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class GameStateDto
{
    /// <summary>
    ///     Current screen/phase (COMBAT, MENU, MAP, SHOP, etc.)
    /// </summary>
    public required string Screen { get; set; }

    /// <summary>
    ///     Unix timestamp of state extraction.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    ///     Error message if state extraction failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Combat state if in combat, null otherwise.
    /// </summary>
    public CombatStateDto? Combat { get; set; }

    /// <summary>
    ///     Reward state if on reward screen, null otherwise.
    /// </summary>
    public RewardStateDto? Rewards { get; set; }

    /// <summary>
    ///     Event state if at an event room, null otherwise.
    /// </summary>
    public EventStateDto? Event { get; set; }

    /// <summary>
    ///     Tri-select (choose-a-card) state if a <c>NChooseACardSelectionScreen</c> is open, null otherwise.
    ///     Triggered by potions, cards (Discovery, Quasar, Splash), relics, or monsters.
    /// </summary>
    public TriSelectStateDto? TriSelect { get; set; }

    /// <summary>
    ///     Character selection state if on character select screen, null otherwise.
    /// </summary>
    public CharacterSelectStateDto? CharacterSelect { get; set; }

    /// <summary>
    ///     Grid card selection state if on a grid-based card selection screen
    ///     (remove, upgrade, transform, enchant, combat grid overlays), null otherwise.
    /// </summary>
    public GridCardSelectStateDto? GridCardSelect { get; set; }

    /// <summary>
    ///     Map state if on the map screen, null otherwise.
    /// </summary>
    public MapStateDto? Map { get; set; }

    /// <summary>
    ///     Rest site (campfire) state if at a rest site, null otherwise.
    /// </summary>
    public RestSiteStateDto? RestSite { get; set; }

    /// <summary>
    ///     Treasure room state if at a treasure room, null otherwise.
    /// </summary>
    public TreasureStateDto? Treasure { get; set; }

    /// <summary>
    ///     Shop (merchant room) state if at a shop, null otherwise.
    /// </summary>
    public ShopStateDto? Shop { get; set; }

    /// <summary>
    ///     Hand card selection state if the player is selecting cards from their hand
    ///     (e.g., discard, exhaust, upgrade prompts during combat), null otherwise.
    /// </summary>
    public HandSelectStateDto? HandSelect { get; set; }

    /// <summary>
    ///     Relic selection state if a "choose a relic" overlay is open
    ///     (e.g., after boss fights or certain events), null otherwise.
    /// </summary>
    public RelicSelectStateDto? RelicSelect { get; set; }

    /// <summary>
    ///     Bundle selection state if a "choose a bundle" overlay is open
    ///     (triggered by the Scroll Boxes relic), null otherwise.
    /// </summary>
    public BundleSelectStateDto? BundleSelect { get; set; }

    /// <summary>
    ///     Crystal Sphere mini-game state if the Crystal Sphere overlay is open, null otherwise.
    ///     The player uses divination tools to clear fog and reveal hidden items on a grid.
    /// </summary>
    public CrystalSphereStateDto? CrystalSphere { get; set; }

    /// <summary>
    ///     Game over screen state if the run has ended (victory or defeat), null otherwise.
    /// </summary>
    public GameOverStateDto? GameOver { get; set; }

    /// <summary>
    ///     Main menu state if on the main menu screen, null otherwise.
    ///     Contains information about saved run availability.
    /// </summary>
    public MenuStateDto? Menu { get; set; }

    /// <summary>
    ///     Singleplayer submenu state if the singleplayer submenu is open, null otherwise.
    ///     Contains information about available game modes (standard, daily, custom).
    /// </summary>
    public SingleplayerSubmenuStateDto? SingleplayerSubmenu { get; set; }
}