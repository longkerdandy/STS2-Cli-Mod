using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles choosing a rest site option by option ID (e.g., "HEAL", "SMITH").
///     Uses <see cref="MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer.ChooseLocalOption" />
///     to execute the selection, then triggers UI updates via
///     <see cref="NRestSiteRoom.AfterSelectingOption" />.
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

            // --- Execute the option via RestSiteSynchronizer ---
            Logger.Info($"Choosing rest site option '{optionId}' at index {optionIndex}");
            var success = await RunManager.Instance.RestSiteSynchronizer.ChooseLocalOption(optionIndex);

            if (!success)
            {
                Logger.Warning($"ChooseLocalOption returned false for '{optionId}'");
                return new
                {
                    ok = false,
                    error = "OPTION_CANCELLED",
                    message = $"Rest site option '{optionId}' was cancelled (e.g., SMITH card selection was skipped)"
                };
            }

            // --- Trigger UI update ---
            Logger.Info($"Option '{optionId}' succeeded, triggering AfterSelectingOption");
            restSiteRoom.AfterSelectingOption(selectedOption);

            // --- Wait for UI to settle ---
            // AfterSelectingOption triggers async animations (HideChoices, VFX, ShowProceedButton).
            // For SMITH, the card selection overlay will have already been shown and closed by now
            // (ChooseLocalOption awaits OnSelect which awaits CardSelectCmd.FromDeckForUpgrade).
            // We wait for either the proceed button to become enabled or an overlay to appear.
            await ActionUtils.PollUntilAsync(() =>
            {
                // Check if proceed button is now enabled
                if (NRestSiteRoom.Instance?.ProceedButton is { IsEnabled: true })
                    return true;

                // Check if an overlay appeared (e.g., rewards from HEAL with specific relics)
                if (NOverlayStack.Instance?.Peek() is not null)
                    return true;

                // Check if map opened (shouldn't happen but safety check)
                if (NMapScreen.Instance is { IsOpen: true })
                    return true;

                return false;
            }, ActionUtils.UiTimeoutMs);

            // --- Build updated rest site state ---
            var updatedState = RestSiteStateBuilder.Build();

            return new
            {
                ok = true,
                data = new
                {
                    option_id = optionId,
                    success = true,
                    rest_site = updatedState
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to choose rest site option: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
