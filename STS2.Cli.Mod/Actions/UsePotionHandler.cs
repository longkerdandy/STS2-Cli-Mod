using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.Models.Actions;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the use potion action by constructing a <see cref="UsePotionAction" />
///     and enqueuing it through the game's native ActionQueue.
///     After enqueuing, waits for completion and collects execution results
///     (damage dealt, block gained, powers applied) from <c>CombatHistory</c>.
/// </summary>
public static class UsePotionHandler
{
    private static readonly ModLogger Logger = new("UsePotionHandler");

    /// <summary>
    ///     Maximum time to wait for a <see cref="UsePotionAction" /> to finish executing.
    ///     Covers potion throw animation and triggered effects.
    /// </summary>
    private const int ActionTimeoutMs = 10000;

    /// <summary>
    ///     Handles the use_potion request.
    ///     Validates parameters and delegates to ExecuteAsync.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Potion ID required (e.g., FIRE_POTION)" };

        var nthValue = request.Nth ?? 0;
        Logger.Info($"Requested to use potion {request.Id}, nth={nthValue}, target={request.Target?.ToString() ?? "none"}");

        return await ExecuteAsync(request.Id, nthValue, request.Target);
    }

    /// <summary>
    ///     Uses a potion from the player's potion belt by ID and returns the execution results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="potionId">Potion ID to use (e.g., "FIRE_POTION").</param>
    /// <param name="nth">N-th occurrence when multiple copies exist (0-based).</param>
    /// <param name="targetCombatId">Optional target combat ID for targeted potions.</param>
    public static async Task<object> ExecuteAsync(string potionId, int nth = 0, int? targetCombatId = null)
    {
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
            Creature? target = null;
            if (potion.TargetType == TargetType.AnyEnemy)
            {
                if (targetCombatId == null)
                    return new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = "Potion requires a target. Provide 'target' with an enemy combat_id."
                    };

                target = ActionUtils.ResolveEnemyTarget((uint)targetCombatId.Value);
                if (target == null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No hittable enemy found with combat_id {targetCombatId}"
                    };
            }
            else if (potion.TargetType is TargetType.Self or TargetType.AnyPlayer)
            {
                // Self-targeting or any player: target is the player's creature
                target = player.Creature;

                if (targetCombatId != null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_ALLOWED",
                        message =
                            $"Potion '{potion.Title}' has target type '{potion.TargetType}' and does not accept a target"
                    };
            }
            else
            {
                // AllEnemies, AllAllies, None, etc. — no target needed
                if (targetCombatId != null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_ALLOWED",
                        message =
                            $"Potion '{potion.Title}' has target type '{potion.TargetType}' and does not accept a target"
                    };
            }

            // --- Check if this potion requires card selection ---

            if (PotionUtils.RequiresCardSelection(potion.Id.Entry))
            {
                Logger.Info($"Potion '{potion.Title}' requires card selection, monitoring for selection screen");
                return await HandlePotionWithCardSelectionAsync(potion, slot, target, targetCombatId);
            }

            // --- Standard potion: Enqueue action and wait for completion ---

            return await ExecuteStandardPotionAsync(potion, slot, target, targetCombatId);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to use potion: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Handles potions that open card selection screens.
    ///     Enqueues the potion action and polls for the selection screen to appear.
    /// </summary>
    private static async Task<object> HandlePotionWithCardSelectionAsync(
        PotionModel potion, int slot, Creature? target, int? targetCombatId)
    {
        // Snapshot history count before the action executes
        var historyBefore = CombatManager.Instance.History.Entries.Count();

        // Enqueue the potion action
        var action = new UsePotionAction(potion, target, CombatManager.Instance.IsInProgress);

        var targetName = target?.Monster?.Title.GetFormattedText();
        var targetMsg = targetName != null ? $" targeting {targetName}" : "";
        Logger.Info($"UsePotionAction enqueued (selection type): '{potion.Title}' (slot {slot}){targetMsg}");

        // Start the action without awaiting completion
        _ = ActionUtils.EnqueueAndAwaitAsync(action, ActionTimeoutMs);

        // Poll for selection screen to appear (max 5 seconds)
        const int SelectionScreenTimeoutMs = 5000;
        const int PollIntervalMs = 100;
        var elapsedMs = 0;

        while (elapsedMs < SelectionScreenTimeoutMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsedMs += PollIntervalMs;

            // Check if selection screen appeared
            var selectionScreen = FindCardSelectionScreen();
            if (selectionScreen != null)
            {
                Logger.Info($"Card selection screen detected for potion '{potion.Title}'");
                var cards = ExtractSelectableCards(selectionScreen);
                var constraints = PotionUtils.GetSelectionConstraints(potion.Id.Entry);

                return new
                {
                    ok = true,
                    data = new
                    {
                        status = "selection_required",
                        selection_type = PotionUtils.GetSelectionType(potion.Id.Entry),
                        potion_id = potion.Id.Entry,
                        potion_slot = slot,
                        min_select = constraints.MinSelect,
                        max_select = constraints.MaxSelect,
                        can_skip = constraints.CanSkip,
                        cards
                    }
                };
            }

            // Check if action completed without selection screen (shouldn't happen for these potions)
            if (action.State != GameActionState.WaitingForExecution && action.State != GameActionState.Executing)
            {
                break;
            }
        }

        // Selection screen didn't appear, wait for normal completion
        Logger.Warning($"Selection screen did not appear for potion '{potion.Title}', waiting for normal completion");
        return await WaitForPotionCompletionAsync(action, potion, slot, targetCombatId, historyBefore);
    }

    /// <summary>
    ///     Executes a standard potion (without card selection) and waits for completion.
    /// </summary>
    private static async Task<object> ExecuteStandardPotionAsync(
        PotionModel potion, int slot, Creature? target, int? targetCombatId)
    {
        // Snapshot history count before the action executes
        var historyBefore = CombatManager.Instance.History.Entries.Count();

        // Manually construct the UsePotionAction
        var action = new UsePotionAction(potion, target, CombatManager.Instance.IsInProgress);

        var targetName = target?.Monster?.Title.GetFormattedText();
        var targetMsg = targetName != null ? $" targeting {targetName}" : "";
        Logger.Info($"UsePotionAction enqueued: '{potion.Title}' (slot {slot}){targetMsg}");

        return await WaitForPotionCompletionAsync(action, potion, slot, targetCombatId, historyBefore);
    }

    /// <summary>
    ///     Waits for a potion action to complete and returns results.
    /// </summary>
    private static async Task<object> WaitForPotionCompletionAsync(
        UsePotionAction action, PotionModel potion, int slot, int? targetCombatId, int historyBefore)
    {
        var finalState = await ActionUtils.EnqueueAndAwaitAsync(action, ActionTimeoutMs);
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

        // Collect results from CombatHistory
        var results = CombatHistoryBuilder.BuildFromHistory(historyBefore);
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
    ///     Finds the currently open card selection screen (NChooseACardSelectionScreen).
    /// </summary>
    private static NChooseACardSelectionScreen? FindCardSelectionScreen()
    {
        // Check overlay stack first
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack?.Peek() is NChooseACardSelectionScreen screen)
        {
            return screen;
        }

        // Search in children if not on top of stack
        if (overlayStack != null)
        {
            foreach (var child in overlayStack.GetChildren())
            {
                if (child is NChooseACardSelectionScreen childScreen)
                {
                    return childScreen;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Extracts selectable cards from the selection screen.
    /// </summary>
    private static List<SelectableCardDto> ExtractSelectableCards(NChooseACardSelectionScreen screen)
    {
        var cards = new List<SelectableCardDto>();
        var cardHolders = UiHelper.FindAll<NCardHolder>(screen);

        for (int i = 0; i < cardHolders.Count; i++)
        {
            var holder = cardHolders[i];
            var card = holder.CardModel;
            if (card == null) continue;

            cards.Add(new SelectableCardDto
            {
                Index = i,
                CardId = card.Id.Entry,
                CardName = TextUtils.StripGameTags(card.Title),
                CardType = card.Type.ToString(),
                Cost = card.EnergyCost?.Canonical,
                Description = TextUtils.StripGameTags(card.Description?.GetFormattedText() ?? "")
            });
        }

        return cards;
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
            {
                matchingPotions.Add((potion, slot));
            }
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
                message = $"Potion '{potionId}' has {matchingPotions.Count} copies. Use nth from 0 to {matchingPotions.Count - 1}."
            });
        }

        var selected = matchingPotions[nth];
        Logger.Info($"Found potion '{potionId}' at slot {selected.Slot} (nth={nth}, total matches={matchingPotions.Count})");

        return (selected.Potion, selected.Slot, null);
    }
}
