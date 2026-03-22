using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles leaving the reward screen and proceeding to the map.
///     Mimics the AutoSlayer / STS2MCP approach: finds the <see cref="NProceedButton" /> in the
///     <see cref="NRewardsScreen" /> and calls <see cref="NClickableControl.ForceClick" />,
///     which triggers the full <c>OnProceedButtonPressed</c> UI flow.
/// </summary>
public static class ProceedHandler
{
    private static readonly ModLogger Logger = new("ProceedAction");

    /// <summary>
    ///     Leaves the reward screen and proceeds to the map.
    ///     Remaining unclaimed rewards are automatically skipped by the game's
    ///     <see cref="NRewardsScreen.AfterOverlayClosed" /> handler.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static object Execute()
    {
        try
        {
            // --- Validation ---

            var screen = FindRewardsScreen();
            if (screen == null)
                return new { ok = false, error = "NOT_ON_REWARD_SCREEN", message = "Reward screen is not active" };

            // --- Find and click the proceed button ---

            var proceedButton = FindFirst<NProceedButton>(screen);
            if (proceedButton == null)
            {
                Logger.Warning("NProceedButton not found in NRewardsScreen");
                return new
                {
                    ok = false, error = "INTERNAL_ERROR",
                    message = "Proceed button not found on reward screen"
                };
            }

            if (!proceedButton.IsEnabled)
            {
                Logger.Warning("NProceedButton is not enabled");
                return new
                {
                    ok = false, error = "NOT_ALLOWED",
                    message = "Proceed button is not enabled"
                };
            }

            Logger.Info("Clicking proceed button via ForceClick");
            proceedButton.ForceClick();

            // ForceClick triggers OnProceedButtonPressed which handles the full
            // transition (ExitCurrentRoom, open map). The NRewardsScreen overlay
            // may linger in the stack, but DetectScreen() checks NMapScreen.IsOpen
            // first, so subsequent state queries will correctly report "MAP".

            return new
            {
                ok = true,
                data = new
                {
                    action = "PROCEED"
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to proceed from reward screen: {ex.Message}");
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

    /// <summary>
    ///     Recursively searches for the first node of type <typeparamref name="T" />
    ///     within the given <paramref name="start" /> node's subtree.
    ///     Mirrors <c>UiHelper.FindFirst&lt;T&gt;</c> from the AutoSlayer.
    /// </summary>
    private static T? FindFirst<T>(Node start) where T : Node
    {
        if (!GodotObject.IsInstanceValid(start))
            return null;

        if (start is T result)
            return result;

        foreach (var child in start.GetChildren())
        {
            var found = FindFirst<T>(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
