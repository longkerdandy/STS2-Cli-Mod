using System.Globalization;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>use_potion</c> CLI command.
///     Uses a potion by constructing a <see cref="UsePotionAction" />
///     and enqueuing it through the game's native ActionQueue.
///     After enqueuing, waits for completion and collects execution results
///     (damage dealt, block gained, powers applied) from <c>CombatHistory</c>.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 use_potion &lt;potion_id&gt; [--nth &lt;n&gt;] [--target &lt;combat_id&gt;]</c></para>
///     <para><b>Scene:</b> Combat, during the player's turn (or outside combat for non-combat potions).</para>
/// </remarks>
public static class UsePotionHandler
{
    private static readonly ModLogger Logger = new("UsePotionHandler");

    /// <summary>
    ///     Uses a potion from the player's potion belt by ID and returns the execution results.
    ///     Validates parameters, enqueues the action, and collects results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Potion ID required (e.g., FIRE_POTION)" };

        var potionId = request.Id;
        var nth = request.Nth ?? 0;
        var targetCombatId = request.Target;
        
        Logger.Info($"Requested to use potion {potionId}, nth={nth}, target={targetCombatId?.ToString(CultureInfo.InvariantCulture) ?? "null"}");

        try
        {
            // --- Validation (synchronous, single frame) ---

            var combatError = ActionUtils.ValidateCombatReady();
            if (combatError != null) return combatError;

            var player = ActionUtils.GetLocalPlayer();
            if (player?.PlayerCombatState == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found or not in combat" };

            if (!player.Creature.IsAlive)
                return new { ok = false, error = "PLAYER_DEAD", message = "Player is dead - cannot use potions" };

            // Find potion by ID
            var (potion, slot, findError) = FindPotionById(player, potionId, nth);
            if (findError != null)
                return findError;

            if (potion.IsQueued)
                return new
                {
                    ok = false, error = "POTION_ALREADY_QUEUED",
                    message = $"Potion '{potion.Title}' is already queued for use"
                };

            if (!potion.PassesCustomUsabilityCheck)
                return new
                {
                    ok = false, error = "POTION_NOT_USABLE",
                    message = $"Potion '{potion.Title}' cannot be used right now"
                };

            // Resolve target based on potion's TargetType
            var (target, targetError) = ActionUtils.ResolveTarget(
                player, potion.TargetType, targetCombatId, potion.Title.ToString() ?? potion.Id.Entry);
            if (targetError != null)
                return targetError;

            // --- Enqueue and execute ---

            if (PotionUtils.RequiresCardSelection(potion.Id.Entry))
            {
                Logger.Info($"Potion '{potion.Title}' requires card selection, monitoring for selection screen");
                return await EnqueueWithCardSelectionAsync(potion, slot, target, targetCombatId);
            }

            return await EnqueueAndAwaitResultsAsync(potion, slot, target, targetCombatId);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to use potion: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Enqueues a standard potion action and waits for completion, then collects results.
    /// </summary>
    private static async Task<object> EnqueueAndAwaitResultsAsync(
        PotionModel potion, int slot, Creature? target, int? targetCombatId)
    {
        var historyBefore = CombatManager.Instance.History.Entries.Count();
        var action = CreateAndLogAction(potion, slot, target);

        var finalState = await ActionUtils.EnqueueAndAwaitAsync(action, ActionUtils.ActionTimeoutMs);

        return HandleActionResult(finalState, potion, slot, targetCombatId, historyBefore);
    }

    /// <summary>
    ///     Enqueues a potion action that opens a card selection screen.
    ///     Determines the expected UI type via <see cref="PotionUtils.GetSelectionUiType" />,
    ///     polls for that specific screen to appear, and builds the appropriate
    ///     <c>selection_required</c> response. Falls back to normal completion if the
    ///     expected screen does not appear (e.g., auto-select when eligible cards ≤ MinSelect).
    /// </summary>
    private static async Task<object> EnqueueWithCardSelectionAsync(
        PotionModel potion, int slot, Creature? target, int? targetCombatId)
    {
        var historyBefore = CombatManager.Instance.History.Entries.Count();
        var uiType = PotionUtils.GetSelectionUiType(potion.Id.Entry);
        var action = CreateAndLogAction(potion, slot, target, selectionType: true);

        Logger.Info($"Potion '{potion.Title}' expects UI type '{uiType}'");

        // Start the action and keep the Task reference for the fallback path
        var enqueueTask = ActionUtils.EnqueueAndAwaitAsync(action, ActionUtils.ActionTimeoutMs);

        // Poll for the expected selection screen to appear
        var elapsedMs = 0;
        while (elapsedMs < ActionUtils.UiTimeoutMs)
        {
            await Task.Delay(ActionUtils.DefaultPollIntervalMs);
            elapsedMs += ActionUtils.DefaultPollIntervalMs;

            // Check for the expected UI type
            var selectionResponse = DetectSelectionScreen(uiType, potion, slot);
            if (selectionResponse != null)
            {
                Logger.Info($"Selection screen ({uiType}) detected for potion '{potion.Title}'");
                return selectionResponse;
            }

            // Action finished before the selection screen appeared
            // (can happen when auto-select kicks in, e.g., eligible cards ≤ MinSelect)
            if (action.State is not (GameActionState.WaitingForExecution or GameActionState.Executing
                or GameActionState.GatheringPlayerChoice))
                break;
        }

        // Fallback: selection screen didn't appear — await the original task
        Logger.Warning($"Selection screen did not appear for potion '{potion.Title}', waiting for normal completion");
        var finalState = await enqueueTask;

        return HandleActionResult(finalState, potion, slot, targetCombatId, historyBefore);
    }

    /// <summary>
    ///     Checks whether the expected card selection screen is currently open and builds
    ///     the corresponding <c>selection_required</c> response.
    /// </summary>
    /// <param name="uiType">Expected UI type: <c>"tri_select"</c>, <c>"hand_select"</c>, or <c>"grid_select"</c>.</param>
    /// <param name="potion">The potion model that triggered the selection.</param>
    /// <param name="slot">The potion belt slot index.</param>
    /// <returns>A response object if the screen is detected, or <c>null</c> if not yet visible.</returns>
    private static object? DetectSelectionScreen(string? uiType, PotionModel potion, int slot)
    {
        switch (uiType)
        {
            case "tri_select":
            {
                var screen = CardSelectionUtils.FindCardSelectionScreen();
                if (screen != null)
                    return PotionUtils.BuildTriSelectResponse(potion, slot, screen);
                break;
            }

            case "hand_select":
            {
                if (NPlayerHand.Instance is { IsInCardSelection: true })
                    return PotionUtils.BuildHandSelectResponse(potion, slot);
                break;
            }

            case "grid_select":
            {
                var gridScreen = UiUtils.FindScreenInOverlay<NCardGridSelectionScreen>();
                if (gridScreen != null)
                    return PotionUtils.BuildGridSelectResponse(potion, slot, gridScreen);
                break;
            }
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a <see cref="UsePotionAction" /> and logs the enqueue.
    /// </summary>
    private static UsePotionAction CreateAndLogAction(
        PotionModel potion, int slot, Creature? target, bool selectionType = false)
    {
        var action = new UsePotionAction(potion, target, CombatManager.Instance.IsInProgress);

        var targetName = target?.Monster?.Title.GetFormattedText();
        var targetMsg = targetName != null ? $" targeting {targetName}" : "";
        var typeTag = selectionType ? " (selection type)" : "";
        Logger.Info($"UsePotionAction enqueued{typeTag}: '{potion.Title}' (slot {slot}){targetMsg}");

        return action;
    }

    /// <summary>
    ///     Converts a completed (or timed-out / cancelled) action into a response object.
    /// </summary>
    private static object HandleActionResult(
        GameActionState? finalState, PotionModel potion, int slot, int? targetCombatId, int historyBefore)
    {
        if (finalState == null)
        {
            Logger.Warning("UsePotionAction timed out waiting for completion");
            return new { ok = false, error = "TIMEOUT", message = "Potion action did not complete in time" };
        }

        if (finalState == GameActionState.Canceled)
        {
            Logger.Info("UsePotionAction was cancelled by the game");
            return new
            {
                ok = false, error = "ACTION_CANCELLED",
                message = $"Potion '{potion.Title}' action was cancelled by the game"
            };
        }

        var results = CombatHistoryUtils.BuildFromHistory(historyBefore);
        Logger.Info($"UsePotionAction completed with {results.Count} result entries");

        return new
        {
            ok = true,
            data = new
            {
                slot,
                potion_id = potion.Id.Entry,
                target = targetCombatId,
                results
            }
        };
    }

    /// <summary>
    ///     Finds a potion in the player's potion belt by ID and nth occurrence.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="potionId">Potion ID to find.</param>
    /// <param name="nth">N-th occurrence (0-based).</param>
    /// <returns>Tuple of (potion, slot, error). If the error is not null, potion and slot are invalid.</returns>
    private static (PotionModel Potion, int Slot, object? Error) FindPotionById(Player player, string potionId, int nth)
    {
        // Collect all non-null potions with their slots
        var matchingPotions = new List<(PotionModel Potion, int Slot)>();
        for (var slot = 0; slot < player.MaxPotionCount; slot++)
        {
            var potion = player.GetPotionAtSlotIndex(slot);
            if (potion != null && potion.Id.Entry.Equals(potionId, StringComparison.OrdinalIgnoreCase))
                matchingPotions.Add((potion, slot));
        }

        if (matchingPotions.Count == 0)
        {
            // Build a list of available potion IDs for the error message
            var availablePotions = new List<string>();
            for (var slot = 0; slot < player.MaxPotionCount; slot++)
            {
                var potion = player.GetPotionAtSlotIndex(slot);
                if (potion != null)
                    availablePotions.Add(potion.Id.Entry);
            }

            var availableStr = availablePotions.Count > 0 ? string.Join(", ", availablePotions) : "(none)";
            Logger.Warning($"Potion '{potionId}' not found. Available: {availableStr}");

            return (null!, 0, new
            {
                ok = false,
                error = "POTION_NOT_FOUND",
                message = $"Potion '{potionId}' not found. Available potions: {availableStr}"
            });
        }

        if (nth < 0 || nth >= matchingPotions.Count)
        {
            Logger.Warning($"Potion '{potionId}' has {matchingPotions.Count} copies, but nth={nth} was requested");
            return (null!, 0, new
            {
                ok = false,
                error = "INVALID_POTION_SLOT",
                message =
                    $"Potion '{potionId}' has {matchingPotions.Count} copies. Use nth from 0 to {matchingPotions.Count - 1}."
            });
        }

        var selected = matchingPotions[nth];
        Logger.Info(
            $"Found potion '{potionId}' at slot {selected.Slot} (nth={nth}, total matches={matchingPotions.Count})");

        return (selected.Potion, selected.Slot, null);
    }
}
