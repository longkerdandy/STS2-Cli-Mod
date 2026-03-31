using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Models.Actions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for finding and interacting with UI nodes in the Godot scene tree.
///     Provides generic tree-traversal helpers (<see cref="FindAll{T}" />, <see cref="FindFirst{T}" />),
///     overlay-stack screen lookup (<see cref="FindScreenInOverlay{T}" />),
///     and card-selection-screen helpers used by potion and deck selection handlers.
/// </summary>
public static class UiUtils
{
    // ── Generic tree-traversal helpers ────────────────────────────────

    /// <summary>
    ///     Recursively finds all descendant nodes of type <typeparamref name="T" />
    ///     within <paramref name="parent" /> (depth-first, inclusive).
    /// </summary>
    /// <typeparam name="T">The Godot node type to search for.</typeparam>
    /// <param name="parent">The root node to start the search from.</param>
    /// <returns>A list of all matching nodes; empty if none found.</returns>
    public static List<T> FindAll<T>(Node parent) where T : Node
    {
        var results = new List<T>();
        FindAllRecursive(parent, results);
        return results;
    }

    /// <summary>
    ///     Recursively finds the first descendant node of type <typeparamref name="T" />
    ///     within <paramref name="parent" /> (depth-first, inclusive).
    /// </summary>
    /// <typeparam name="T">The Godot node type to search for.</typeparam>
    /// <param name="parent">The root node to start the search from.</param>
    /// <returns>The first matching node, or <c>null</c> if none found.</returns>
    public static T? FindFirst<T>(Node parent) where T : Node
    {
        if (parent is T typedNode)
            return typedNode;

        foreach (var child in parent.GetChildren())
        {
            var result = FindFirst<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    // ── Overlay-stack screen lookup ──────────────────────────────────

    /// <summary>
    ///     Finds a screen of type <typeparamref name="T" /> in the game's <see cref="NOverlayStack" />.
    ///     Checks the top overlay first (fast path), then iterates all children (slow path).
    /// </summary>
    /// <typeparam name="T">The screen type to find (must extend <see cref="Node" />).</typeparam>
    /// <returns>The screen instance if found, or <c>null</c>.</returns>
    public static T? FindScreenInOverlay<T>() where T : Node
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        // Fast path: the top overlay is the screen we want
        if (overlayStack.Peek() is T screen)
            return screen;

        // Slow path: search children (another screen may be on top)
        foreach (var child in overlayStack.GetChildren())
            if (child is T found)
                return found;

        return null;
    }

    // ── Card selection screen helpers ────────────────────────────────

    /// <summary>
    ///     Finds the currently open <see cref="NChooseACardSelectionScreen" /> in the overlay stack.
    ///     Used by potion selection, deck selection (SMITH), and state detection.
    /// </summary>
    /// <returns>The card selection screen if found, or <c>null</c>.</returns>
    public static NChooseACardSelectionScreen? FindCardSelectionScreen()
    {
        return FindScreenInOverlay<NChooseACardSelectionScreen>();
    }

    /// <summary>
    ///     Extracts all selectable cards from a <see cref="NChooseACardSelectionScreen" />
    ///     as a list of <see cref="SelectableCardDto" />.
    /// </summary>
    /// <param name="screen">The card selection screen to extract from.</param>
    /// <returns>A list of selectable card DTOs; empty if no cards found.</returns>
    public static List<SelectableCardDto> ExtractSelectableCards(NChooseACardSelectionScreen screen)
    {
        var cards = new List<SelectableCardDto>();
        var cardHolders = FindAll<NCardHolder>(screen);

        for (var i = 0; i < cardHolders.Count; i++)
        {
            var holder = cardHolders[i];
            var card = holder.CardModel;
            if (card == null) continue;

            cards.Add(new SelectableCardDto
            {
                Index = i,
                CardId = card.Id.Entry,
                CardName = TextUtils.StripGameTags(card.Title),
                CardType = card.Type.ToString(),
                Cost = card.EnergyCost.Canonical,
                Description = TextUtils.StripGameTags(card.Description.GetFormattedText())
            });
        }

        return cards;
    }

    /// <summary>
    ///     Finds a <see cref="NCardHolder" /> in a card selection screen by card ID and nth occurrence.
    /// </summary>
    /// <param name="screen">The card selection screen to search.</param>
    /// <param name="cardId">Card ID to find (case-insensitive).</param>
    /// <param name="nth">Zero-based occurrence index when multiple copies exist.</param>
    /// <returns>The matching card holder, or <c>null</c> if not found or nth is out of range.</returns>
    public static NCardHolder? FindCardHolderById(NChooseACardSelectionScreen screen, string cardId, int nth)
    {
        var cardHolders = FindAll<NCardHolder>(screen);
        var matchingHolders = new List<NCardHolder>();

        foreach (var holder in cardHolders)
        {
            if (holder.CardModel?.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase) == true)
                matchingHolders.Add(holder);
        }

        if (nth < 0 || nth >= matchingHolders.Count)
            return null;

        return matchingHolders[nth];
    }

    /// <summary>
    ///     Finds the skip button on a <see cref="NChooseACardSelectionScreen" />.
    ///     Looks for a unique-name node <c>%SkipButton</c> first, then falls back to
    ///     a child whose name contains "Skip".
    /// </summary>
    /// <param name="screen">The card selection screen to search.</param>
    /// <returns>The skip button if found, or <c>null</c>.</returns>
    public static NButton? FindSkipButton(NChooseACardSelectionScreen screen)
    {
        // Try to find by unique node name
        var skipButton = screen.GetNodeOrNull<NButton>("%SkipButton");
        if (skipButton != null) return skipButton;

        // Fallback: search for any button with "skip" in its name
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

    /// <summary>
    ///     Reads the private <c>_canSkip</c> field from an <see cref="NChooseACardSelectionScreen" />
    ///     via reflection to determine if the selection can be skipped.
    /// </summary>
    /// <param name="screen">The card selection screen to check.</param>
    /// <returns><c>true</c> if the selection can be skipped; <c>false</c> otherwise.</returns>
    public static bool ReadCanSkip(NChooseACardSelectionScreen screen)
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

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    ///     Recursively collects all descendant nodes of type <typeparamref name="T" />.
    /// </summary>
    private static void FindAllRecursive<T>(Node node, List<T> results) where T : Node
    {
        if (node is T typedNode)
            results.Add(typedNode);

        foreach (var child in node.GetChildren())
            FindAllRecursive(child, results);
    }
}
