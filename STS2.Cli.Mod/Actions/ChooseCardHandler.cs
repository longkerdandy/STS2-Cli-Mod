using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
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
    ///     Delay after the card reward selection screen opens before interacting with cards.
    /// </summary>
    private const int CardEnableDelayMs = 500;

    /// <summary>
    ///     Maximum time to wait for the card reward button to be removed from the UI.
    /// </summary>
    private const int CompletionTimeoutMs = 5000;

    /// <summary>
    ///     Polling interval when waiting for UI transitions.
    /// </summary>
    private const int PollIntervalMs = 100;

    /// <summary>
    ///     Picks a card from a card reward using card ID.
    ///     Must be called on the Godot main thread.
    /// </summary>
    /// <param name="rewardType">Reward type (only 'card' is supported).</param>
    /// <param name="cardId">Card ID to select (e.g., STRIKE_IRONCLAD).</param>
    /// <param name="nth">N-th card reward when multiple exist (0-based).</param>
    public static async Task<object> ExecuteAsync(string rewardType, string cardId, int nth)
    {
        try
        {
            // --- Validation ---

            var screen = RewardUiHelper.FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            // Find all card rewards
            var cardRewards = FindCardRewards(screen);

            if (cardRewards.Count == 0)
                return new { ok = false, error = "REWARD_NOT_FOUND", message = "No card rewards available" };

            // If multiple card rewards exist and nth is out of range
            if (nth < 0 || nth >= cardRewards.Count)
            {
                return new
                {
                    ok = false,
                    error = "INVALID_REWARD_INDEX",
                    message = $"Card reward count: {cardRewards.Count}. Use nth from 0 to {cardRewards.Count - 1}."
                };
            }

            var (rewardButton, cardReward) = cardRewards[nth];

            // Find the specific card by ID within this reward
            var cardChoice = FindCardById(cardReward, cardId);
            if (cardChoice == null)
            {
                var availableCards = GetAvailableCardIds(cardReward);
                Logger.Warning($"Card '{cardId}' not found in card reward (nth={nth}). Available: {string.Join(", ", availableCards)}");

                return new
                {
                    ok = false,
                    error = "CARD_NOT_FOUND",
                    message = $"Card '{cardId}' not found in the card reward",
                    available_cards = availableCards
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
            await Task.Delay(CardEnableDelayMs);

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
                    nth = nth,
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
    /// <param name="nth">N-th card reward when multiple exist (0-based).</param>
    public static async Task<object> ExecuteSkipAsync(string rewardType, int nth)
    {
        try
        {
            // --- Validation ---

            var screen = RewardUiHelper.FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            // Find all card rewards
            var cardRewards = FindCardRewards(screen);

            if (cardRewards.Count == 0)
                return new { ok = false, error = "REWARD_NOT_FOUND", message = "No card rewards available" };

            if (nth < 0 || nth >= cardRewards.Count)
            {
                return new
                {
                    ok = false,
                    error = "INVALID_REWARD_INDEX",
                    message = $"Card reward count: {cardRewards.Count}. Use nth from 0 to {cardRewards.Count - 1}."
                };
            }

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
            await Task.Delay(CardEnableDelayMs);

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
                    nth = nth,
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
        {
            if (button.Reward is CardReward cardReward)
                result.Add((button, cardReward));
        }

        return result;
    }

    /// <summary>
    ///     Finds a card by ID within a card reward.
    /// </summary>
    private static CardModel? FindCardById(CardReward cardReward, string cardId)
    {
        var cards = cardReward.Cards;
        if (cards == null) return null;

        foreach (var card in cards)
        {
            if (card.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase))
                return card;
        }

        return null;
    }

    /// <summary>
    ///     Gets list of available card IDs in a reward for error messages.
    /// </summary>
    private static List<string> GetAvailableCardIds(CardReward cardReward)
    {
        var ids = new List<string>();
        var cards = cardReward.Cards;
        if (cards == null) return ids;

        foreach (var card in cards)
            ids.Add(card.Id.Entry);

        return ids;
    }

    /// <summary>
    ///     Finds the card holder by card ID.
    /// </summary>
    private static NCardHolder? FindCardHolderById(List<NCardHolder> cardHolders, string cardId)
    {
        foreach (var holder in cardHolders)
        {
            if (holder.CardModel?.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase) == true)
                return holder;
        }
        return null;
    }

    /// <summary>
    ///     Waits for a reward button to be removed from the scene tree after card selection.
    /// </summary>
    private static async Task<bool> WaitForRewardCompletion(NRewardButton button)
    {
        var elapsed = 0;
        while (elapsed < CompletionTimeoutMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsed += PollIntervalMs;

            if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Waits for the <see cref="NCardRewardSelectionScreen" /> to be removed from the overlay stack.
    /// </summary>
    private static async Task<bool> WaitForCardScreenDismissed()
    {
        var elapsed = 0;
        while (elapsed < CompletionTimeoutMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsed += PollIntervalMs;

            var overlay = NOverlayStack.Instance?.Peek();
            if (overlay is not NCardRewardSelectionScreen)
                return true;
        }

        return false;
    }
}
