using Godot;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles choosing an event option by index via ForceClick.
///     Returns updated event state after the option resolves.
/// </summary>
public static class ChooseEventHandler
{
    private static readonly ModLogger Logger = new("ChooseEventHandler");

    /// <summary>
    ///     Delay after ForceClick before starting to poll for state changes.
    /// </summary>
    private const int PostClickDelayMs = 200;

    /// <summary>
    ///     Polling interval when waiting for event state changes.
    /// </summary>
    private const int PollIntervalMs = 100;

    /// <summary>
    ///     Maximum time to wait for event state to change after clicking an option.
    /// </summary>
    private const int MaxWaitTimeMs = 5000;

    /// <summary>
    ///     Executes the choose_event command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    /// <param name="optionIndex">0-based index of the option to choose.</param>
    public static async Task<object> ExecuteAsync(int optionIndex)
    {
        try
        {
            // --- Guard: Check event room ---
            var eventRoom = NEventRoom.Instance;
            if (eventRoom == null || !eventRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_EVENT", message = "Not currently in an event" };

            // --- Guard: Check layout exists ---
            var layout = eventRoom.Layout;
            if (layout == null)
                return new { ok = false, error = "NO_EVENT_LAYOUT", message = "Event layout not found" };

            // --- Guard: Get event model and validate option index ---
            var eventModel = GetEventModel(eventRoom);
            if (eventModel == null)
                return new { ok = false, error = "INTERNAL_ERROR", message = "Failed to access event model" };

            // --- Handle finished event (proceed to map) ---
            if (eventModel.IsFinished)
            {
                if (optionIndex != 0)
                    return new
                    {
                        ok = false,
                        error = "INVALID_OPTION_INDEX",
                        message = "Event is finished, only option index 0 (proceed) is available"
                    };

                Logger.Info("Event is finished, calling NEventRoom.Proceed()");
                await NEventRoom.Proceed();

                // Wait for map to open
                var proceeded = await WaitForProceed(eventRoom);
                if (!proceeded)
                    Logger.Warning("Timed out waiting for proceed transition");

                return new
                {
                    ok = true,
                    data = new
                    {
                        option_index = 0,
                        is_proceed = true,
                        proceeded = proceeded
                    }
                };
            }

            var currentOptions = eventModel.CurrentOptions;
            if (optionIndex < 0 || optionIndex >= currentOptions.Count)
                return new
                {
                    ok = false,
                    error = "INVALID_OPTION_INDEX",
                    message = $"Option index {optionIndex} out of range (event has {currentOptions.Count} options)"
                };

            var selectedOption = currentOptions[optionIndex];

            // --- Guard: Check option is not locked ---
            if (selectedOption.IsLocked)
                return new
                {
                    ok = false,
                    error = "OPTION_LOCKED",
                    message = $"Option at index {optionIndex} is locked and cannot be selected"
                };

            // --- Find the option button ---
            var optionButtons = layout.OptionButtons.ToList();
            if (optionIndex >= optionButtons.Count)
                return new
                {
                    ok = false,
                    error = "OPTION_BUTTON_NOT_FOUND",
                    message = $"Option button at index {optionIndex} not found in UI"
                };

            var targetButton = optionButtons[optionIndex];

            // --- Capture state before click for comparison ---
            var preClickSnapshot = new EventStateSnapshot(eventModel);

            // --- ForceClick the button ---
            Logger.Info($"ForceClick option at index {optionIndex}: '{selectedOption.Title}'");
            targetButton.ForceClick();

            // --- Wait a bit for the click to register ---
            await Task.Delay(PostClickDelayMs);

            // --- Post-click handling based on option type ---
            if (selectedOption.IsProceed)
            {
                // Wait for map to open or event room to leave tree
                var proceeded = await WaitForProceed(eventRoom);
                if (!proceeded)
                    Logger.Warning("Timed out waiting for proceed transition");

                return new
                {
                    ok = true,
                    data = new
                    {
                        option_index = optionIndex,
                        is_proceed = true,
                        proceeded = proceeded
                    }
                };
            }
            else
            {
                // Wait for event state to change (new page)
                var stateChanged = await WaitForEventStateChange(eventRoom, preClickSnapshot);
                if (!stateChanged)
                {
                    Logger.Warning("Timed out waiting for event state change");
                    return new
                    {
                        ok = false,
                        error = "EVENT_TIMEOUT",
                        message = "Event state did not change within timeout period"
                    };
                }

                // Build and return updated event state
                var updatedEventState = EventStateBuilder.Build();

                return new
                {
                    ok = true,
                    data = new
                    {
                        option_index = optionIndex,
                        is_proceed = false,
                        event_state = updatedEventState
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to choose event option: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Gets the EventModel from NEventRoom using reflection.
    /// </summary>
    private static EventModel? GetEventModel(NEventRoom eventRoom)
    {
        try
        {
            var field = typeof(NEventRoom).GetField("_event",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(eventRoom) as EventModel;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get EventModel: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Waits for the proceed transition (map opens or event room leaves tree).
    /// </summary>
    private static async Task<bool> WaitForProceed(NEventRoom originalEventRoom)
    {
        var elapsed = 0;
        while (elapsed < MaxWaitTimeMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsed += PollIntervalMs;

            // Check if map opened
            if (NMapScreen.Instance is { IsOpen: true })
                return true;

            // Check if event room left tree
            if (!GodotObject.IsInstanceValid(originalEventRoom) || !originalEventRoom.IsInsideTree())
                return true;

            // Check if a new overlay appeared (combat event)
            if (NOverlayStack.Instance?.Peek() is not null)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Waits for event state to change (new options or finished).
    /// </summary>
    private static async Task<bool> WaitForEventStateChange(NEventRoom eventRoom, EventStateSnapshot snapshot)
    {
        var elapsed = 0;
        while (elapsed < MaxWaitTimeMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsed += PollIntervalMs;

            // Check if screen changed (combat started)
            if (!eventRoom.IsInsideTree())
                return true;

            var overlay = NOverlayStack.Instance?.Peek();
            if (overlay is not null)
                return true;

            // Check if map opened (event finished)
            if (NMapScreen.Instance is { IsOpen: true })
                return true;

            // Check event state change
            var eventModel = GetEventModel(eventRoom);
            if (eventModel == null)
                return true;

            if (HasEventStateChanged(eventModel, snapshot))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if event state has changed compared to the snapshot.
    /// </summary>
    private static bool HasEventStateChanged(EventModel eventModel, EventStateSnapshot snapshot)
    {
        // Check if option count changed
        if (eventModel.CurrentOptions.Count != snapshot.OptionCount)
            return true;

        // Check if first option title changed
        if (eventModel.CurrentOptions.Count > 0 && snapshot.FirstOptionTitle != null)
        {
            var currentFirstTitle = eventModel.CurrentOptions[0].Title.GetFormattedText();
            if (currentFirstTitle != snapshot.FirstOptionTitle)
                return true;
        }

        // Check if event finished
        if (eventModel.IsFinished != snapshot.IsFinished)
            return true;

        return false;
    }

    /// <summary>
    ///     Snapshot of event state for comparison.
    /// </summary>
    private class EventStateSnapshot
    {
        public int OptionCount { get; }
        public string? FirstOptionTitle { get; }
        public bool IsFinished { get; }

        public EventStateSnapshot(EventModel eventModel)
        {
            OptionCount = eventModel.CurrentOptions.Count;
            try
            {
                FirstOptionTitle = OptionCount > 0
                    ? eventModel.CurrentOptions[0].Title.GetFormattedText()
                    : null;
            }
            catch
            {
                FirstOptionTitle = null;
            }
            IsFinished = eventModel.IsFinished;
        }
    }
}
