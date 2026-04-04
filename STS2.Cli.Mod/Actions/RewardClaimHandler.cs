using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>reward_claim</c> CLI command.
///     Claims a non-card reward (gold, potion, relic, special card) by type + ID.
///     Uses <see cref="NClickableControl.ForceClick" /> on the <see cref="NRewardButton" />
///     to trigger the full game UI flow: claim animation, signal emission, and button removal.
/// </summary>
/// <remarks>
///     <para><b>CLI commands:</b> <c>sts2 reward_claim --type &lt;type&gt; [--id &lt;id&gt;] [--nth &lt;n&gt;]</c></para>
///     <para><b>Scene:</b> Reward screen (<see cref="NRewardsScreen" />) after combat or events.</para>
/// </remarks>
public static class RewardClaimHandler
{
    private static readonly ModLogger Logger = new("RewardClaimHandler");

    /// <summary>
    ///     Handles the <c>reward_claim</c> request.
    ///     Validates parameters and delegates to ExecuteAsync.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.RewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        var nthValue = request.Nth ?? 0;
        Logger.Info($"Requested to claim reward: type={request.RewardType}, id={request.Id ?? "null"}, nth={nthValue}");

        return await ExecuteAsync(request.RewardType, request.Id, nthValue);
    }

    /// <summary>
    ///     Claims a reward by type and optional ID.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="rewardType">Reward type: gold, potion, relic, special_card.</param>
    /// <param name="itemId">Item ID for potion/relic/special_card (optional for gold).</param>
    /// <param name="nth">N-th occurrence when multiple rewards of the same type exist (0-based).</param>
    private static async Task<object> ExecuteAsync(string rewardType, string? itemId, int nth)
    {
        try
        {
            // --- Validation ---

            var screen = UiUtils.FindScreenInOverlay<NRewardsScreen>();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            var rewardButtons = RewardCommonUiUtils.FindRewardButtons(screen);

            // Find rewards of the requested type
            var matchingRewards = FindRewardsByType(rewardButtons, rewardType, itemId);

            if (matchingRewards.Count == 0)
            {
                // Build a list of available reward types for the error message
                var availableTypes = GetAvailableRewardTypes(rewardButtons);
                Logger.Warning(
                    $"No {rewardType} reward found with id={itemId ?? "null"}. Available: {string.Join(", ", availableTypes)}");

                return new
                {
                    ok = false,
                    error = "REWARD_NOT_FOUND",
                    message = $"No {rewardType} reward found" + (itemId != null ? $" with ID '{itemId}'" : ""),
                    available_types = availableTypes
                };
            }

            // If only one matching reward, nth must be 0
            if (matchingRewards.Count == 1 && nth != 0)
                return new
                {
                    ok = false,
                    error = "AMBIGUOUS_REWARD",
                    message =
                        $"Only one {rewardType} reward available, but nth={nth} was specified. Use nth=0 or omit --nth."
                };

            // If multiple matching rewards, nth is required to be specified (or defaults to 0)
            if (nth < 0 || nth >= matchingRewards.Count)
            {
                var idPart = itemId != null ? $" with ID '{itemId}'" : "";
                return new
                {
                    ok = false,
                    error = "INVALID_REWARD_INDEX",
                    message =
                        $"{rewardType}{idPart} has {matchingRewards.Count} copies. Use nth from 0 to {matchingRewards.Count - 1}."
                };
            }

            var (rewardButton, reward) = matchingRewards[nth];

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

            var rewardTypeName = GetRewardTypeName(reward);
            Logger.Info($"Claiming {rewardType} reward (nth={nth}): {rewardTypeName} via ForceClick");

            rewardButton.ForceClick();

            // Wait for the reward button to be removed from the UI.
            var removed = await WaitForButtonRemoval(rewardButton);
            if (!removed)
            {
                // Button still presents — claim likely failed (e.g., potion belt full)
                if (reward is PotionReward)
                    return new
                    {
                        ok = false, error = "POTION_BELT_FULL",
                        message = "Cannot claim potion — potion belt is full"
                    };

                return new { ok = false, error = "CLAIM_FAILED", message = "Reward claim was not successful" };
            }

            Logger.Info($"Reward claimed successfully: {rewardTypeName}");

            return new
            {
                ok = true,
                data = new
                {
                    reward_type = rewardType,
                    item_id = itemId,
                    nth,
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
    ///     Finds all rewards matching the specified type and optional ID.
    /// </summary>
    private static List<(NRewardButton Button, Reward Reward)> FindRewardsByType(
        List<NRewardButton> rewardButtons, string rewardType, string? itemId)
    {
        var result = new List<(NRewardButton Button, Reward Reward)>();

        foreach (var button in rewardButtons)
        {
            var reward = button.Reward;
            if (reward == null) continue;

            var matches = rewardType.ToLower() switch
            {
                "gold" => reward is GoldReward,
                "potion" => reward is PotionReward pr &&
                            (itemId == null || MatchesId(pr.Potion?.Id.Entry, itemId)),
                "relic" => reward is RelicReward rr &&
                           (itemId == null || MatchesId(rr.ClaimedRelic?.Id.Entry, itemId) ||
                            MatchesId(UiUtils.GetPrivateField<RelicModel>(rr, "_relic")?.Id.Entry, itemId)),
                "special_card" => reward is SpecialCardReward scr &&
                                  (itemId == null || MatchesId(UiUtils.GetPrivateField<CardModel>(scr, "_card")?.Id.Entry, itemId)),
                _ => false
            };

            if (matches)
                result.Add((button, reward));
        }

        return result;
    }

    /// <summary>
    ///     Checks if two IDs match (case-insensitive).
    /// </summary>
    private static bool MatchesId(string? actualId, string? expectedId)
    {
        if (actualId == null || expectedId == null) return false;
        return actualId.Equals(expectedId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets a list of available reward types for error messages.
    /// </summary>
    private static List<string> GetAvailableRewardTypes(List<NRewardButton> rewardButtons)
    {
        var types = new List<string>();
        foreach (var button in rewardButtons)
            if (button.Reward != null)
                types.Add(GetRewardTypeName(button.Reward));
        return types;
    }

    /// <summary>
    ///     Waits for a reward button to be removed from the scene tree after ForceClick.
    /// </summary>
    private static async Task<bool> WaitForButtonRemoval(NRewardButton button)
    {
        var removed = await ActionUtils.PollUntilAsync(
            () => !GodotObject.IsInstanceValid(button) || !button.IsInsideTree(),
            ActionUtils.UiTimeoutMs);

        if (!removed)
            Logger.Warning($"Timed out waiting for reward button removal after {ActionUtils.UiTimeoutMs}ms");

        return removed;
    }

    /// <summary>
    ///     Gets the reward type name string for the response.
    /// </summary>
    private static string GetRewardTypeName(Reward reward)
    {
        return reward switch
        {
            GoldReward => "Gold",
            PotionReward => "Potion",
            RelicReward => "Relic",
            SpecialCardReward => "SpecialCard",
            CardRemovalReward => "CardRemoval",
            CardReward => "Card",
            _ => reward.GetType().Name
        };
    }
}