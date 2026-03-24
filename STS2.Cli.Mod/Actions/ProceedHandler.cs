using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles proceeding to the map from various screens.
///     Supports:
///     - Reward screen (NRewardsScreen with NProceedButton)
///     - FakeMerchant custom event (NFakeMerchant with NProceedButton)
///     Mimics the AutoSlayer / STS2MCP approach: finds the <see cref="NProceedButton" /> and calls
///     <see cref="NClickableControl.ForceClick" />, which triggers the full UI flow.
/// </summary>
public static class ProceedHandler
{
    private static readonly ModLogger Logger = new("ProceedHandler");

    /// <summary>
    ///     Handles the proceed request.
    ///     Automatically detects the current context (reward screen or FakeMerchant event).
    /// </summary>
    public static object HandleRequest(Request request)
    {
        Logger.Info("Requested to proceed");
        return Execute();
    }

    /// <summary>
    ///     Executes the proceed action based on detected context.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    private static object Execute()
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
                var fakeMerchant = UiHelper.FindFirst<NFakeMerchant>(eventRoom);
                if (fakeMerchant != null)
                {
                    Logger.Info("Detected FakeMerchant event context");
                    return ExecuteFakeMerchantProceed(fakeMerchant);
                }

                // Event room exists but not FakeMerchant
                return new
                {
                    ok = false,
                    error = "UNSUPPORTED_EVENT",
                    message = "Current event is not FakeMerchant and does not support proceed"
                };
            }

            // --- No valid context found ---

            return new
            {
                ok = false,
                error = "NO_PROCEED_AVAILABLE",
                message = "Not on reward screen or FakeMerchant event"
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
        var proceedButton = RewardUiHelper.FindFirst<NProceedButton>(screen);
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
    private static object ExecuteFakeMerchantProceed(NFakeMerchant fakeMerchant)
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

        // Wait for map to open
        var proceeded = WaitForMapOpen();

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
    ///     Waits for the map to open after proceeding.
    /// </summary>
    private static bool WaitForMapOpen()
    {
        const int timeoutMs = 5000;
        const int pollIntervalMs = 100;
        var elapsed = 0;

        while (elapsed < timeoutMs)
        {
            Thread.Sleep(pollIntervalMs);
            elapsed += pollIntervalMs;

            if (NMapScreen.Instance is { IsOpen: true })
                return true;
        }

        Logger.Warning("Timed out waiting for map to open after proceed");
        return false;
    }
}
