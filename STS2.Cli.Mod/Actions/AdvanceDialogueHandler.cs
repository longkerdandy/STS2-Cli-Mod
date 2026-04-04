using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>advance_dialogue</c> CLI command.
///     Advances dialogue in Ancient events via ForceClick on the dialogue hitbox.
///     Can advance a single line or auto-advance until options appear.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 advance_dialogue [--auto]</c></para>
///     <para><b>Scene:</b> Ancient event room with active dialogue (e.g., Neow, Act bosses with dialogue).</para>
/// </remarks>
public static class AdvanceDialogueHandler
{
    /// <summary>
    ///     Maximum number of dialogue lines to auto-advance (safety limit).
    /// </summary>
    private const int MaxAutoAdvanceLines = 50;

    private static readonly ModLogger Logger = new("AdvanceDialogueHandler");

    /// <summary>
    ///     Executes the advance_dialogue command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    public static async Task<object> ExecuteAsync(Request request)
    {
        // args[0] = 1 for auto mode, 0 or not present for a single advance
        var auto = request.Args is { Length: > 0 } && request.Args[0] == 1;
        Logger.Info($"Requested to advance dialogue (auto={auto})");
        
        try
        {
            // --- Guard: Check event room ---
            var eventRoom = NEventRoom.Instance;
            if (eventRoom == null || !eventRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_EVENT", message = "Not currently in an event" };

            // --- Guard: Check layout is Ancient ---
            if (eventRoom.Layout is not NAncientEventLayout ancientLayout)
                return new
                {
                    ok = false, error = "NOT_ANCIENT_EVENT", message = "Current event is not an Ancient event"
                };

            // --- Guard: Check we're in dialogue phase ---
            if (IsDialogueFinished(ancientLayout))
                return new
                {
                    ok = false, error = "NOT_IN_DIALOGUE",
                    message = "Dialogue has already finished, options are available"
                };

            // --- Find the dialogue hitbox ---
            var hitbox = FindDialogueHitbox(ancientLayout);
            if (hitbox == null)
                return new
                {
                    ok = false, error = "DIALOGUE_HITBOX_NOT_FOUND", message = "Could not find dialogue hitbox"
                };

            if (auto)
                // Auto-advance all dialogue lines
                return await AutoAdvanceDialogue(ancientLayout, hitbox);

            // Advance a single line
            return await AdvanceSingleLine(ancientLayout, hitbox);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to advance dialogue: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Advances a single dialogue line.
    /// </summary>
    private static async Task<object> AdvanceSingleLine(NAncientEventLayout ancientLayout, NClickableControl hitbox)
    {
        var initialLine = GetCurrentDialogueLine(ancientLayout);

        // ForceClick the hitbox
        Logger.Info($"Advancing dialogue from line {initialLine}");
        hitbox.ForceClick();

        // Wait for animation
        await Task.Delay(ActionUtils.PostClickDelayMs);

        // Wait for the line to change or dialogue to finish
        var advanced = await WaitForDialogueAdvance(ancientLayout, initialLine);

        if (!advanced)
        {
            Logger.Warning("Timed out waiting for dialogue to advance");
            return new
            {
                ok = false,
                error = "EVENT_TIMEOUT",
                message = "Dialogue did not advance within timeout period"
            };
        }

        var currentLine = GetCurrentDialogueLine(ancientLayout);
        var isFinished = IsDialogueFinished(ancientLayout);
        var totalLines = GetTotalDialogueLines(ancientLayout);

        Logger.Info($"Advanced to line {currentLine}, finished={isFinished}");

        return new
        {
            ok = true,
            data = new
            {
                advanced = true,
                is_dialogue_finished = isFinished,
                current_line = currentLine,
                total_lines = totalLines,
                previous_line = initialLine
            }
        };
    }

    /// <summary>
    ///     Auto-advances all dialogue lines until options appear.
    /// </summary>
    private static async Task<object> AutoAdvanceDialogue(NAncientEventLayout ancientLayout, NClickableControl hitbox)
    {
        var linesAdvanced = 0;

        Logger.Info("Auto-advancing Ancient event dialogue");

        while (!IsDialogueFinished(ancientLayout) && linesAdvanced < MaxAutoAdvanceLines)
        {
            var currentLine = GetCurrentDialogueLine(ancientLayout);

            // ForceClick the hitbox
            hitbox.ForceClick();

            // Wait for animation
            await Task.Delay(ActionUtils.PostClickDelayMs);

            // Wait for the line to change
            var advanced = await WaitForDialogueAdvance(ancientLayout, currentLine);

            if (!advanced)
            {
                Logger.Warning($"Stopped auto-advance at line {currentLine} (timeout or stuck)");
                break;
            }

            linesAdvanced++;

            // Check if the screen changed (e.g., combat started)
            if (NOverlayStack.Instance?.Peek() is not null)
            {
                Logger.Info("Overlay detected during auto-advance, stopping");
                break;
            }

            // Check if the map opened (event ended)
            if (NMapScreen.Instance is { IsOpen: true })
            {
                Logger.Info("Map opened during auto-advance, event ended");
                break;
            }
        }

        var finalLine = GetCurrentDialogueLine(ancientLayout);
        var isFinished = IsDialogueFinished(ancientLayout);
        var totalLines = GetTotalDialogueLines(ancientLayout);

        Logger.Info($"Auto-advance complete: {linesAdvanced} lines advanced, finished={isFinished}");

        // Build updated event state if dialogue finished
        object? eventState = null;
        if (isFinished) eventState = EventStateBuilder.Build();

        return new
        {
            ok = true,
            data = new
            {
                advanced = true,
                is_dialogue_finished = isFinished,
                lines_advanced = linesAdvanced,
                current_line = finalLine,
                total_lines = totalLines,
                event_state = eventState
            }
        };
    }

    /// <summary>
    ///     Finds the dialogue hitbox in the Ancient layout.
    /// </summary>
    private static NClickableControl? FindDialogueHitbox(NAncientEventLayout ancientLayout)
    {
        try
        {
            // Try to find via the node path first
            var hitbox = ancientLayout.GetNodeOrNull<NClickableControl>("%DialogueHitbox");
            if (hitbox != null)
                return hitbox;

            // Fallback: search children
            foreach (var child in ancientLayout.GetChildren())
                if (child is NClickableControl clickable && child.Name.ToString().Contains("Hitbox"))
                    return clickable;

            Logger.Warning("Could not find dialogue hitbox in Ancient layout");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Error finding dialogue hitbox: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Checks if dialogue has finished (options are available).
    ///     Uses the private <c>IsDialogueOnLastLine</c> property from <see cref="NAncientEventLayout" />.
    /// </summary>
    private static bool IsDialogueFinished(NAncientEventLayout ancientLayout)
    {
        try
        {
            var property = typeof(NAncientEventLayout).GetProperty("IsDialogueOnLastLine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(ancientLayout) as bool? ?? true;
        }
        catch
        {
            return true; // Assume finished if we can't check
        }
    }

    /// <summary>
    ///     Gets the current dialogue line index.
    /// </summary>
    private static int GetCurrentDialogueLine(NAncientEventLayout ancientLayout)
    {
        try
        {
            var field = typeof(NAncientEventLayout).GetField("_currentDialogueLine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(ancientLayout) as int? ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    ///     Gets the total number of dialogue lines.
    /// </summary>
    private static int GetTotalDialogueLines(NAncientEventLayout ancientLayout)
    {
        try
        {
            var field = typeof(NAncientEventLayout).GetField("_dialogue",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var dialogue = field?.GetValue(ancientLayout) as IList<object>;
            return dialogue?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    ///     Waits for the dialogue line to change or dialogue to finish.
    /// </summary>
    private static async Task<bool> WaitForDialogueAdvance(NAncientEventLayout ancientLayout, int previousLine)
    {
        return await ActionUtils.PollUntilAsync(() =>
                GetCurrentDialogueLine(ancientLayout) != previousLine ||
                IsDialogueFinished(ancientLayout) ||
                NOverlayStack.Instance?.Peek() is not null ||
                NMapScreen.Instance is { IsOpen: true },
            ActionUtils.ShortTimeoutMs);
    }
}