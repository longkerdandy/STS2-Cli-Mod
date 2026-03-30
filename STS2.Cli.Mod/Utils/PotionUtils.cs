using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Actions;
using STS2.Cli.Mod.State.Builders;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for potion-related metadata queries and response building.
///     Classifies potions by the type of card-selection UI they open
///     (<see cref="GetSelectionUiType" />) and builds the appropriate
///     <c>selection_required</c> response for each UI type.
/// </summary>
public static class PotionUtils
{
    /// <summary>
    ///     Potions that open a Hand Select UI (Type A) — player picks cards from hand.
    /// </summary>
    private static readonly HashSet<string> HandSelectPotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GAMBLERS_BREW",       // Choose 0-N from hand (discard and redraw)
        "ASHWATER",            // Choose 0-N from hand (exhaust)
        "TOUCH_OF_INSANITY"   // Choose 1 from hand (make free)
    };

    /// <summary>
    ///     Potions that open a Grid Select UI (Type B) — overlay grid showing draw/discard pile.
    /// </summary>
    private static readonly HashSet<string> GridSelectPotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIQUID_MEMORIES",         // Choose 1 from discard pile
        "DROPLET_OF_PRECOGNITION" // Choose 1 from draw pile
    };

    /// <summary>
    ///     Potions that open a Tri Select UI (Type C) — choose 1 of up to 3 generated cards.
    /// </summary>
    private static readonly HashSet<string> TriSelectPotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ATTACK_POTION",    // Choose 1 of 3 attacks
        "SKILL_POTION",     // Choose 1 of 3 skills
        "POWER_POTION",     // Choose 1 of 3 powers
        "COLORLESS_POTION" // Choose 1 of 3 colorless (can skip)
    };

    /// <summary>
    ///     Checks if a potion requires any form of card selection when used.
    /// </summary>
    /// <param name="potionId">The potion ID to check.</param>
    /// <returns>True if the potion opens a card selection screen of any type.</returns>
    public static bool RequiresCardSelection(string potionId)
    {
        return HandSelectPotions.Contains(potionId)
               || GridSelectPotions.Contains(potionId)
               || TriSelectPotions.Contains(potionId);
    }

    /// <summary>
    ///     Gets the UI type of the card selection screen a potion opens.
    /// </summary>
    /// <param name="potionId">The potion ID.</param>
    /// <returns>
    ///     <c>"hand_select"</c> for Type A,
    ///     <c>"grid_select"</c> for Type B,
    ///     <c>"tri_select"</c> for Type C,
    ///     or <c>null</c> if the potion does not require card selection.
    /// </returns>
    public static string? GetSelectionUiType(string potionId)
    {
        if (HandSelectPotions.Contains(potionId)) return "hand_select";
        if (GridSelectPotions.Contains(potionId)) return "grid_select";
        if (TriSelectPotions.Contains(potionId)) return "tri_select";
        return null;
    }

    /// <summary>
    ///     Gets the selection type category for a potion (describes the source of selectable cards).
    /// </summary>
    /// <param name="potionId">The potion ID.</param>
    /// <returns>Selection type string describing the card source.</returns>
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

    // ── Response builders per UI type ────────────────────────────────

    /// <summary>
    ///     Builds a <c>selection_required</c> response for potions that opened a Tri Select screen (Type C).
    /// </summary>
    /// <param name="potion">The potion model that triggered the selection.</param>
    /// <param name="slot">The potion belt slot index.</param>
    /// <param name="selectionScreen">The <see cref="NChooseACardSelectionScreen" /> to extract data from.</param>
    /// <returns>A success response with selection details and <c>next_action = "tri_select_card"</c>.</returns>
    public static object BuildTriSelectResponse(
        PotionModel potion, int slot, NChooseACardSelectionScreen selectionScreen)
    {
        var cards = UiUtils.ExtractSelectableCards(selectionScreen);
        var canSkip = ReadCanSkip(selectionScreen);

        return new
        {
            ok = true,
            data = new
            {
                status = "selection_required",
                next_action = "tri_select_card",
                selection_type = GetSelectionType(potion.Id.Entry),
                potion_id = potion.Id.Entry,
                potion_slot = slot,
                min_select = canSkip ? 0 : 1,
                max_select = 1,
                can_skip = canSkip,
                cards
            }
        };
    }

    /// <summary>
    ///     Builds a <c>selection_required</c> response for potions that opened a Hand Select screen (Type A).
    /// </summary>
    /// <param name="potion">The potion model that triggered the selection.</param>
    /// <param name="slot">The potion belt slot index.</param>
    /// <returns>A success response with selection details and <c>next_action = "hand_select_card"</c>.</returns>
    public static object BuildHandSelectResponse(PotionModel potion, int slot)
    {
        var handState = HandSelectStateBuilder.Build();
        return new
        {
            ok = true,
            data = new
            {
                status = "selection_required",
                next_action = "hand_select_card",
                selection_type = GetSelectionType(potion.Id.Entry),
                potion_id = potion.Id.Entry,
                potion_slot = slot,
                hand_select = handState
            }
        };
    }

    /// <summary>
    ///     Builds a <c>selection_required</c> response for potions that opened a Grid Select screen (Type B).
    /// </summary>
    /// <param name="potion">The potion model that triggered the selection.</param>
    /// <param name="slot">The potion belt slot index.</param>
    /// <param name="gridScreen">The <see cref="NCardGridSelectionScreen" /> to extract data from.</param>
    /// <returns>A success response with selection details and <c>next_action = "grid_select_card"</c>.</returns>
    public static object BuildGridSelectResponse(
        PotionModel potion, int slot, NCardGridSelectionScreen gridScreen)
    {
        var gridState = GridCardSelectStateBuilder.Build(gridScreen);
        return new
        {
            ok = true,
            data = new
            {
                status = "selection_required",
                next_action = "grid_select_card",
                selection_type = GetSelectionType(potion.Id.Entry),
                potion_id = potion.Id.Entry,
                potion_slot = slot,
                grid_card_select = gridState
            }
        };
    }

    // ── Private helpers ─────────────────────────────────────────────

    /// <summary>
    ///     Reads the private <c>_canSkip</c> field from an <see cref="NChooseACardSelectionScreen" />
    ///     via reflection to determine if the selection can be skipped.
    /// </summary>
    private static bool ReadCanSkip(NChooseACardSelectionScreen screen)
    {
        try
        {
            var field = typeof(NChooseACardSelectionScreen).GetField("_canSkip",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(screen) as bool? ?? false;
        }
        catch
        {
            return false;
        }
    }
}
