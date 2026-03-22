using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles choosing a card from a <see cref="CardReward" /> or skipping the card reward entirely.
///     Bypasses the <see cref="CardReward.OnSelect" /> UI flow (Option A from development plan):
///     directly calls <see cref="CardPileCmd.Add" />, records history, syncs, and removes the reward button.
/// </summary>
public static class ChooseCardHandler
{
    private static readonly ModLogger Logger = new("ChooseCardAction");

    /// <summary>
    ///     Picks a card from a card reward and adds it to the player's deck.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="rewardIndex">0-based index of the card reward in the reward list.</param>
    /// <param name="cardIndex">0-based index of the card within the card reward choices.</param>
    public static async Task<object> ExecuteAsync(int rewardIndex, int cardIndex)
    {
        try
        {
            // --- Validation ---

            var screen = FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            var (rewardButton, error) = FindCardRewardButton(screen, rewardIndex);
            if (error != null) return error;

            var cardReward = (CardReward)rewardButton!.Reward!;
            var cards = cardReward.Cards.ToList();

            if (cardIndex < 0 || cardIndex >= cards.Count)
                return new
                {
                    ok = false, error = "INVALID_CARD_INDEX",
                    message = $"Card index {cardIndex} out of range (reward has {cards.Count} card choices)"
                };

            var selectedCard = cards[cardIndex];

            // --- Add card to deck ---

            Logger.Info($"Choosing card at index {cardIndex}: {selectedCard.Id.Entry}");

            var addResult = await CardPileCmd.Add(selectedCard, PileType.Deck);
            if (!addResult.success)
            {
                Logger.Warning($"CardPileCmd.Add failed for card {selectedCard.Id.Entry}");
                return new { ok = false, error = "CLAIM_FAILED", message = "Failed to add card to deck" };
            }

            // Use the card model that was actually added (may differ due to hooks)
            var addedCard = addResult.cardAdded;

            // --- Record history (same as CardReward.OnSelect) ---

            var historyEntry = cardReward.Player?.RunState?.CurrentMapPointHistoryEntry;
            var netId = LocalContext.NetId;
            if (historyEntry != null && netId.HasValue)
            {
                // Record chosen card as picked
                historyEntry.GetEntry(netId.Value).CardChoices
                    .Add(new CardChoiceHistoryEntry(addedCard, wasPicked: true));

                // Record remaining cards as not picked
                foreach (var card in cards)
                {
                    if (card != selectedCard)
                    {
                        historyEntry.GetEntry(netId.Value).CardChoices
                            .Add(new CardChoiceHistoryEntry(card, wasPicked: false));
                    }
                }
            }
            else
            {
                Logger.Warning("Could not record card choice history (history entry or NetId unavailable)");
            }

            // Sync skipped cards
            foreach (var card in cards)
            {
                if (card != selectedCard)
                    RunManager.Instance.RewardSynchronizer.SyncLocalSkippedCard(card);
            }

            // Sync obtained card
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(addedCard);

            // --- Remove reward from screen ---

            screen.RewardCollectedFrom(rewardButton);
            Logger.Info($"Card '{addedCard.Id.Entry}' added to deck and reward removed from screen");

            return new
            {
                ok = true,
                data = new
                {
                    reward_index = rewardIndex,
                    card_index = cardIndex,
                    card_id = addedCard.Id.Entry,
                    card_name = StripGameTags(addedCard.Title)
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
    ///     Skips a card reward (takes nothing), recording all card options as not picked.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="rewardIndex">0-based index of the card reward in the reward list.</param>
    public static object ExecuteSkip(int rewardIndex)
    {
        try
        {
            // --- Validation ---

            var screen = FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            var (rewardButton, error) = FindCardRewardButton(screen, rewardIndex);
            if (error != null) return error;

            var cardReward = (CardReward)rewardButton!.Reward!;

            // --- Skip: call OnSkipped (records history for all cards as not picked) ---

            Logger.Info($"Skipping card reward at index {rewardIndex}");
            cardReward.OnSkipped();

            // --- Remove reward from screen ---

            screen.RewardCollectedFrom(rewardButton);
            Logger.Info("Card reward skipped and removed from screen");

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
        Control? rewardsContainer;
        try
        {
            rewardsContainer = screen.GetNode<Control>("%RewardsContainer");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to access RewardsContainer: {ex.Message}");
            return (null, new { ok = false, error = "INTERNAL_ERROR", message = "Failed to access rewards container" });
        }

        if (rewardsContainer == null)
            return (null, new { ok = false, error = "INTERNAL_ERROR", message = "Rewards container is null" });

        var rewardButtons = new List<NRewardButton>();
        foreach (var child in rewardsContainer.GetChildren())
        {
            if (child is NRewardButton button)
                rewardButtons.Add(button);
        }

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
    ///     Finds the <see cref="NRewardsScreen" /> in the overlay stack.
    /// </summary>
    private static NRewardsScreen? FindRewardsScreen()
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        var top = overlayStack.Peek();
        if (top is NRewardsScreen rewardsScreen)
            return rewardsScreen;

        foreach (var child in overlayStack.GetChildren())
        {
            if (child is NRewardsScreen found)
                return found;
        }

        return null;
    }
}
