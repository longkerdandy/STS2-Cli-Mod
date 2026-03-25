using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles choosing a rest site option by option ID (e.g., "HEAL", "SMITH").
///     Mirrors the game's <c>NRestSiteButton.SelectOption</c> flow:
///     disables options, fires <c>ChooseLocalOption</c> (which awaits the option's
///     <c>OnSelect</c>), then calls <c>AfterSelectingOption</c> on success.
///     Because some options (SMITH, COOK) open a card selection overlay and block
///     inside <c>OnSelect</c> until the player finishes, this handler uses a
///     fire-and-forget pattern: it launches the option task, then polls for
///     an observable state change (overlay appeared or proceed button enabled)
///     and returns immediately so the CLI can issue follow-up commands
///     (e.g., <c>deck_select_card</c>).
/// </summary>
public static class ChooseRestOptionHandler
{
    private static readonly ModLogger Logger = new("ChooseRestOptionHandler");

    /// <summary>
    ///     Handles the choose_rest_option request.
    ///     Validates parameters and delegates to ExecuteAsync.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Option ID required (e.g., HEAL, SMITH)" };

        var optionId = request.Id.ToUpperInvariant();
        Logger.Info($"Requested to choose rest site option: {optionId}");

        return await ExecuteAsync(optionId);
    }

    /// <summary>
    ///     Executes the choose_rest_option command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    /// <param name="optionId">The option ID (e.g., "HEAL", "SMITH").</param>
    private static async Task<object> ExecuteAsync(string optionId)
    {
        try
        {
            // --- Guard: Check rest site room ---
            var restSiteRoom = NRestSiteRoom.Instance;
            if (restSiteRoom == null || !restSiteRoom.IsInsideTree())
                return new { ok = false, error = "NOT_AT_REST_SITE", message = "Not currently at a rest site" };

            // --- Guard: Find the option by ID ---
            var options = restSiteRoom.Options;
            int optionIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].OptionId, optionId, StringComparison.OrdinalIgnoreCase))
                {
                    optionIndex = i;
                    break;
                }
            }

            if (optionIndex < 0)
                return new
                {
                    ok = false,
                    error = "OPTION_NOT_FOUND",
                    message = $"Rest site option '{optionId}' not found. Available: {string.Join(", ", options.Select(o => o.OptionId))}"
                };

            var selectedOption = options[optionIndex];

            // --- Guard: Check option is enabled ---
            if (!selectedOption.IsEnabled)
                return new
                {
                    ok = false,
                    error = "OPTION_DISABLED",
                    message = $"Rest site option '{optionId}' is disabled"
                };

            // --- Disable options to prevent double-clicks (mirrors NRestSiteButton) ---
            restSiteRoom.DisableOptions();

            // --- Fire-and-forget: launch the option execution ---
            // Mirrors NRestSiteButton.SelectOption: await ChooseLocalOption, then
            // call AfterSelectingOption on success, or re-enable on failure.
            // We cannot await this because SMITH/COOK block inside OnSelect
            // waiting for card selection overlay input.
            // Important: do NOT use Task.Run — the entire chain must stay on the
            // Godot main thread (OnSelect calls Godot APIs like CardSelectCmd).
            // The discard (_) makes this fire-and-forget on the current thread.
            _ = ExecuteOptionFireAndForgetAsync(restSiteRoom, optionIndex, selectedOption, optionId);

            // --- Poll for observable state change ---
            // For immediate options (HEAL, LIFT, DIG, etc.): proceed button becomes enabled.
            // For overlay options (SMITH, COOK): a card selection overlay appears.
            // For HEAL with relic rewards: an overlay stack entry appears.
            await ActionUtils.PollUntilAsync(() =>
            {
                // Proceed button enabled means option completed immediately
                if (NRestSiteRoom.Instance?.ProceedButton is { IsEnabled: true })
                    return true;

                // An overlay appeared (card selection for SMITH/COOK, or reward overlay)
                if (NOverlayStack.Instance?.Peek() is not null)
                    return true;

                // Map opened (safety)
                if (NMapScreen.Instance is { IsOpen: true })
                    return true;

                return false;
            }, ActionUtils.UiTimeoutMs);

            // --- Detect resulting screen and return appropriate state ---
            var screen = StateHandler.DetectCurrentScreen();
            Logger.Info($"After choosing '{optionId}', detected screen: {screen}");

            return new
            {
                ok = true,
                data = new
                {
                    option_id = optionId,
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to choose rest site option: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Fire-and-forget helper that mirrors <c>NRestSiteButton.SelectOption</c>.
    ///     Awaits <c>ChooseLocalOption</c> (which blocks on SMITH/COOK card selection),
    ///     then calls <c>AfterSelectingOption</c> on success or re-enables options on failure.
    ///     Must run on the Godot main thread (caller uses discard <c>_</c>, not <c>Task.Run</c>).
    /// </summary>
    private static async Task ExecuteOptionFireAndForgetAsync(
        NRestSiteRoom restSiteRoom, int optionIndex,
        MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption option, string optionId)
    {
        try
        {
            var success = await RunManager.Instance.RestSiteSynchronizer
                .ChooseLocalOption(optionIndex);

            if (success)
            {
                Logger.Info($"Option '{optionId}' succeeded, triggering AfterSelectingOption");
                restSiteRoom.AfterSelectingOption(option);
            }
            else
            {
                Logger.Warning($"ChooseLocalOption returned false for '{optionId}', re-enabling options");
                restSiteRoom.EnableOptions();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Fire-and-forget option task failed: {ex.Message}");
            try { restSiteRoom.EnableOptions(); } catch { /* best effort */ }
        }
    }
}
