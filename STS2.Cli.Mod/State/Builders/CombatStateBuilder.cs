using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds CombatStateDto and related collections (hand, enemies).
/// </summary>
public static class CombatStateBuilder
{
    private static readonly ModLogger Logger = new("CombatStateBuilder");

    /// <summary>
    ///     Builds the full combat state DTO from <see cref="CombatManager" />.
    ///     Returns null if combat is not in progress.
    /// </summary>
    /// <param name="includePileDetails">Whether to include full card descriptions in pile listings.</param>
    public static CombatStateDto? Build(bool includePileDetails = false)
    {
        var combatManager = CombatManager.Instance;
        if (!combatManager.IsInProgress)
        {
            Logger.Warning("CombatManager reports IsInProgress = false");
            return null;
        }

        var combatState = combatManager.DebugOnlyGetState();
        if (combatState == null)
        {
            Logger.Warning("CombatState is null");
            return null;
        }

        var result = new CombatStateDto
        {
            Encounter = combatState.Encounter?.Id.Entry,
            TurnNumber = combatState.RoundNumber,
            IsPlayerTurn = combatManager.IsPlayPhase,
            IsPlayerActionsDisabled = combatManager.PlayerActionsDisabled,
            IsCombatEnding = combatManager.IsOverOrEnding
        };

        var player = combatState.Players.Count > 0 ? combatState.Players[0] : null;
        if (player != null)
        {
            result.Player = PlayerStateBuilder.Build(player);
            result.Hand = BuildHand(player);
            result.DrawPile = BuildDrawPile(player, includePileDetails);
            result.DiscardPile = BuildDiscardPile(player, includePileDetails);
            result.ExhaustPile = BuildExhaustPile(player, includePileDetails);
        }

        result.Enemies = BuildEnemies(combatState);

        return result;
    }

    /// <summary>
    ///     Builds the hand state from the player's combat state.
    /// </summary>
    public static List<CardStateDto> BuildHand(Player player)
    {
        var hand = new List<CardStateDto>();

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState?.Hand == null) return hand;

        var cards = playerCombatState.Hand.Cards;
        for (var i = 0; i < cards.Count; i++)
            try
            {
                hand.Add(CardStateBuilder.Build(cards[i], i));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build card state for hand index {i}: {ex.Message}");
            }

        return hand;
    }

    /// <summary>
    ///     Builds the enemies state from combat state.
    ///     Includes all enemies (alive and dead) with IsAlive flag for each.
    /// </summary>
    public static List<EnemyStateDto> BuildEnemies(CombatState combatState)
    {
        var enemies = new List<EnemyStateDto>();

        foreach (var creature in combatState.Enemies)
            try
            {
                enemies.Add(EnemyStateBuilder.Build(creature, combatState));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build enemy state for {creature.Monster?.Id}: {ex.Message}");
            }

        return enemies;
    }

    /// <summary>
    ///     Builds the draw pile state (shuffled to hide draw order).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="includeDescription">Whether to include card descriptions.</param>
    public static List<PileCardDto> BuildDrawPile(Player player, bool includeDescription = false)
    {
        var pile = new List<PileCardDto>();

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState?.DrawPile == null) return pile;

        var cards = playerCombatState.DrawPile.Cards;
        foreach (var card in cards)
            try
            {
                pile.Add(BuildPileCard(card, includeDescription));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build draw pile card state: {ex.Message}");
            }

        // Shuffle to hide draw order (matches in-game behavior)
        // Using a deterministic seed based on card count for consistency
        var rng = new Random(pile.Count + DateTime.Now.Millisecond);
        pile = pile.OrderBy(_ => rng.Next()).ToList();

        return pile;
    }

    /// <summary>
    ///     Builds the discard pile state.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="includeDescription">Whether to include card descriptions.</param>
    public static List<PileCardDto> BuildDiscardPile(Player player, bool includeDescription = false)
    {
        var pile = new List<PileCardDto>();

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState?.DiscardPile == null) return pile;

        var cards = playerCombatState.DiscardPile.Cards;
        foreach (var card in cards)
            try
            {
                pile.Add(BuildPileCard(card, includeDescription));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build discard pile card state: {ex.Message}");
            }

        return pile;
    }

    /// <summary>
    ///     Builds the exhaust pile state.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="includeDescription">Whether to include card descriptions.</param>
    public static List<PileCardDto> BuildExhaustPile(Player player, bool includeDescription = false)
    {
        var pile = new List<PileCardDto>();

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState?.ExhaustPile == null) return pile;

        var cards = playerCombatState.ExhaustPile.Cards;
        foreach (var card in cards)
            try
            {
                pile.Add(BuildPileCard(card, includeDescription));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build exhaust pile card state: {ex.Message}");
            }

        return pile;
    }

    /// <summary>
    ///     Builds a simplified pile card DTO.
    /// </summary>
    private static PileCardDto BuildPileCard(CardModel card, bool includeDescription)
    {
        // Get energy cost (handle X cost as -1)
        int cost = 0;
        try
        {
            if (card.EnergyCost.CostsX)
                cost = -1;
            else
                cost = card.EnergyCost.GetAmountToSpend();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read energy cost for pile card {card.Id}: {ex.Message}");
        }

        // Get description if requested
        string? description = null;
        if (includeDescription)
        {
            try
            {
                description = TextUtils.StripGameTags(card.GetDescriptionForPile(PileType.Hand));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to read description for pile card {card.Id}: {ex.Message}");
            }
        }

        // Get keywords
        var keywords = new List<string>();
        try
        {
            foreach (var keyword in card.Keywords)
                if (keyword != CardKeyword.None)
                    keywords.Add(keyword.ToString());
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read keywords for pile card {card.Id}: {ex.Message}");
        }

        return new PileCardDto
        {
            Id = card.Id.Entry,
            Name = TextUtils.StripGameTags(card.Title),
            Type = card.Type.ToString(),
            Rarity = card.Rarity.ToString(),
            Cost = cost,
            Keywords = keywords,
            IsUpgraded = card.IsUpgraded,
            Description = description
        };
    }
}
