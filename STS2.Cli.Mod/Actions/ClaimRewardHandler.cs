using Godot;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles claiming a non-card reward (gold, potion, relic, special card) by index.
///     Locates the reward button on the <see cref="NRewardsScreen" />, calls
///     <see cref="Reward.OnSelectWrapper" /> to claim, and removes the button from the UI.
/// </summary>
public static class ClaimRewardHandler
{
    private static readonly ModLogger Logger = new("ClaimRewardAction");

    /// <summary>
    ///     Claims a reward at the given index from the reward screen.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="rewardIndex">0-based index of the reward in the reward list.</param>
    public static async Task<object> ExecuteAsync(int rewardIndex)
    {
        try
        {
            // --- Validation ---

            var screen = FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            // Find reward buttons from the rewards container
            Control? rewardsContainer;
            try
            {
                rewardsContainer = screen.GetNode<Control>("%RewardsContainer");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to access RewardsContainer: {ex.Message}");
                return new { ok = false, error = "INTERNAL_ERROR", message = "Failed to access rewards container" };
            }

            if (rewardsContainer == null)
                return new { ok = false, error = "INTERNAL_ERROR", message = "Rewards container is null" };

            // Collect reward buttons
            var rewardButtons = new List<NRewardButton>();
            foreach (var child in rewardsContainer.GetChildren())
            {
                if (child is NRewardButton button)
                    rewardButtons.Add(button);
            }

            if (rewardIndex < 0 || rewardIndex >= rewardButtons.Count)
                return new
                {
                    ok = false, error = "INVALID_REWARD_INDEX",
                    message = $"Reward index {rewardIndex} out of range (screen has {rewardButtons.Count} rewards)"
                };

            var rewardButton = rewardButtons[rewardIndex];
            var reward = rewardButton.Reward;
            if (reward == null)
                return new { ok = false, error = "INTERNAL_ERROR", message = "Reward object is null" };

            // Card rewards must use choose_card / skip_card commands
            if (reward is CardReward)
                return new
                {
                    ok = false, error = "USE_CHOOSE_CARD",
                    message = "Card rewards must be claimed with choose_card or skipped with skip_card"
                };

            // CardRemovalReward not supported yet
            if (reward is CardRemovalReward)
                return new
                {
                    ok = false, error = "NOT_SUPPORTED",
                    message = "Card removal reward is not yet supported"
                };

            // --- Claim the reward ---

            var rewardType = GetRewardTypeName(reward);
            Logger.Info($"Claiming reward at index {rewardIndex}: {rewardType}");

            var success = await reward.OnSelectWrapper();
            if (!success)
            {
                // PotionReward returns false when belt is full
                if (reward is PotionReward)
                    return new
                    {
                        ok = false, error = "POTION_BELT_FULL",
                        message = "Cannot claim potion — potion belt is full"
                    };

                return new { ok = false, error = "CLAIM_FAILED", message = "Reward claim was not successful" };
            }

            // Remove the button from the rewards screen UI
            screen.RewardCollectedFrom(rewardButton);
            Logger.Info($"Reward claimed and removed from screen: {rewardType}");

            return new
            {
                ok = true,
                data = new
                {
                    reward_index = rewardIndex,
                    reward_type = rewardType,
                    claimed = true
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to claim reward: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds the <see cref="NRewardsScreen" /> in the overlay stack.
    /// </summary>
    private static NRewardsScreen? FindRewardsScreen()
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        // Fast path: top overlay is the rewards screen
        var top = overlayStack.Peek();
        if (top is NRewardsScreen rewardsScreen)
            return rewardsScreen;

        // Slow path: search children (card selection may be on top)
        foreach (var child in overlayStack.GetChildren())
        {
            if (child is NRewardsScreen found)
                return found;
        }

        return null;
    }

    /// <summary>
    ///     Gets the reward type name string for the response.
    /// </summary>
    private static string GetRewardTypeName(Reward reward) => reward switch
    {
        GoldReward => "Gold",
        PotionReward => "Potion",
        RelicReward => "Relic",
        SpecialCardReward => "SpecialCard",
        CardRemovalReward => "CardRemoval",
        _ => reward.GetType().Name
    };
}
