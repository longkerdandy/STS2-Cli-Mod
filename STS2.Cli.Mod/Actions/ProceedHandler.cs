using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>proceed</c> CLI command.
///     Proceeds to the map from various screens.
///     Supports:
///     - Reward screen (NRewardsScreen with NProceedButton)
///     - Event room (any event with IsFinished, via NEventRoom.Proceed())
///     - FakeMerchant custom event (NFakeMerchant with NProceedButton)
///     - Rest site (NRestSiteRoom with NProceedButton, after choosing an option)
///     - Treasure room (NTreasureRoom with NProceedButton, after picking/skipping relic)
///     - Merchant room (NMerchantRoom with NProceedButton, shop proceed to map)
///     Mimics the AutoSlayer / STS2MCP approach: finds the <see cref="NProceedButton" /> and calls
///     <see cref="NClickableControl.ForceClick" />, which triggers the full UI flow.
///     For events, calls <see cref="NEventRoom.Proceed" /> directly (events use
///     <c>NEventOptionButton</c> with <c>IsProceed=true</c> instead of <c>NProceedButton</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI command:</b> <c>sts2 proceed</c>
///     </para>
///     <para>
///         <b>Scene:</b> Reward screen, finished event room, FakeMerchant event, rest site (after option chosen),
///         treasure room (after relic picked/skipped), or merchant room (shop).
///     </para>
/// </remarks>
public static class ProceedHandler
{
    private static readonly ModLogger Logger = new("ProceedHandler");

    /// <summary>
    ///     Executes the proceed action based on detected context.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync()
    {
        Logger.Info("Requested to proceed");

        try
        {
            // --- Try Reward Screen first ---

            var rewardScreen = UiUtils.FindScreenInOverlay<NRewardsScreen>();
            if (rewardScreen != null)
            {
                Logger.Info("Detected reward screen context");
                return ExecuteRewardProceed(rewardScreen);
            }

            // --- Try FakeMerchant Event ---
            // FakeMerchant is a special custom event node with its own NProceedButton,
            // separate from the standard event option system. Check it first.

            var eventRoom = NEventRoom.Instance;
            if (eventRoom != null && eventRoom.IsInsideTree())
            {
                var fakeMerchant = UiUtils.FindFirst<NFakeMerchant>(eventRoom);
                if (fakeMerchant != null)
                {
                    Logger.Info("Detected FakeMerchant event context");
                    return await ExecuteFakeMerchantProceedAsync(fakeMerchant);
                }

                // --- Try standard/Ancient event proceed ---
                // All events (including Neow) use NEventOptionButton with IsProceed=true
                // instead of NProceedButton. When IsFinished, call NEventRoom.Proceed() directly.
                return await ExecuteEventProceedAsync(eventRoom);
            }

            // --- Try Rest Site ---

            var restSiteRoom = NRestSiteRoom.Instance;
            if (restSiteRoom != null && restSiteRoom.IsInsideTree())
            {
                Logger.Info("Detected rest site context");
                return await ExecuteRestSiteProceedAsync(restSiteRoom);
            }

            // --- Try Treasure Room ---

            var treasureRoom = NRun.Instance?.TreasureRoom;
            if (treasureRoom != null && treasureRoom.IsInsideTree())
            {
                Logger.Info("Detected treasure room context");
                return await ExecuteTreasureRoomProceedAsync(treasureRoom);
            }

            // --- Try Merchant Room ---

            var merchantRoom = NRun.Instance?.MerchantRoom;
            if (merchantRoom != null && merchantRoom.IsInsideTree())
            {
                Logger.Info("Detected merchant room context");
                return await ExecuteMerchantRoomProceedAsync(merchantRoom);
            }

            // --- No valid context found ---

            return new
            {
                ok = false,
                error = "NO_PROCEED_AVAILABLE",
                message = "Not on reward screen, event room, rest site, treasure room, or merchant room"
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
            // Check if skipping is disallowed (mandatory rewards like NeowsBones)
            var skipDisallowed = UiUtils.GetPrivateFieldValue<bool>(screen, "_skipDisallowed") ?? false;
            var message = skipDisallowed
                ? "All rewards must be claimed before proceeding (skipping is not allowed)"
                : "Proceed button is not enabled";

            Logger.Warning($"NProceedButton is not enabled (skipDisallowed={skipDisallowed})");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_ENABLED",
                message
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
    ///     Proceeds from a standard or Ancient event (e.g., Neow) after it has finished.
    ///     Events do not use <see cref="NProceedButton" />; instead, <see cref="NEventRoom.SetOptions" />
    ///     injects a synthetic <c>EventOption</c> with <c>IsProceed=true</c> when <c>IsFinished</c>.
    ///     We call <see cref="NEventRoom.Proceed" /> directly to trigger the map transition.
    /// </summary>
    private static async Task<object> ExecuteEventProceedAsync(NEventRoom eventRoom)
    {
        var eventModel = EventUtils.GetEventModel(eventRoom);
        if (eventModel == null)
            return new
            {
                ok = false,
                error = "INTERNAL_ERROR",
                message = "Failed to access event model"
            };

        if (!eventModel.IsFinished)
        {
            var eventId = eventModel.Id.Entry;
            Logger.Warning($"Event '{eventId}' is not finished, cannot proceed");
            return new
            {
                ok = false,
                error = "EVENT_NOT_FINISHED",
                message = $"Event '{eventId}' is not finished. Use 'choose_event' to select an option first."
            };
        }

        Logger.Info("Event is finished, calling NEventRoom.Proceed()");
        await NEventRoom.Proceed();

        var proceeded = await WaitForMapOpenAsync();

        return new
        {
            ok = true,
            data = new
            {
                context = "event",
                proceeded,
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
    ///     Proceeds from the treasure room after picking or skipping the relic.
    ///     Handles both the "Proceed" state (relic picked) and the "Skip" state
    ///     (skip button visible, calls <c>SkipRelicLocally</c> then proceeds).
    ///     Mirrors the game's <c>OnProceedButtonPressed</c> logic.
    /// </summary>
    private static async Task<object> ExecuteTreasureRoomProceedAsync(NTreasureRoom treasureRoom)
    {
        var proceedButton = treasureRoom.ProceedButton;

        if (!proceedButton.IsEnabled)
        {
            Logger.Warning("NProceedButton is not enabled on treasure room");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_ENABLED",
                message = "Proceed button is not enabled (open chest and pick/skip relic first)"
            };
        }

        // Mirrors OnProceedButtonPressed: if IsSkip, call SkipRelicLocally first
        var isSkip = proceedButton.IsSkip;
        if (isSkip)
        {
            Logger.Info("Skip button detected — skipping relic and proceeding");
            RunManager.Instance.TreasureRoomRelicSynchronizer.SkipRelicLocally();
            NMapScreen.Instance?.SetTravelEnabled(true);
        }
        else
        {
            Logger.Info("Clicking proceed button on treasure room");
        }

        // ForceClick triggers OnProceedButtonPressed which calls
        // ProceedFromTerminalRewardsScreen (same for both skip and proceed paths).
        proceedButton.ForceClick();

        var proceeded = await WaitForMapOpenAsync();

        return new
        {
            ok = true,
            data = new
            {
                context = "treasure_room",
                skipped_relic = isSkip,
                proceeded,
                action = "PROCEED"
            }
        };
    }

    /// <summary>
    ///     Proceeds from the merchant room (shop).
    ///     The proceed button in <see cref="NMerchantRoom" /> triggers <c>HideScreen</c>
    ///     which calls <c>NMapScreen.Instance.Open()</c>.
    ///     The proceed button is enabled by default on room entry, disabled while the
    ///     inventory is open, and re-enabled when the inventory closes.
    /// </summary>
    private static async Task<object> ExecuteMerchantRoomProceedAsync(NMerchantRoom merchantRoom)
    {
        var proceedButton = merchantRoom.ProceedButton;

        if (!proceedButton.IsEnabled)
        {
            Logger.Warning("NProceedButton is not enabled on merchant room (shop inventory may be open)");
            return new
            {
                ok = false,
                error = "PROCEED_NOT_ENABLED",
                message = "Proceed button is not enabled (close shop inventory first)"
            };
        }

        Logger.Info("Clicking proceed button on merchant room");
        proceedButton.ForceClick();

        var proceeded = await WaitForMapOpenAsync();

        return new
        {
            ok = true,
            data = new
            {
                context = "merchant_room",
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