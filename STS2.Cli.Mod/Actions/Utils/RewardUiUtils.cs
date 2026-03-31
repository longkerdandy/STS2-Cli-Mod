using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions.Utils;

/// <summary>
///     Shared UI utility methods for reward-related handlers.
///     Provides node traversal and polling utilities for reward screens
///     used by <see cref="RewardClaimHandler" />, <see cref="RewardCardHandler" />,
///     and <see cref="ProceedHandler" />.
///     For generic screen discovery, use <see cref="UiUtils.FindScreenInOverlay{T}" /> directly.
/// </summary>
public static class RewardUiUtils
{
    private static readonly ModLogger Logger = new("RewardUiUtils");

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
    /// <param name="timeoutMs">Maximum time to wait (default <see cref="ActionUtils.ShortTimeoutMs" />).</param>
    /// <param name="pollIntervalMs">Polling interval (default <see cref="ActionUtils.DefaultPollIntervalMs" />).</param>
    /// <returns>The card reward selection screen, or null if timed out.</returns>
    public static async Task<NCardRewardSelectionScreen?> WaitForCardRewardScreen(
        int timeoutMs = ActionUtils.ShortTimeoutMs, int pollIntervalMs = ActionUtils.DefaultPollIntervalMs)
    {
        NCardRewardSelectionScreen? result = null;

        await ActionUtils.PollUntilAsync(() =>
        {
            var cardScreen = UiUtils.FindScreenInOverlay<NCardRewardSelectionScreen>();
            if (cardScreen != null)
            {
                result = cardScreen;
                return true;
            }

            return false;
        }, timeoutMs, pollIntervalMs);

        if (result == null)
            Logger.Warning($"Timed out waiting for NCardRewardSelectionScreen after {timeoutMs}ms");

        return result;
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
}