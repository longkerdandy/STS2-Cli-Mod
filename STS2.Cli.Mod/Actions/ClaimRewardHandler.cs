using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles claiming a non-card reward (gold, potion, relic, special card) by index.
///     Uses <see cref="NClickableControl.ForceClick" /> on the <see cref="NRewardButton" />
///     to trigger the full game UI flow: claim animation, signal emission, and button removal.
/// </summary>
public static class ClaimRewardHandler
{
    private static readonly ModLogger Logger = new("ClaimRewardAction");

    /// <summary>
    ///     Maximum time to wait for the reward button to be removed from the UI
    ///     after ForceClick (covers claim animation for relic/potion fly-to-inventory).
    /// </summary>
    private const int ClaimTimeoutMs = 5000;

    /// <summary>
    ///     Polling interval when waiting for the reward button removal.
    /// </summary>
    private const int PollIntervalMs = 100;

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

            var screen = RewardUiHelper.FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            var rewardButtons = RewardUiHelper.FindRewardButtons(screen);

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

            // --- Claim the reward via ForceClick ---

            var rewardType = GetRewardTypeName(reward);
            Logger.Info($"Claiming reward at index {rewardIndex}: {rewardType} via ForceClick");

            rewardButton.ForceClick();

            // Wait for the reward button to be removed from the UI.
            // ForceClick triggers GetReward() -> OnSelectWrapper() -> RewardClaimed signal
            // -> RewardCollectedFrom() which removes the button from the rewards container.
            var removed = await WaitForButtonRemoval(rewardButton);
            if (!removed)
            {
                // Button still present — claim likely failed (e.g., potion belt full)
                if (reward is PotionReward)
                    return new
                    {
                        ok = false, error = "POTION_BELT_FULL",
                        message = "Cannot claim potion — potion belt is full"
                    };

                return new { ok = false, error = "CLAIM_FAILED", message = "Reward claim was not successful" };
            }

            Logger.Info($"Reward claimed successfully: {rewardType}");

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
    ///     Waits for a reward button to be removed from the scene tree after ForceClick.
    ///     The game removes the button via the <c>RewardClaimed</c> signal → <c>RewardCollectedFrom()</c>.
    /// </summary>
    /// <returns><c>true</c> if the button was removed within the timeout; <c>false</c> otherwise.</returns>
    private static async Task<bool> WaitForButtonRemoval(NRewardButton button)
    {
        var elapsed = 0;
        while (elapsed < ClaimTimeoutMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsed += PollIntervalMs;

            // Button removed from the tree means the reward was claimed
            if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
                return true;
        }

        Logger.Warning($"Timed out waiting for reward button removal after {ClaimTimeoutMs}ms");
        return false;
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
