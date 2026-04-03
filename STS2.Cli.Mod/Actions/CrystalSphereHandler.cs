using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>crystal_set_tool</c>, <c>crystal_click_cell</c>, and <c>crystal_proceed</c> commands.
///     Manages the Crystal Sphere mini-game overlay where the player uses divination tools
///     to clear fog from a grid and reveal hidden items.
/// </summary>
/// <remarks>
///     <para><b>CLI commands:</b></para>
///     <list type="bullet">
///         <item><c>sts2 crystal_set_tool &lt;tool&gt;</c> — switch divination tool ("big" or "small")</item>
///         <item><c>sts2 crystal_click_cell &lt;x&gt; &lt;y&gt;</c> — click a hidden cell to clear fog</item>
///         <item><c>sts2 crystal_proceed</c> — leave the mini-game after divinations are exhausted</item>
///     </list>
/// </remarks>
public static class CrystalSphereHandler
{
    private static readonly ModLogger Logger = new("CrystalSphereHandler");

    /// <summary>
    ///     Cached reflection accessor for the private <c>_entity</c> field on
    ///     <see cref="NCrystalSphereScreen" />.
    /// </summary>
    private static readonly FieldInfo? EntityField =
        typeof(NCrystalSphereScreen).GetField("_entity",
            BindingFlags.NonPublic | BindingFlags.Instance);

    // ── Public entry points ────────────────────────────────────────────

    /// <summary>
    ///     Handles the <c>crystal_set_tool</c> request.
    ///     Accepts a tool name via <see cref="Request.Id" /> — "big" or "small".
    /// </summary>
    public static async Task<object> HandleSetToolAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Tool type required: 'big' or 'small'" };

        var tool = request.Id.ToLowerInvariant();
        Logger.Info($"Requested to set Crystal Sphere tool to '{tool}'");

        return await ExecuteSetToolAsync(tool);
    }

    /// <summary>
    ///     Handles the <c>crystal_click_cell</c> request.
    ///     Accepts x (args[0]) and y (args[1]) coordinates.
    /// </summary>
    public static async Task<object> HandleClickCellAsync(Request request)
    {
        if (request.Args == null || request.Args.Length < 2)
            return new
            {
                ok = false, error = "MISSING_ARGUMENT",
                message = "Cell coordinates required: x (args[0]) and y (args[1])"
            };

        var x = request.Args[0];
        var y = request.Args[1];

        Logger.Info($"Requested to click Crystal Sphere cell at ({x}, {y})");

        return await ExecuteClickCellAsync(x, y);
    }

    /// <summary>
    ///     Handles the <c>crystal_proceed</c> request.
    ///     Clicks the proceed button to leave the mini-game.
    /// </summary>
    public static async Task<object> HandleProceedAsync(Request request)
    {
        Logger.Info("Requested to proceed from Crystal Sphere mini-game");
        return await ExecuteProceedAsync();
    }

    // ── Private execution methods ─────────────────────────────────────

    /// <summary>
    ///     Switches the divination tool by clicking the corresponding button.
    /// </summary>
    private static async Task<object> ExecuteSetToolAsync(string? tool)
    {
        try
        {
            // --- Guard: Validate tool name ---
            if (tool is not ("big" or "small"))
                return new
                {
                    ok = false, error = "INVALID_TOOL",
                    message = $"Invalid tool '{tool}'. Must be 'big' or 'small'."
                };

            // --- Guard: Check Crystal Sphere screen ---
            var screen = CommonUiUtils.FindScreenInOverlay<NCrystalSphereScreen>();
            if (screen == null)
                return new
                {
                    ok = false, error = "NOT_IN_CRYSTAL_SPHERE",
                    message = "Not in Crystal Sphere mini-game. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Check tool buttons are visible (hidden after minigame finishes) ---
            var bigButton = screen.GetNodeOrNull<NDivinationButton>("%BigDivinationButton");
            var smallButton = screen.GetNodeOrNull<NDivinationButton>("%SmallDivinationButton");
            var targetButton = tool == "big" ? bigButton : smallButton;

            if (targetButton is not { Visible: true })
                return new
                {
                    ok = false, error = "TOOL_NOT_AVAILABLE",
                    message = $"The '{tool}' divination tool button is not available (minigame may have finished)."
                };

            // --- Guard: Check if tool is already active ---
            var outline = targetButton.GetNodeOrNull<Control>("%Outline");
            if (outline?.Visible == true)
                return new
                {
                    ok = false, error = "TOOL_ALREADY_ACTIVE",
                    message = $"The '{tool}' divination tool is already active."
                };

            // --- Click the tool button ---
            Logger.Info($"Clicking {tool} divination button");
            targetButton.ForceClick();

            // --- Wait for outline toggle animation ---
            await Task.Delay(ActionUtils.PostClickDelayMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectScreen();
            return new
            {
                ok = true,
                data = new
                {
                    action = "CRYSTAL_SET_TOOL",
                    tool,
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to set Crystal Sphere tool: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Clicks a grid cell to clear fog. Uses <c>EmitSignal(Released)</c> on the cell node
    ///     (proven pattern from AutoSlay's <c>CrystalSphereScreenHandler</c>).
    /// </summary>
    private static async Task<object> ExecuteClickCellAsync(int x, int y)
    {
        try
        {
            // --- Guard: Check Crystal Sphere screen ---
            var screen = CommonUiUtils.FindScreenInOverlay<NCrystalSphereScreen>();
            if (screen == null)
                return new
                {
                    ok = false, error = "NOT_IN_CRYSTAL_SPHERE",
                    message = "Not in Crystal Sphere mini-game. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Check minigame not finished ---
            var entity = GetEntity(screen);
            if (entity is { IsFinished: true })
                return new
                {
                    ok = false, error = "MINIGAME_FINISHED",
                    message = "The Crystal Sphere mini-game has finished. Use 'crystal_proceed' to leave."
                };

            // --- Guard: Find the cell node ---
            var cellContainer = screen.GetNodeOrNull<Control>("%Cells");
            if (cellContainer == null)
                return new
                {
                    ok = false, error = "CELL_NOT_FOUND",
                    message = "Could not find the cells container on the Crystal Sphere screen."
                };

            var cellNodes = CommonUiUtils.FindAll<NCrystalSphereCell>(cellContainer);
            NCrystalSphereCell? targetCell = null;
            foreach (var cellNode in cellNodes)
            {
                if (cellNode.Entity.X == x && cellNode.Entity.Y == y)
                {
                    targetCell = cellNode;
                    break;
                }
            }

            if (targetCell == null)
                return new
                {
                    ok = false, error = "CELL_NOT_FOUND",
                    message = $"No cell found at coordinates ({x}, {y})."
                };

            // --- Guard: Cell must be clickable (hidden and visible) ---
            if (!targetCell.Entity.IsHidden || !targetCell.Visible)
                return new
                {
                    ok = false, error = "CELL_NOT_CLICKABLE",
                    message =
                        $"Cell at ({x}, {y}) is not clickable (IsHidden={targetCell.Entity.IsHidden}, Visible={targetCell.Visible})."
                };

            // --- Click the cell via EmitSignal (proven pattern from AutoSlay) ---
            Logger.Info($"Clicking Crystal Sphere cell at ({x}, {y})");
            targetCell.EmitSignal(NClickableControl.SignalName.Released, targetCell);

            // --- Wait for fog animation and potential item reveal ---
            await Task.Delay(ActionUtils.PreviewAppearDelayMs);

            // Check if a child overlay appeared (reward from item reveal)
            var topOverlay = NOverlayStack.Instance?.Peek();
            if (topOverlay != null && topOverlay != (IOverlayScreen)screen)
            {
                // A reward screen appeared on top — wait a bit more for it to fully render
                await Task.Delay(ActionUtils.PostClickDelayMs);
            }

            // --- Return result ---
            var resultScreen = StateHandler.DetectScreen();
            var divLeft = entity?.DivinationCount ?? 0;

            return new
            {
                ok = true,
                data = new
                {
                    action = "CRYSTAL_CLICK_CELL",
                    x,
                    y,
                    divinations_left = divLeft,
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to click Crystal Sphere cell: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Clicks the proceed button to leave the mini-game and polls for screen removal.
    /// </summary>
    private static async Task<object> ExecuteProceedAsync()
    {
        try
        {
            // --- Guard: Check Crystal Sphere screen ---
            var screen = CommonUiUtils.FindScreenInOverlay<NCrystalSphereScreen>();
            if (screen == null)
                return new
                {
                    ok = false, error = "NOT_IN_CRYSTAL_SPHERE",
                    message = "Not in Crystal Sphere mini-game. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Check proceed button is enabled ---
            var proceedButton = screen.GetNodeOrNull<NProceedButton>("%ProceedButton");
            if (proceedButton is not { Visible: true, IsEnabled: true })
                return new
                {
                    ok = false, error = "CANNOT_PROCEED",
                    message =
                        "Proceed button is not enabled. The mini-game must finish (use all divinations) before proceeding."
                };

            // --- Click proceed ---
            Logger.Info("Clicking Crystal Sphere proceed button");
            proceedButton.ForceClick();

            // --- Poll for the overlay to be removed ---
            await ActionUtils.PollUntilAsync(() =>
            {
                var current = CommonUiUtils.FindScreenInOverlay<NCrystalSphereScreen>();
                return current == null;
            }, ActionUtils.UiTimeoutMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectScreen();
            Logger.Info($"After proceeding from Crystal Sphere, detected screen: {resultScreen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "CRYSTAL_PROCEED",
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to proceed from Crystal Sphere: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    // ── Private helpers ───────────────────────────────────────────────

    /// <summary>
    ///     Gets the <see cref="CrystalSphereMinigame" /> entity from the screen via reflection.
    /// </summary>
    private static CrystalSphereMinigame? GetEntity(NCrystalSphereScreen screen)
    {
        if (EntityField == null)
        {
            Logger.Warning("Could not find _entity field on NCrystalSphereScreen via reflection");
            return null;
        }

        return EntityField.GetValue(screen) as CrystalSphereMinigame;
    }
}
