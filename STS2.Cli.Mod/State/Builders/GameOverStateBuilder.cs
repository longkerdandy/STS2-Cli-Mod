using System.Collections;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="GameOverStateDto" /> from the current <see cref="NGameOverScreen" />.
/// </summary>
public static class GameOverStateBuilder
{
    private static readonly ModLogger Logger = new("GameOverStateBuilder");

    /// <summary>
    ///     Builds the game over state from the current <see cref="NGameOverScreen" />.
    ///     Returns null if the game over screen is not found in the overlay stack.
    /// </summary>
    public static GameOverStateDto? Build()
    {
        try
        {
            var overlayStack = NOverlayStack.Instance;
            if (overlayStack == null)
            {
                Logger.Warning("NOverlayStack.Instance is null");
                return null;
            }

            if (overlayStack.Peek() is not NGameOverScreen screen)
            {
                Logger.Warning("NGameOverScreen not found in overlay stack");
                return null;
            }

            var runState = UiUtils.GetPrivateField<object>(screen, "_runState");
            var score = UiUtils.GetPrivateFieldValue<int>(screen, "_score") ?? 0;

            var isVictory = false;
            var floor = 0;
            string? characterId = null;

            if (runState != null)
            {
                isVictory = GetProperty<bool>(runState, "Win");
                floor = GetProperty<int>(runState, "CurrentFloor");
                characterId = GetCharacterId(runState);
            }

            return new GameOverStateDto
            {
                IsVictory = isVictory,
                Floor = floor,
                CharacterId = characterId,
                Score = score,
                EpochsDiscovered = 0,
                CanReturnToMenu = UiUtils.HasChildNode(screen, "%MainMenuButton"),
                CanContinue = UiUtils.HasChildNode(screen, "%ContinueButton")
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build game over state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets a property value from an object via reflection, returning default on failure.
    /// </summary>
    private static T GetProperty<T>(object obj, string name) where T : struct
    {
        var prop = obj.GetType().GetProperty(name);
        return prop != null ? (T)(prop.GetValue(obj) ?? default(T)) : default;
    }

    /// <summary>
    ///     Extracts the first character's ID from the run state's Characters list.
    /// </summary>
    private static string? GetCharacterId(object runState)
    {
        if (runState.GetType().GetProperty("Characters")?.GetValue(runState) is not IList { Count: > 0 } characters)
            return null;

        var id = characters[0]?.GetType().GetProperty("Id")?.GetValue(characters[0]);
        return id?.GetType().GetProperty("Entry")?.GetValue(id) as string;
    }
}