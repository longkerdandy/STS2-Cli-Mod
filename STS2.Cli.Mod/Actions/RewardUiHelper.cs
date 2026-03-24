using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Shared UI helper methods for reward-related handlers.
///     Provides screen discovery, node traversal, and polling utilities
///     used by <see cref="ClaimRewardHandler" />, <see cref="ChooseCardHandler" />,
///     and <see cref="ProceedHandler" />.
/// </summary>
public static class RewardUiHelper
{
    private static readonly ModLogger Logger = new("RewardUiHelper");

    /// <summary>
    ///     Finds the <see cref="NRewardsScreen" /> in the overlay stack.
    ///     Checks both the top overlay and all children (in case a card selection screen is on top).
    /// </summary>
    public static NRewardsScreen? FindRewardsScreen()
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        // Fast path: the top overlay is the rewards screen
        var top = overlayStack.Peek();
        if (top is NRewardsScreen rewardsScreen)
            return rewardsScreen;

        // Slow path: search children (card selection may be on top)
        foreach (var child in overlayStack.GetChildren())
            if (child is NRewardsScreen found)
                return found;

        return null;
    }

    /// <summary>
    ///     Collects all <see cref="NRewardButton" /> instances from the rewards container.
    /// </summary>
    public static List<NRewardButton> FindRewardButtons(NRewardsScreen screen)
    {
        var buttons = new List<NRewardButton>();

        try
        {
            var rewardsContainer = screen.GetNode<Control>("%RewardsContainer");
            if (rewardsContainer == null) return buttons;

            foreach (var child in rewardsContainer.GetChildren())
                if (child is NRewardButton button)
                    buttons.Add(button);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to access RewardsContainer: {ex.Message}");
        }

        return buttons;
    }

    /// <summary>
    ///     Waits for the <see cref="NCardRewardSelectionScreen" /> to appear on the overlay stack.
    ///     Called after ForceClick on a card reward button, which triggers
    ///     <see cref="MegaCrit.Sts2.Core.Rewards.CardReward.OnSelect" /> to push the screen.
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait.</param>
    /// <param name="pollIntervalMs">Polling interval.</param>
    /// <returns>The card reward selection screen, or null if timed out.</returns>
    public static async Task<NCardRewardSelectionScreen?> WaitForCardRewardScreen(
        int timeoutMs = 3000, int pollIntervalMs = 100)
    {
        var elapsed = 0;
        while (elapsed < timeoutMs)
        {
            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;

            var overlay = NOverlayStack.Instance?.Peek();
            if (overlay is NCardRewardSelectionScreen cardScreen)
                return cardScreen;
        }

        Logger.Warning($"Timed out waiting for NCardRewardSelectionScreen after {timeoutMs}ms");
        return null;
    }

    /// <summary>
    ///     Finds all <see cref="NCardHolder" /> instances in a <see cref="NCardRewardSelectionScreen" />.
    ///     Cardholders are children of the <c>%CardRow</c> node.
    /// </summary>
    public static List<NCardHolder> FindCardHolders(NCardRewardSelectionScreen screen)
    {
        var holders = new List<NCardHolder>();

        try
        {
            var cardRow = screen.GetNode<Control>("UI/CardRow");
            if (cardRow == null) return holders;

            foreach (var child in cardRow.GetChildren())
                if (child is NCardHolder holder)
                    holders.Add(holder);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to access CardRow: {ex.Message}");
        }

        return holders;
    }

    /// <summary>
    ///     Finds all <see cref="NCardRewardAlternativeButton" /> instances (e.g., Skip, Reroll)
    ///     in a <see cref="NCardRewardSelectionScreen" />.
    /// </summary>
    public static List<NCardRewardAlternativeButton> FindAlternativeButtons(NCardRewardSelectionScreen screen)
    {
        var buttons = new List<NCardRewardAlternativeButton>();

        try
        {
            var container = screen.GetNode<Control>("UI/RewardAlternatives");
            if (container == null) return buttons;

            foreach (var child in container.GetChildren())
                if (child is NCardRewardAlternativeButton button)
                    buttons.Add(button);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to access RewardAlternatives: {ex.Message}");
        }

        return buttons;
    }

    /// <summary>
    ///     Recursively searches for the first node of type <typeparamref name="T" />
    ///     within the given <paramref name="start" /> node's subtree.
    ///     Mirrors <c>UiHelper.FindFirst&lt;T&gt;</c> from the AutoSlayer.
    /// </summary>
    public static T? FindFirst<T>(Node start) where T : Node
    {
        if (!GodotObject.IsInstanceValid(start))
            return null;

        if (start is T result)
            return result;

        foreach (var child in start.GetChildren())
        {
            var found = FindFirst<T>(child);
            if (found != null)
                return found;
        }

        return null;
    }
}