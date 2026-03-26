using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles choosing a card from a <see cref="CardReward" /> or skipping the card reward entirely.
///     Uses reward type + card ID + nth for stable identification.
/// </summary>
public static class ChooseCardHandler
{
    private static readonly ModLogger Logger = new("ChooseCardHandler");

    /// <summary>
    ///     Handles the choose_card request.
    ///     Validates parameters and delegates to ExecuteAsync.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.RewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        if (string.IsNullOrEmpty(request.CardId))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card ID required (--card_id)" };

        if (request.RewardType != "card")
            return new { ok = false, error = "INVALID_REWARD_TYPE", message = "choose_card only supports --type card" };

        var nthValue = request.Nth ?? 0;
        Logger.Info($"Requested to choose card: type={request.RewardType}, card_id={request.CardId}, nth={nthValue}");

        return await ExecuteAsync(request.RewardType, request.CardId, nthValue);
    }

    /// <summary>
    ///     Handles the skip_card request.
    ///     Validates parameters and delegates to ExecuteSkipAsync.
    /// </summary>
    public static async Task<object> HandleSkipRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.RewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        if (request.RewardType != "card")
            return new { ok = false, error = "INVALID_REWARD_TYPE", message = "skip_card only supports --type card" };

        var nthValue = request.Nth ?? 0;
        Logger.Info($"Requested to skip card reward: type={request.RewardType}, nth={nthValue}");

        return await ExecuteSkipAsync(request.RewardType, nthValue);
    }

    /// <summary>
    ///     Picks a card from a card reward using card ID.
    ///     Must be called on the Godot main thread.
    /// </summary>
    /// <param name="rewardType">Reward type (only 'card' is supported).</param>
    /// <param name="cardId">Card ID to select (e.g., STRIKE_IRONCLAD).</param>
    /// <param name="nth">N-th card reward when multiple exists (0-based).</param>
    private static async Task<object> ExecuteAsync(string rewardType, string cardId, int nth)
    {
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
            var cardScreen = await RewardUiHelper.WaitForCardRewardScreen();
            if (cardScreen == null)
                return new
                {
                    ok = false, error = "TIMEOUT",
                    message = "Card reward selection screen did not open in time"
                };

            // Wait for cards to become clickable
            await Task.Delay(ActionUtils.CardEnableDelayMs);

            // --- Find and select the target card ---

            var cardHolders = RewardUiHelper.FindCardHolders(cardScreen);
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
    /// <param name="rewardType">Reward type (only 'card' is supported).</param>
    /// <param name="nth">N-th card reward when multiple exists (0-based).</param>
    private static async Task<object> ExecuteSkipAsync(string rewardType, int nth)
    {
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
            var cardScreen = await RewardUiHelper.WaitForCardRewardScreen();
            if (cardScreen == null)
                return new
                {
                    ok = false, error = "TIMEOUT",
                    message = "Card reward selection screen did not open in time"
                };

            // Wait for UI to settle
            await Task.Delay(ActionUtils.CardEnableDelayMs);

            // --- Find and click the Skip button ---

            var altButtons = RewardUiHelper.FindAlternativeButtons(cardScreen);
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
        var rewardButtons = RewardUiHelper.FindRewardButtons(screen);

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
    ///     Gets a list of available card IDs in a reward for error messages.
    /// </summary>
    private static List<string> GetAvailableCardIds(CardReward cardReward)
    {
        var ids = new List<string>();
        var cards = cardReward.Cards;

        foreach (var card in cards)
            ids.Add(card.Id.Entry);

        return ids;
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
}