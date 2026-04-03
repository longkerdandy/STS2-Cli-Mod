using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>bundle_select</c>, <c>bundle_confirm</c>, and <c>bundle_cancel</c> CLI commands.
///     Manages the bundle selection flow on <see cref="NChooseABundleSelectionScreen" />,
///     which appears when the player obtains the Scroll Boxes relic.
/// </summary>
/// <remarks>
///     <para><b>CLI commands:</b></para>
///     <list type="bullet">
///         <item><c>sts2 bundle_select &lt;index&gt;</c> — preview a bundle by clicking its hitbox</item>
///         <item><c>sts2 bundle_confirm</c> — confirm the previewed bundle selection</item>
///         <item><c>sts2 bundle_cancel</c> — cancel preview and return to bundle selection</item>
///     </list>
///     <para><b>Flow:</b> select (preview) → confirm or cancel → select again → confirm</para>
/// </remarks>
public static class BundleSelectHandler
{
    private static readonly ModLogger Logger = new("BundleSelectHandler");

    /// <summary>
    ///     Handles the bundle_select request.
    ///     Accepts an index argument (args[0]) for the bundle to preview.
    /// </summary>
    public static async Task<object> HandleSelectAsync(Request request)
    {
        if (request.Args == null || request.Args.Length < 1)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Bundle index required (0-based)" };

        var bundleIndex = request.Args[0];
        Logger.Info($"Requested to select bundle at index {bundleIndex}");

        return await ExecuteSelectAsync(bundleIndex);
    }

    /// <summary>
    ///     Handles the bundle_confirm request.
    ///     Clicks the confirm button to accept the previewed bundle.
    /// </summary>
    public static async Task<object> HandleConfirmAsync(Request request)
    {
        Logger.Info("Requested to confirm bundle selection");
        return await ExecuteConfirmAsync();
    }

    /// <summary>
    ///     Handles the bundle_cancel request.
    ///     Clicks the cancel button to return to bundle selection.
    /// </summary>
    public static async Task<object> HandleCancelAsync(Request request)
    {
        Logger.Info("Requested to cancel bundle preview");
        return await ExecuteCancelAsync();
    }

    /// <summary>
    ///     Selects (previews) a bundle by index. Clicks the bundle's hitbox to open its preview.
    /// </summary>
    /// <param name="bundleIndex">0-based index of the bundle to preview.</param>
    private static async Task<object> ExecuteSelectAsync(int bundleIndex)
    {
        try
        {
            // --- Guard: Check bundle selection screen ---
            var screen = UiUtils.FindScreenInOverlay<NChooseABundleSelectionScreen>();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_BUNDLE_SELECT",
                    message = "Not in bundle selection screen. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Check if preview is already showing ---
            var previewContainer = screen.GetNodeOrNull<Control>("%BundlePreviewContainer");
            if (previewContainer?.Visible == true)
                return new
                {
                    ok = false,
                    error = "PREVIEW_ALREADY_OPEN",
                    message = "A bundle preview is already open. Use 'bundle_confirm' or 'bundle_cancel' first."
                };

            // --- Guard: Find bundles ---
            var bundles = UiUtils.FindAll<NCardBundle>(screen);
            if (bundles.Count == 0)
                return new
                {
                    ok = false,
                    error = "NO_BUNDLES_AVAILABLE",
                    message = "No bundles available in the selection screen"
                };

            // --- Guard: Check bundle index ---
            if (bundleIndex < 0 || bundleIndex >= bundles.Count)
                return new
                {
                    ok = false,
                    error = "INVALID_BUNDLE_INDEX",
                    message = $"Bundle index {bundleIndex} is out of range (0-{bundles.Count - 1})"
                };

            // --- Click the bundle hitbox to open preview ---
            var bundle = bundles[bundleIndex];
            Logger.Info($"Clicking bundle at index {bundleIndex}");
            bundle.Hitbox.ForceClick();

            // --- Wait for preview to appear ---
            await Task.Delay(ActionUtils.PreviewAppearDelayMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectScreen();
            return new
            {
                ok = true,
                data = new
                {
                    action = "BUNDLE_SELECT",
                    bundle_index = bundleIndex,
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to select bundle: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Confirms the currently previewed bundle. Clicks the confirm button
    ///     and polls until the overlay screen is removed.
    /// </summary>
    private static async Task<object> ExecuteConfirmAsync()
    {
        try
        {
            // --- Guard: Check bundle selection screen ---
            var screen = UiUtils.FindScreenInOverlay<NChooseABundleSelectionScreen>();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_BUNDLE_SELECT",
                    message = "Not in bundle selection screen. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Find and check confirm button ---
            var confirmButton = screen.GetNodeOrNull<NConfirmButton>("%Confirm");
            if (confirmButton is not { IsEnabled: true })
                return new
                {
                    ok = false,
                    error = "CANNOT_CONFIRM",
                    message = "Confirm button is not enabled. Preview a bundle first with 'bundle_select <index>'."
                };

            // --- Click confirm ---
            Logger.Info("Clicking confirm button");
            confirmButton.ForceClick();

            // --- Poll for the overlay to be removed ---
            await ActionUtils.PollUntilAsync(() =>
            {
                var current = UiUtils.FindScreenInOverlay<NChooseABundleSelectionScreen>();
                return current == null;
            }, ActionUtils.UiTimeoutMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectScreen();
            Logger.Info($"After confirming bundle, detected screen: {resultScreen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "BUNDLE_CONFIRM",
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to confirm bundle selection: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Cancels the current bundle preview. Clicks the cancel/back button
    ///     and waits for the preview to close.
    /// </summary>
    private static async Task<object> ExecuteCancelAsync()
    {
        try
        {
            // --- Guard: Check bundle selection screen ---
            var screen = UiUtils.FindScreenInOverlay<NChooseABundleSelectionScreen>();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_BUNDLE_SELECT",
                    message = "Not in bundle selection screen. Use 'sts2 state' to check current screen."
                };

            // --- Guard: Find and check cancel button ---
            var cancelButton = screen.GetNodeOrNull<NBackButton>("%Cancel");
            if (cancelButton is not { IsEnabled: true })
                return new
                {
                    ok = false,
                    error = "CANNOT_CANCEL",
                    message = "Cancel button is not enabled. No bundle preview is currently open."
                };

            // --- Click cancel ---
            Logger.Info("Clicking cancel button");
            cancelButton.ForceClick();

            // --- Wait for preview to close ---
            await Task.Delay(ActionUtils.PreviewAppearDelayMs);

            // --- Return result ---
            var resultScreen = StateHandler.DetectScreen();
            Logger.Info($"After cancelling bundle preview, detected screen: {resultScreen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "BUNDLE_CANCEL",
                    screen = resultScreen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to cancel bundle preview: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
