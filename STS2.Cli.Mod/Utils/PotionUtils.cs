using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Actions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for potion-related operations and card selection handling.
/// </summary>
public static class PotionUtils
{
    private static readonly ModLogger Logger = new("PotionUtils");

    /// <summary>
    ///     Potions that open card selection screens when used.
    /// </summary>
    private static readonly HashSet<string> CardSelectionPotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ATTACK_POTION",      // Choose 1 of 3 attacks
        "SKILL_POTION",       // Choose 1 of 3 skills
        "POWER_POTION",       // Choose 1 of 3 powers
        "COLORLESS_POTION",   // Choose 1 of 3 colorless (can skip)
        "LIQUID_MEMORIES",    // Choose 1 from discard pile
        "DROPLET_OF_PRECOGNITION", // Choose 1 from draw pile
        "GAMBLERS_BREW",      // Choose 0-N from hand (discard and draw)
        "ASHWATER",           // Choose 1-N from hand (exhaust)
        "TOUCH_OF_INSANITY"   // Choose 1 from hand (make free)
    };

    /// <summary>
    ///     Checks if a potion requires card selection when used.
    /// </summary>
    /// <param name="potionId">The potion ID to check.</param>
    /// <returns>True if the potion opens a card selection screen.</returns>
    public static bool RequiresCardSelection(string potionId)
    {
        return CardSelectionPotions.Contains(potionId);
    }

    /// <summary>
    ///     Gets the selection type category for a potion.
    /// </summary>
    /// <param name="potionId">The potion ID.</param>
    /// <returns>Selection type string.</returns>
    public static string GetSelectionType(string potionId)
    {
        return potionId.ToUpperInvariant() switch
        {
            "ATTACK_POTION" or "SKILL_POTION" or "POWER_POTION" or "COLORLESS_POTION"
                => "choose_from_pool",
            "LIQUID_MEMORIES"
                => "choose_from_discard",
            "DROPLET_OF_PRECOGNITION"
                => "choose_from_draw",
            "GAMBLERS_BREW"
                => "choose_from_hand_multi",
            "ASHWATER"
                => "choose_from_hand_multi_exhaust",
            "TOUCH_OF_INSANITY"
                => "choose_from_hand_single",
            _ => "unknown"
        };
    }

    /// <summary>
    ///     Gets selection constraints (min/max select count, can skip) for a potion.
    /// </summary>
    /// <param name="potionId">The potion ID.</param>
    /// <returns>Selection constraints.</returns>
    public static SelectionConstraintsDto GetSelectionConstraints(string potionId)
    {
        return potionId.ToUpperInvariant() switch
        {
            // Single select potions
            "ATTACK_POTION" or "SKILL_POTION" or "POWER_POTION" or
            "LIQUID_MEMORIES" or "DROPLET_OF_PRECOGNITION" or "TOUCH_OF_INSANITY"
                => new SelectionConstraintsDto { MinSelect = 1, MaxSelect = 1, CanSkip = false },

            // Single select with skip option
            "COLORLESS_POTION"
                => new SelectionConstraintsDto { MinSelect = 0, MaxSelect = 1, CanSkip = true },

            // Multi-select potions
            "GAMBLERS_BREW"
                => new SelectionConstraintsDto { MinSelect = 0, MaxSelect = 10, CanSkip = true }, // Can select 0 (skip)

            "ASHWATER"
                => new SelectionConstraintsDto { MinSelect = 1, MaxSelect = 10, CanSkip = false }, // Must select at least 1

            // Default
            _ => new SelectionConstraintsDto { MinSelect = 1, MaxSelect = 1, CanSkip = false }
        };
    }

    /// <summary>
    ///     Finds the currently open card selection screen.
    /// </summary>
    /// <returns>The selection screen if found, null otherwise.</returns>
    public static NChooseACardSelectionScreen? FindSelectionScreen()
    {
        return UiUtils.FindScreenInOverlay<NChooseACardSelectionScreen>();
    }

    /// <summary>
    ///     Finds a card holder in the selection screen by card ID and nth occurrence.
    /// </summary>
    /// <param name="screen">The selection screen.</param>
    /// <param name="cardId">Card ID to find.</param>
    /// <param name="nth">N-th occurrence (0-based).</param>
    /// <returns>The card holder if found, null otherwise.</returns>
    public static NCardHolder? FindCardHolderById(NChooseACardSelectionScreen screen, string cardId, int nth)
    {
        var cardHolders = UiUtils.FindAll<NCardHolder>(screen);
        var matchingHolders = new List<NCardHolder>();

        foreach (var holder in cardHolders)
        {
            if (holder.CardModel?.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase) == true)
            {
                matchingHolders.Add(holder);
            }
        }

        if (nth < 0 || nth >= matchingHolders.Count)
        {
            Logger.Warning($"Card '{cardId}' has {matchingHolders.Count} copies, but nth={nth} was requested");
            return null;
        }

        return matchingHolders[nth];
    }

    /// <summary>
    ///     Finds the skip button on a selection screen.
    /// </summary>
    /// <param name="screen">The selection screen.</param>
    /// <returns>The skip button if found, null otherwise.</returns>
    public static NButton? FindSkipButton(NChooseACardSelectionScreen screen)
    {
        // Try to find by common node names/patterns
        var skipButton = screen.GetNodeOrNull<NButton>("%SkipButton");
        if (skipButton != null) return skipButton;

        // Search for any button with "skip" in its name
        foreach (var child in screen.GetChildren())
        {
            if (child is NButton button && 
                button.Name.ToString().Contains("Skip", StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }
}
