using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles relic selection from the "choose a relic" screen (<see cref="NChooseARelicSelection" />).
///     This overlay appears after boss fights and certain events, presenting relics for the player
///     to pick one (or skip). Selection is done by calling <c>ForceClick()</c> on the target
///     <see cref="NRelicBasicHolder" />, which emits <c>Released</c> → <c>SelectHolder()</c>.
/// </summary>
public static class RelicSelectHandler
{
    private static readonly ModLogger Logger = new("RelicSelectHandler");

    /// <summary>
    ///     Handles the relic_select request.
    ///     Accepts an index argument (args[0]) for the relic to select.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (request.Args == null || request.Args.Length < 1)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Relic index required (0-based)" };

        var relicIndex = request.Args[0];
        Logger.Info($"Requested to select relic at index {relicIndex}");

        return await ExecuteAsync(relicIndex);
    }

    /// <summary>
    ///     Handles the relic_skip request.
    ///     Clicks the skip button to decline all relics.
    /// </summary>
    public static async Task<object> HandleSkipRequestAsync(Request request)
    {
        Logger.Info("Requested to skip relic selection");
        return await ExecuteSkipAsync();
    }

    /// <summary>
    ///     Selects a relic by index from the "choose a relic" selection screen.
    /// </summary>
    /// <param name="relicIndex">0-based index of the relic to select.</param>
    private static async Task<object> ExecuteAsync(int relicIndex)
    {
        try
        {
            // --- Guard: Check relic selection screen ---
            var screen = UiUtils.FindScreenInOverlay<NChooseARelicSelection>();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_RELIC_SELECT",
                    message = "Not in relic selection screen. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Find relic holders ---
            var holders = UiUtils.FindAll<NRelicBasicHolder>(screen);
            if (holders.Count == 0)
                return new
                {
                    ok = false,
                    error = "NO_RELICS_AVAILABLE",
                    message = "No relics available in the selection screen"
                };

            // --- Guard: Check relic index ---
            if (relicIndex < 0 || relicIndex >= holders.Count)
                return new
                {
                    ok = false,
                    error = "INVALID_RELIC_INDEX",
                    message = $"Relic index {relicIndex} is out of range (0-{holders.Count - 1})"
                };

            // --- Select the relic ---
            var holder = holders[relicIndex];
            var relicModel = holder.Relic?.Model;
            var relicId = relicModel?.Id.Entry ?? "UNKNOWN";
            Logger.Info($"Selecting relic at index {relicIndex}: {relicId}");

            holder.ForceClick();

            // --- Poll for the overlay to be removed ---
            await ActionUtils.PollUntilAsync(() =>
            {
                var current = UiUtils.FindScreenInOverlay<NChooseARelicSelection>();
                return current == null;
            }, ActionUtils.UiTimeoutMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectCurrentScreen();
            Logger.Info($"After selecting relic, detected screen: {resultScreen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "RELIC_SELECT",
                    relic_id = relicId,
                    relic_index = relicIndex,
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to select relic: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Skips the relic selection by clicking the skip button.
    /// </summary>
    private static async Task<object> ExecuteSkipAsync()
    {
        try
        {
            // --- Guard: Check relic selection screen ---
            var screen = UiUtils.FindScreenInOverlay<NChooseARelicSelection>();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_RELIC_SELECT",
                    message = "Not in relic selection screen. Use 'sts2 state' to check current screen."
                };

            // --- Find and click the skip button ---
            // NChooseARelicSelection has a NChoiceSelectionSkipButton at node path "SkipButton"
            var skipButton = UiUtils.FindFirst<NChoiceSelectionSkipButton>(screen);
            if (skipButton == null)
                return new
                {
                    ok = false,
                    error = "SKIP_BUTTON_NOT_FOUND",
                    message = "Skip button not found in relic selection screen"
                };

            Logger.Info("Skipping relic selection");
            skipButton.ForceClick();

            // --- Poll for the overlay to be removed ---
            await ActionUtils.PollUntilAsync(() =>
            {
                var current = UiUtils.FindScreenInOverlay<NChooseARelicSelection>();
                return current == null;
            }, ActionUtils.UiTimeoutMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectCurrentScreen();
            Logger.Info($"After skipping relic selection, detected screen: {resultScreen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "RELIC_SKIP",
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to skip relic selection: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
