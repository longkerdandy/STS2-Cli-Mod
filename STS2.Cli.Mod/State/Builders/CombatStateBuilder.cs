using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

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
            var pcs = player.PlayerCombatState;
            result.Player = PlayerStateBuilder.Build(player);
            result.Hand = BuildHand(pcs);
            result.DrawPile = BuildPile(pcs?.DrawPile, includePileDetails, shuffle: true);
            result.DiscardPile = BuildPile(pcs?.DiscardPile, includePileDetails);
            result.ExhaustPile = BuildPile(pcs?.ExhaustPile, includePileDetails);
        }

        result.Enemies = BuildEnemies(combatState);

        return result;
    }

    /// <summary>
    ///     Builds the hand state from the player's combat state.
    /// </summary>
    private static List<CardStateDto> BuildHand(PlayerCombatState? pcs)
    {
        var hand = new List<CardStateDto>();
        if (pcs?.Hand == null) return hand;

        var cards = pcs.Hand.Cards;
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
    private static List<EnemyStateDto> BuildEnemies(CombatState combatState)
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
    ///     Builds a pile state from a <see cref="CardPile" />.
    ///     Optionally shuffles the result to hide draw order (for draw pile).
    /// </summary>
    private static List<PileCardDto> BuildPile(CardPile? cardPile, bool includeDescription,
        bool shuffle = false)
    {
        var pile = new List<PileCardDto>();
        if (cardPile == null) return pile;

        foreach (var card in cardPile.Cards)
            try
            {
                pile.Add(BuildPileCard(card, includeDescription));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build pile card state: {ex.Message}");
            }

        if (shuffle)
        {
            var rng = new Random(pile.Count + DateTime.Now.Millisecond);
            pile = pile.OrderBy(_ => rng.Next()).ToList();
        }

        return pile;
    }

    /// <summary>
    ///     Builds a simplified pile card DTO.
    /// </summary>
    private static PileCardDto BuildPileCard(CardModel card, bool includeDescription)
    {
        var keywords = new List<string>();
        foreach (var keyword in card.Keywords)
            if (keyword != CardKeyword.None)
                keywords.Add(keyword.ToString());

        return new PileCardDto
        {
            Id = card.Id.Entry,
            Name = StripGameTags(card.Title),
            Type = card.Type.ToString(),
            Rarity = card.Rarity.ToString(),
            Cost = card.EnergyCost.CostsX ? -1 : card.EnergyCost.GetAmountToSpend(),
            Keywords = keywords,
            IsUpgraded = card.IsUpgraded,
            Description = includeDescription
                ? StripGameTags(card.GetDescriptionForPile(PileType.Hand))
                : null
        };
    }
}
