using Godot;
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
///     Uses the full game UI flow: ForceClick on the <see cref="NRewardButton" /> to open the
///     <see cref="NCardRewardSelectionScreen" />, then emit <see cref="NCardHolder.SignalName.Pressed" />
///     on the target card holder (or ForceClick on the skip button).
///     This ensures all game hooks, animations, history recording, and sync are handled by the game.
/// </summary>
public static class ChooseCardHandler
{
    private static readonly ModLogger Logger = new("ChooseCardAction");

    /// <summary>
    ///     Delay after the card reward selection screen opens before interacting with cards.
    ///     The game disables cards for 350ms via <c>DisableCardsForShortTimeAfterOpening()</c>.
    /// </summary>
    private const int CardEnableDelayMs = 500;

    /// <summary>
    ///     Maximum time to wait for the card reward button to be removed from the UI
    ///     after choosing a card (covers card-fly VFX animation).
    /// </summary>
    private const int CompletionTimeoutMs = 5000;

    /// <summary>
    ///     Polling interval when waiting for UI transitions.
    /// </summary>
    private const int PollIntervalMs = 100;

    /// <summary>
    ///     Picks a card from a card reward by driving the full game UI flow.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="rewardIndex">0-based index of the card reward in the reward list.</param>
    /// <param name="cardIndex">0-based index of the card within the card reward choices.</param>
    public static async Task<object> ExecuteAsync(int rewardIndex, int cardIndex)
    {
        try
        {
            // --- Validation ---

            var screen = RewardUiHelper.FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            var (rewardButton, error) = FindCardRewardButton(screen, rewardIndex);
            if (error != null) return error;

            // Validate card index against the card reward's choices
            var cardReward = (CardReward)rewardButton!.Reward!;
            var cardCount = cardReward.Cards.Count();
            if (cardIndex < 0 || cardIndex >= cardCount)
                return new
                {
                    ok = false, error = "INVALID_CARD_INDEX",
                    message = $"Card index {cardIndex} out of range (reward has {cardCount} card choices)"
                };

            // --- Open the card reward selection screen via ForceClick ---

            Logger.Info($"Opening card reward screen at index {rewardIndex} via ForceClick");
            rewardButton.ForceClick();

            // Wait for NCardRewardSelectionScreen to appear on the overlay stack
            var cardScreen = await RewardUiHelper.WaitForCardRewardScreen();
            if (cardScreen == null)
                return new
                {
                    ok = false, error = "TIMEOUT",
                    message = "Card reward selection screen did not open in time"
                };

            // Wait for cards to become clickable (game disables them for 350ms after opening)
            await Task.Delay(CardEnableDelayMs);

            // --- Find and select the target card ---

            var cardHolders = RewardUiHelper.FindCardHolders(cardScreen);
            if (cardIndex >= cardHolders.Count)
                return new
                {
                    ok = false, error = "INVALID_CARD_INDEX",
                    message =
                        $"Card index {cardIndex} out of range (screen has {cardHolders.Count} card holders)"
                };

            var targetHolder = cardHolders[cardIndex];

            // Get card info before selecting (for response)
            var cardModel = targetHolder.CardModel;
            var cardId = cardModel?.Id.Entry ?? "UNKNOWN";
            var cardName = cardModel != null ? StripGameTags(cardModel.Title) : "Unknown";

            Logger.Info($"Selecting card at index {cardIndex}: {cardId} via Pressed signal");

            // Emit Pressed signal — same as AutoSlayer's approach.
            // NCardHolder does not extend NClickableControl, so ForceClick is not available.
            // The Pressed signal is connected to NCardRewardSelectionScreen.SelectCard()
            // which resolves the TaskCompletionSource in CardReward.OnSelect().
            targetHolder.EmitSignal(NCardHolder.SignalName.Pressed, targetHolder);

            // Wait for the reward button to be removed (indicates full flow completed)
            var completed = await WaitForRewardCompletion(rewardButton);
            if (!completed)
                Logger.Warning("Card selection completed but reward button was not removed in time");

            Logger.Info($"Card '{cardId}' selected successfully");

            return new
            {
                ok = true,
                data = new
                {
                    reward_index = rewardIndex,
                    card_index = cardIndex,
                    card_id = cardId,
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
    ///     Skips a card reward by driving the full game UI flow.
    ///     Opens the card selection screen, then clicks the Skip alternative button.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="rewardIndex">0-based index of the card reward in the reward list.</param>
    public static async Task<object> ExecuteSkipAsync(int rewardIndex)
    {
        try
        {
            // --- Validation ---

            var screen = RewardUiHelper.FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            var (rewardButton, error) = FindCardRewardButton(screen, rewardIndex);
            if (error != null) return error;

            // --- Open the card reward selection screen via ForceClick ---

            Logger.Info($"Opening card reward screen at index {rewardIndex} for skip via ForceClick");
            rewardButton!.ForceClick();

            // Wait for NCardRewardSelectionScreen to appear on the overlay stack
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

            // The first alternative button is typically "Skip"
            // (generated by CardRewardAlternative.Generate when CanSkip is true)
            var skipButton = altButtons[0];

            Logger.Info("Clicking Skip button via ForceClick");
            skipButton.ForceClick();

            // After skip, the card reward screen closes and the reward button stays
            // (DismissScreenAndKeepReward). Wait for the card screen to be removed.
            var dismissed = await WaitForCardScreenDismissed();
            if (!dismissed)
                Logger.Warning("Card reward screen was not dismissed in time after skip");

            Logger.Info("Card reward skipped successfully");

            return new
            {
                ok = true,
                data = new
                {
                    reward_index = rewardIndex,
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
    ///     Finds the <see cref="NRewardButton" /> at the given index and validates it is a <see cref="CardReward" />.
    /// </summary>
    /// <returns>
    ///     A tuple of (button, null) on success, or (null, errorResponse) on failure.
    /// </returns>
    private static (NRewardButton? button, object? error) FindCardRewardButton(
        NRewardsScreen screen, int rewardIndex)
    {
        var rewardButtons = RewardUiHelper.FindRewardButtons(screen);

        if (rewardIndex < 0 || rewardIndex >= rewardButtons.Count)
            return (null, new
            {
                ok = false, error = "INVALID_REWARD_INDEX",
                message = $"Reward index {rewardIndex} out of range (screen has {rewardButtons.Count} rewards)"
            });

        var rewardButton = rewardButtons[rewardIndex];
        var reward = rewardButton.Reward;
        if (reward == null)
            return (null, new { ok = false, error = "INTERNAL_ERROR", message = "Reward object is null" });

        if (reward is not CardReward)
            return (null, new
            {
                ok = false, error = "NOT_CARD_REWARD",
                message = $"Reward at index {rewardIndex} is not a card reward (type: {reward.GetType().Name})"
            });

        return (rewardButton, null);
    }

    /// <summary>
    ///     Waits for a reward button to be removed from the scene tree after card selection.
    ///     The game removes the button via <c>RewardClaimed</c> signal → <c>RewardCollectedFrom()</c>.
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
    ///     After skip, <c>CardReward.OnSelect()</c> removes the screen and returns false.
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
