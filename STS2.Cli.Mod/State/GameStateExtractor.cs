using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Extracts game state from Slay the Spire 2 using direct type references.
///     Based on actual class names from decompiled sts2.dll.
/// </summary>
public static class GameStateExtractor
{
    private static readonly ModLogger Logger = new("GameStateExtractor");

    /// <summary>
    ///     Gets the current game state.
    /// </summary>
    public static GameStateDto GetState()
    {
        try
        {
            var state = new GameStateDto
            {
                Screen = DetectScreen(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Extract combat state if in combat
            if (state.Screen == "COMBAT")
            {
                state.Combat = ExtractCombatState();
            }

            return state;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract game state: {ex.Message}");
            return new GameStateDto { Screen = "ERROR", Error = ex.Message };
        }
    }

    /// <summary>
    ///     Detects which screen the player is currently on.
    /// </summary>
    private static string DetectScreen()
    {
        // Check if run is in progress
        if (!RunManager.Instance.IsInProgress)
        {
            return "MENU";
        }

        // Check CombatManager for active combat
        if (CombatManager.Instance.IsInProgress)
        {
            return "COMBAT";
        }

        // TODO: Detect other screens (MAP, SHOP, EVENT, etc.)
        return "UNKNOWN";
    }

    /// <summary>
    ///     Extracts combat state from CombatManager.
    /// </summary>
    private static CombatStateDto? ExtractCombatState()
    {
        try
        {
            var combatManager = CombatManager.Instance;
            if (!combatManager.IsInProgress)
            {
                Logger.Warning("CombatManager reports IsInProgress = false");
                return null;
            }

            // Get CombatState via DebugOnlyGetState
            var combatState = combatManager.DebugOnlyGetState();
            if (combatState == null)
            {
                Logger.Warning("CombatState is null");
                return null;
            }

            var result = new CombatStateDto
            {
                IsPlayerTurn = combatManager.IsPlayPhase,
                TurnNumber = combatState.RoundNumber
            };

            // Extract player state
            var player = GetLocalPlayer(combatState);
            if (player != null)
            {
                result.Player = BuildPlayerState(player);
                result.Hand = BuildHandState(player);
            }

            // Extract enemies
            result.Enemies = BuildEnemiesState(combatState);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract combat state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the local player from combat state.
    /// </summary>
    private static Player? GetLocalPlayer(MegaCrit.Sts2.Core.Combat.CombatState combatState)
    {
        try
        {
            // In single player, get the first player
            var players = combatState.Players;
            if (players.Count > 0)
            {
                return players[0];
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get local player: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    ///     Builds player state from Player object.
    /// </summary>
    private static PlayerStateDto BuildPlayerState(Player player)
    {
        var state = new PlayerStateDto();

        try
        {
            var creature = player.Creature;
            var playerCombatState = player.PlayerCombatState;

            // Basic stats from Creature
            state.Hp = creature.CurrentHp;
            state.MaxHp = creature.MaxHp;
            state.Block = creature.Block;

            // Combat stats from PlayerCombatState
            if (playerCombatState != null)
            {
                state.Energy = playerCombatState.Energy;
                state.MaxEnergy = playerCombatState.MaxEnergy;

                // Pile counts
                state.DeckCount = playerCombatState.DrawPile.Cards.Count;
                state.DiscardCount = playerCombatState.DiscardPile.Cards.Count;
                state.ExhaustCount = playerCombatState.ExhaustPile?.Cards.Count ?? 0;
                state.HandCount = playerCombatState.Hand.Cards.Count;
            }

            // Buffs/Powers from Creature
            state.Buffs = BuildBuffsState(creature.Powers);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build player state: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds hand state from player's combat state.
    /// </summary>
    private static List<CardStateDto> BuildHandState(Player player)
    {
        var hand = new List<CardStateDto>();

        try
        {
            var playerCombatState = player.PlayerCombatState;
            if (playerCombatState == null) return hand;

            var cards = playerCombatState.Hand.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                hand.Add(BuildCardState(card, i));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build hand state: {ex.Message}");
        }

        return hand;
    }

    /// <summary>
    ///     Builds a single card state.
    /// </summary>
    private static CardStateDto BuildCardState(CardModel card, int index)
    {
        var state = new CardStateDto
        {
            Index = index,
            Id = card.Id.Entry,
            Name = card.Title,
            IsUpgraded = card.IsUpgraded
        };

        try
        {
            // Cost display
            if (card.EnergyCost.CostsX)
            {
                state.Cost = -1; // X cost represented as -1
                state.CostDisplay = "X";
            }
            else
            {
                int cost = card.EnergyCost.GetAmountToSpend();
                state.Cost = cost;
                state.CostDisplay = cost.ToString();
            }

            // Can play check
            card.CanPlay(out var unplayableReason, out _);
            state.CanPlay = unplayableReason == UnplayableReason.None;
            state.UnplayableReason = unplayableReason != UnplayableReason.None ? unplayableReason.ToString() : null;

            // Description
            state.Description = card.Description.GetFormattedText();

            // Type
            state.Type = card.Type.ToString();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build card state for {card.Id}: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds enemies state from CombatState.
    /// </summary>
    private static List<EnemyStateDto> BuildEnemiesState(MegaCrit.Sts2.Core.Combat.CombatState combatState)
    {
        var enemies = new List<EnemyStateDto>();

        try
        {
            var creatures = combatState.Enemies;
            for (int i = 0; i < creatures.Count; i++)
            {
                var creature = creatures[i];
                if (!creature.IsAlive) continue;

                enemies.Add(BuildEnemyState(creature, i, combatState));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build enemies state: {ex.Message}");
        }

        return enemies;
    }

    /// <summary>
    ///     Builds a single enemy state.
    /// </summary>
    private static EnemyStateDto BuildEnemyState(Creature creature, int index, MegaCrit.Sts2.Core.Combat.CombatState combatState)
    {
        var state = new EnemyStateDto
        {
            Index = index,
            Hp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            Block = creature.Block,
            IsMinion = creature.IsPet
        };

        try
        {
            var monster = creature.Monster;
            if (monster != null)
            {
                state.Id = monster.Id.Entry;
                state.Name = monster.Title.GetFormattedText();

                // Intent
                var nextMove = monster.NextMove;
                if (nextMove is MoveState moveState)
                {
                    state.Intent = BuildIntentState(moveState, creature, combatState);
                }
            }
            else
            {
                state.Id = "unknown";
                state.Name = creature.Name;
            }

            // Buffs
            state.Buffs = BuildBuffsState(creature.Powers);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build enemy state: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds intent state from MoveState.
    /// </summary>
    private static IntentStateDto BuildIntentState(MoveState moveState, Creature creature, MegaCrit.Sts2.Core.Combat.CombatState combatState)
    {
        var state = new IntentStateDto();

        try
        {
            var intents = moveState.Intents;
            if (intents.Count > 0)
            {
                // Use first intent for now
                var intent = intents[0];
                state.Type = intent.IntentType.ToString();

                // Try to get label
                try
                {
                    var targets = combatState.PlayerCreatures;
                    var label = intent.GetIntentLabel(targets, creature);
                    state.Description = label.GetFormattedText();
                }
                catch
                {
                    state.Description = "";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build intent state: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds buffs/powers state.
    /// </summary>
    private static List<BuffStateDto> BuildBuffsState(IEnumerable<PowerModel> powers)
    {
        var buffs = new List<BuffStateDto>();

        try
        {
            foreach (var power in powers)
            {
                if (!power.IsVisible) continue;

                var buff = new BuffStateDto
                {
                    Id = power.Id.Entry,
                    Name = power.Title.GetFormattedText(),
                    Amount = power.DisplayAmount,
                    Type = power.Type.ToString(),
                    Description = power.SmartDescription.GetFormattedText()
                };

                buffs.Add(buff);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build buffs state: {ex.Message}");
        }

        return buffs;
    }
}

// State DTOs - using "Dto" suffix to avoid conflicts with game types

public class GameStateDto
{
    public string Screen { get; set; } = "UNKNOWN";
    public long Timestamp { get; set; }
    public string? Error { get; set; }
    public CombatStateDto? Combat { get; set; }
}

public class CombatStateDto
{
    public bool IsPlayerTurn { get; set; } = true;
    public int TurnNumber { get; set; } = 1;
    public PlayerStateDto Player { get; set; } = new();
    public List<CardStateDto> Hand { get; set; } = new();
    public List<EnemyStateDto> Enemies { get; set; } = new();
}

public class PlayerStateDto
{
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; } = 3;
    public int Block { get; set; }
    public int HandCount { get; set; }
    public int DeckCount { get; set; }
    public int DiscardCount { get; set; }
    public int ExhaustCount { get; set; }
    public List<BuffStateDto> Buffs { get; set; } = new();
}

public class CardStateDto
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string CostDisplay { get; set; } = "";
    public bool CanPlay { get; set; }
    public string? UnplayableReason { get; set; }
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsUpgraded { get; set; }
}

public class EnemyStateDto
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Block { get; set; }
    public bool IsMinion { get; set; }
    public IntentStateDto Intent { get; set; } = new();
    public List<BuffStateDto> Buffs { get; set; } = new();
}

public class IntentStateDto
{
    public string Type { get; set; } = "UNKNOWN";
    public string Description { get; set; } = "";
}

public class BuffStateDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Amount { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}
