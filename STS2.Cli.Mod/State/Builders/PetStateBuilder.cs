using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds PetStateDto list from a player's combat pets.
/// </summary>
public static class PetStateBuilder
{
    private static readonly ModLogger Logger = new("PetStateBuilder");

    /// <summary>
    ///     Builds pet states from the player's pet creature list.
    /// </summary>
    public static List<PetStateDto> Build(IReadOnlyList<Creature> pets)
    {
        var result = new List<PetStateDto>();

        foreach (var pet in pets)
            try
            {
                result.Add(new PetStateDto
                {
                    CombatId = pet.CombatId ?? 0,
                    Id = pet.Monster!.Id.Entry,
                    Name = StripGameTags(pet.Monster.Title.GetFormattedText()),
                    IsAlive = pet.IsAlive,
                    Hp = pet.CurrentHp,
                    MaxHp = pet.MaxHp,
                    Block = pet.Block,
                    Powers = PowerStateBuilder.Build(pet.Powers)
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build pet state: {ex.Message}");
            }

        return result;
    }
}