using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>reward_choose_card</c> and <c>reward_skip_card</c> CLI commands.
///     Chooses a card from a <see cref="CardReward" /> or skips the card reward entirely.
///     Uses reward type + card ID + nth for stable identification.
/// </summary>
/// <remarks>
///     <para><b>CLI commands:</b></para>
///     <list type="bullet">
///         <item><c>sts2 reward_choose_card --type card --card_id &lt;card_id&gt; [--nth &lt;n&gt;]</c></item>
///         <item><c>sts2 reward_skip_card --type card [--nth &lt;n&gt;]</c></item>
///     </list>
///     <para><b>Scene:</b> Reward screen (<see cref="NRewardsScreen" />) after combat, when a card reward is available.</para>
/// </remarks>
public static class RewardCardHandler
{
    private static readonly ModLogger Logger = new("RewardCardHandler");

    /// <summary>
    ///     Picks a card from a card reward using card ID.
    ///     Must be called on the Godot main thread.
    /// </summary>
    public static async Task<object> ExecuteAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.RewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        if (string.IsNullOrEmpty(request.CardId))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card ID required (--card_id)" };

        if (request.RewardType != "card")
            return new { ok = false, error = "INVALID_REWARD_TYPE", message = "choose_card only supports --type card" };

        var nth = request.Nth ?? 0;
        var rewardType = request.RewardType;
        var cardId = request.CardId;

        Logger.Info($"Requested to choose card: type={rewardType}, card_id={cardId}, nth={nth}");

        try
        {
            // --- Validation ---

            var screen = UiUtils.FindScreenInOverlay<NRewardsScreen>();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            // Find all card rewards
            var cardRewards = FindCardRewards(screen);

            if (cardRewards.Count == 0)
                return new { ok = false, error = "REWARD_NOT_FOUND", message = "No card rewards available" };

            // If multiple card rewards exist and nth is out of range
            if (nth < 0 || nth >= cardRewards.Count)
                return new
                {
                    ok = false,
                    error = "INVALID_REWARD_INDEX",
                    message = $"Card reward count: {cardRewards.Count}. Use nth from 0 to {cardRewards.Count - 1}."
                };

            var (rewardButton, cardReward) = cardRewards[nth];

            // Validate the card exists in this reward
            var targetCard = FindCardById(cardReward, cardId);
            if (targetCard == null)
            {
                var availableCards = string.Join(", ", cardReward.Cards.Select(c => c.Id.Entry));
                return new
                {
                    ok = false,
                    error = "CARD_NOT_FOUND",
                    message = $"Card '{cardId}' not found in reward. Available: [{availableCards}]"
                };
            }

            // --- Open the card reward selection screen via ForceClick ---

            Logger.Info($"Opening card reward screen (nth={nth}) via ForceClick to select card: {cardId}");
            rewardButton.ForceClick();

            // Wait for NCardRewardSelectionScreen to appear
            var cardScreen = await WaitForCardRewardScreen();
            if (cardScreen == null)
                return new
                {
                    ok = false, error = "TIMEOUT",
                    message = "Card reward selection screen did not open in time"
                };

            // Wait for cards to become clickable
            await Task.Delay(ActionUtils.CardEnableDelayMs);

            // --- Find and select the target card ---

            var cardHolders = FindCardHolders(cardScreen);
            var targetHolder = FindCardHolderById(cardHolders, cardId);

            if (targetHolder == null)
                return new
                {
                    ok = false, error = "CARD_NOT_FOUND",
                    message = $"Card '{cardId}' not found on the selection screen"
                };

            // Get card info before selecting (for response)
            var cardModel = targetHolder.CardModel;
            var selectedCardId = cardModel?.Id.Entry ?? "UNKNOWN";
            var cardName = cardModel != null ? StripGameTags(cardModel.Title) : "Unknown";

            Logger.Info($"Selecting card: {selectedCardId} via Pressed signal");

            // Emit Pressed signal
            targetHolder.EmitSignal(NCardHolder.SignalName.Pressed, targetHolder);

            // Wait for the reward button to be removed
            var completed = await WaitForRewardCompletion(rewardButton);
            if (!completed)
                Logger.Warning("Card selection completed but reward button was not removed in time");

            Logger.Info($"Card '{selectedCardId}' selected successfully");

            return new
            {
                ok = true,
                data = new
                {
                    reward_type = rewardType,
                    nth,
                    card_id = selectedCardId,
                    card_name = cardName
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to choose card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Skips a card reward.
    ///     Must be called on the Godot main thread.
    /// </summary>
    public static async Task<object> ExecuteSkipAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.RewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        if (request.RewardType != "card")
            return new { ok = false, error = "INVALID_REWARD_TYPE", message = "skip_card only supports --type card" };

        var nth = request.Nth ?? 0;
        var rewardType = request.RewardType;

        Logger.Info($"Requested to skip card reward: type={rewardType}, nth={nth}");

        try
        {
            // --- Validation ---

            var screen = UiUtils.FindScreenInOverlay<NRewardsScreen>();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            // Find all card rewards
            var cardRewards = FindCardRewards(screen);

            if (cardRewards.Count == 0)
                return new { ok = false, error = "REWARD_NOT_FOUND", message = "No card rewards available" };

            if (nth < 0 || nth >= cardRewards.Count)
                return new
                {
                    ok = false,
                    error = "INVALID_REWARD_INDEX",
                    message = $"Card reward count: {cardRewards.Count}. Use nth from 0 to {cardRewards.Count - 1}."
                };

            var (rewardButton, _) = cardRewards[nth];

            // --- Open the card reward selection screen via ForceClick ---

            Logger.Info($"Opening card reward screen (nth={nth}) for skip via ForceClick");
            rewardButton.ForceClick();

            // Wait for NCardRewardSelectionScreen to appear
            var cardScreen = await WaitForCardRewardScreen();
            if (cardScreen == null)
                return new
                {
                    ok = false, error = "TIMEOUT",
                    message = "Card reward selection screen did not open in time"
                };

            // Wait for UI to settle
            await Task.Delay(ActionUtils.CardEnableDelayMs);

            // --- Find and click the Skip button ---

            var altButtons = FindAlternativeButtons(cardScreen);
            if (altButtons.Count == 0)
                return new
                {
                    ok = false, error = "INTERNAL_ERROR",
                    message = "No alternative buttons found on card reward screen"
                };

            var skipButton = altButtons[0];

            Logger.Info("Clicking Skip button via ForceClick");
            skipButton.ForceClick();

            // Wait for the card screen to be dismissed
            var dismissed = await WaitForCardScreenDismissed();
            if (!dismissed)
                Logger.Warning("Card reward screen was not dismissed in time after skip");

            Logger.Info("Card reward skipped successfully");

            return new
            {
                ok = true,
                data = new
                {
                    reward_type = rewardType,
                    nth,
                    skipped = true
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to skip card reward: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds all card rewards on the screen.
    /// </summary>
    private static List<(NRewardButton Button, CardReward CardReward)> FindCardRewards(NRewardsScreen screen)
    {
        var result = new List<(NRewardButton Button, CardReward CardReward)>();
        var rewardButtons = UiUtils.FindRewardButtons(screen);

        foreach (var button in rewardButtons)
            if (button.Reward is CardReward cardReward)
                result.Add((button, cardReward));

        return result;
    }

    /// <summary>
    ///     Finds a card by ID within a card reward.
    /// </summary>
    private static CardModel? FindCardById(CardReward cardReward, string cardId)
    {
        var cards = cardReward.Cards;

        foreach (var card in cards)
            if (card.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase))
                return card;

        return null;
    }

    /// <summary>
    ///     Finds the cardholder by card ID.
    /// </summary>
    private static NCardHolder? FindCardHolderById(List<NCardHolder> cardHolders, string cardId)
    {
        foreach (var holder in cardHolders)
            if (holder.CardModel?.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase) == true)
                return holder;
        return null;
    }

    /// <summary>
    ///     Waits for a reward button to be removed from the scene tree after card selection.
    /// </summary>
    private static async Task<bool> WaitForRewardCompletion(NRewardButton button)
    {
        return await ActionUtils.PollUntilAsync(
            () => !GodotObject.IsInstanceValid(button) || !button.IsInsideTree(),
            ActionUtils.UiTimeoutMs);
    }

    /// <summary>
    ///     Waits for the <see cref="NCardRewardSelectionScreen" /> to be removed from the overlay stack.
    /// </summary>
    private static async Task<bool> WaitForCardScreenDismissed()
    {
        return await ActionUtils.PollUntilAsync(
            () => NOverlayStack.Instance?.Peek() is not NCardRewardSelectionScreen,
            ActionUtils.UiTimeoutMs);
    }

    /// <summary>
    ///     Waits for the <see cref="NCardRewardSelectionScreen" /> to appear on the overlay stack.
    ///     Called after ForceClick on a card reward button.
    /// </summary>
    private static async Task<NCardRewardSelectionScreen?> WaitForCardRewardScreen(
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
    ///     Cardholders are children of the <c>UI/CardRow</c> node.
    /// </summary>
    private static List<NCardHolder> FindCardHolders(NCardRewardSelectionScreen screen)
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
    private static List<NCardRewardAlternativeButton> FindAlternativeButtons(NCardRewardSelectionScreen screen)
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
