using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the 'view_deck' command by reading the player's master deck.
///     Works at any point during a run (map, combat, shop, event, rest site, etc.),
///     similar to the in-game deck button in the top-right corner.
/// </summary>
public static class ViewDeckHandler
{
    private static readonly ModLogger Logger = new("ViewDeckHandler");

    /// <summary>
    ///     Returns the full master deck as a list of <see cref="PileCardDto" />.
    /// </summary>
    public static Task<object> ExecuteAsync()
    {
        return Task.FromResult<object>(Execute());
    }

    private static object Execute()
    {
        try
        {
            if (!RunManager.Instance.IsInProgress)
                return new { ok = false, error = "NO_ACTIVE_RUN", message = "No run is currently in progress" };

            var runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null)
                return new { ok = false, error = "INTERNAL_ERROR", message = "RunState is null" };

            if (runState.Players.Count == 0)
                return new { ok = false, error = "INTERNAL_ERROR", message = "No players found" };

            var player = runState.Players[0];
            var cards = player.Deck.Cards;

            var deckCards = new List<PileCardDto>();
            foreach (var card in cards)
                try
                {
                    deckCards.Add(BuildDeckCard(card));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build deck card: {ex.Message}");
                }

            Logger.Info($"Deck viewed: {deckCards.Count} cards");

            return new
            {
                ok = true,
                data = new
                {
                    count = deckCards.Count,
                    cards = deckCards
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to view deck: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = $"Deck view failed: {ex.Message}" };
        }
    }

    /// <summary>
    ///     Builds a <see cref="PileCardDto" /> from a master deck <see cref="CardModel" />.
    ///     Always includes description since this is an explicit deck view request.
    /// </summary>
    private static PileCardDto BuildDeckCard(CardModel card)
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
            Description = StripGameTags(card.GetDescriptionForPile(PileType.Deck))
        };
    }
}
