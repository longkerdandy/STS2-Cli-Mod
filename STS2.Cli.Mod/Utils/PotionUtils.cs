using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Actions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for potion-related metadata queries and response building.
///     Provides potion card-selection classification (<see cref="RequiresCardSelection" />,
///     <see cref="GetSelectionType" />, <see cref="GetSelectionConstraints" />)
///     and a helper to build the selection response sent back to the CLI.
/// </summary>
public static class PotionUtils
{
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
    /// <returns>Selection type string describing the source of selectable cards.</returns>
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
    /// <returns>Selection constraints DTO.</returns>
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
    ///     Builds a response object for potions that opened a card selection screen.
    ///     Extracts selectable cards and selection constraints from the screen.
    /// </summary>
    /// <param name="potion">The potion model that triggered the selection.</param>
    /// <param name="slot">The potion belt slot index.</param>
    /// <param name="selectionScreen">The card selection screen to extract data from.</param>
    /// <returns>A success response with selection details.</returns>
    public static object BuildSelectionResponse(
        PotionModel potion, int slot, NChooseACardSelectionScreen selectionScreen)
    {
        var cards = UiUtils.ExtractSelectableCards(selectionScreen);
        var constraints = GetSelectionConstraints(potion.Id.Entry);

        return new
        {
            ok = true,
            data = new
            {
                status = "selection_required",
                selection_type = GetSelectionType(potion.Id.Entry),
                potion_id = potion.Id.Entry,
                potion_slot = slot,
                min_select = constraints.MinSelect,
                max_select = constraints.MaxSelect,
                can_skip = constraints.CanSkip,
                cards
            }
        };
    }
}
