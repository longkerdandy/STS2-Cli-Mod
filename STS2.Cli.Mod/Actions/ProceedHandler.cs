using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles proceeding to the map from various screens.
///     Supports:
///     - Reward screen (NRewardsScreen with NProceedButton)
///     - FakeMerchant custom event (NFakeMerchant with NProceedButton)
///     - Rest site (NRestSiteRoom with NProceedButton, after choosing an option)
///     Mimics the AutoSlayer / STS2MCP approach: finds the <see cref="NProceedButton" /> and calls
///     <see cref="NClickableControl.ForceClick" />, which triggers the full UI flow.
/// </summary>
public static class ProceedHandler
{
    private static readonly ModLogger Logger = new("ProceedHandler");

    /// <summary>
    ///     Handles the proceed request.
    ///     Automatically detects the current context (reward screen, FakeMerchant event, or rest site).
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        Logger.Info("Requested to proceed");
        return await ExecuteAsync();
    }

    /// <summary>
    ///     Executes the proceed action based on detected context.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    private static async Task<object> ExecuteAsync()
    {
        try
        {
            // --- Try Reward Screen first ---

            var rewardScreen = RewardUiHelper.FindRewardsScreen();
            if (rewardScreen != null)
            {
                Logger.Info("Detected reward screen context");
                return ExecuteRewardProceed(rewardScreen);
            }

            // --- Try FakeMerchant Event ---

            var eventRoom = NEventRoom.Instance;
            if (eventRoom != null && eventRoom.IsInsideTree())
            {
                var fakeMerchant = UiUtils.FindFirst<NFakeMerchant>(eventRoom);
                if (fakeMerchant != null)
                {
                    Logger.Info("Detected FakeMerchant event context");
                    return await ExecuteFakeMerchantProceedAsync(fakeMerchant);
                }

                // Event room exists but not FakeMerchant
                return new
                {
                    ok = false,
                    error = "UNSUPPORTED_EVENT",
                    message = "Current event is not FakeMerchant and does not support proceed"
                };
            }

            // --- Try Rest Site ---

            var restSiteRoom = NRestSiteRoom.Instance;
            if (restSiteRoom != null && restSiteRoom.IsInsideTree())
            {
                Logger.Info("Detected rest site context");
                return await ExecuteRestSiteProceedAsync(restSiteRoom);
            }

            // --- No valid context found ---

            return new
            {
                ok = false,
                error = "NO_PROCEED_AVAILABLE",
                message = "Not on reward screen, FakeMerchant event, or rest site"
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to proceed: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Proceeds from the reward screen.
    /// </summary>
    private static object ExecuteRewardProceed(NRewardsScreen screen)
    {
        var proceedButton = UiUtils.FindFirst<NProceedButton>(screen);
        if (proceedButton == null)
        {
            Logger.Warning("NProceedButton not found in NRewardsScreen");
            return new
            {
                ok = false,
                error = "PROCEED_BUTTON_NOT_FOUND",
                message = "Proceed button not found on reward screen"
            };
        }

        if (!proceedButton.IsEnabled)
        {
            Logger.Warning("NProceedButton is not enabled");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_ENABLED",
                message = "Proceed button is not enabled"
            };
        }

        Logger.Info("Clicking proceed button on reward screen");
        proceedButton.ForceClick();

        // ForceClick triggers OnProceedButtonPressed which handles the full
        // transition (ExitCurrentRoom, open map).

        return new
        {
            ok = true,
            data = new
            {
                context = "reward_screen",
                action = "PROCEED"
            }
        };
    }

    /// <summary>
    ///     Proceeds from the FakeMerchant event.
    /// </summary>
    private static async Task<object> ExecuteFakeMerchantProceedAsync(NFakeMerchant fakeMerchant)
    {
        var proceedButton = fakeMerchant.GetNodeOrNull<NProceedButton>("%ProceedButton");
        if (proceedButton == null)
        {
            Logger.Warning("NProceedButton not found in NFakeMerchant");
            return new
            {
                ok = false,
                error = "PROCEED_BUTTON_NOT_FOUND",
                message = "Proceed button not found on FakeMerchant event"
            };
        }

        if (!proceedButton.Visible)
        {
            Logger.Warning("NProceedButton is not visible");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_VISIBLE",
                message = "Proceed button is not visible"
            };
        }

        if (!proceedButton.IsEnabled)
        {
            Logger.Warning("NProceedButton is not enabled");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_ENABLED",
                message = "Proceed button is not enabled (shop may be open)"
            };
        }

        Logger.Info("Clicking proceed button on FakeMerchant event");
        proceedButton.ForceClick();

        // Wait for the map to open (async — does not block the Godot main thread)
        var proceeded = await WaitForMapOpenAsync();

        return new
        {
            ok = true,
            data = new
            {
                context = "fake_merchant",
                proceeded,
                action = "PROCEED"
            }
        };
    }

    /// <summary>
    ///     Proceeds from the rest site after an option has been chosen.
    ///     Finds the <see cref="NProceedButton" /> in <see cref="NRestSiteRoom" />,
    ///     validates it is enabled, clicks it, and waits for the map to open.
    /// </summary>
    private static async Task<object> ExecuteRestSiteProceedAsync(NRestSiteRoom restSiteRoom)
    {
        var proceedButton = restSiteRoom.ProceedButton;
        if (proceedButton == null)
        {
            Logger.Warning("NProceedButton not found in NRestSiteRoom");
            return new
            {
                ok = false,
                error = "PROCEED_BUTTON_NOT_FOUND",
                message = "Proceed button not found on rest site"
            };
        }

        if (!proceedButton.IsEnabled)
        {
            Logger.Warning("NProceedButton is not enabled (no option chosen yet?)");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_ENABLED",
                message = "Proceed button is not enabled (choose a rest site option first)"
            };
        }

        Logger.Info("Clicking proceed button on rest site");
        proceedButton.ForceClick();

        var proceeded = await WaitForMapOpenAsync();

        return new
        {
            ok = true,
            data = new
            {
                context = "rest_site",
                proceeded,
                action = "PROCEED"
            }
        };
    }

    /// <summary>
    ///     Waits for the map to open after proceeding.
    ///     Uses <see cref="ActionUtils.PollUntilAsync" /> to avoid blocking the Godot main thread.
    /// </summary>
    private static async Task<bool> WaitForMapOpenAsync()
    {
        var opened = await ActionUtils.PollUntilAsync(
            () => NMapScreen.Instance is { IsOpen: true },
            ActionUtils.UiTimeoutMs);

        if (!opened)
            Logger.Warning("Timed out waiting for map to open after proceed");

        return opened;
    }
}