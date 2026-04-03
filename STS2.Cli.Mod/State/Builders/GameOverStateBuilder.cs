using System.Collections;
using System.Reflection;
using Godot;
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

    private static readonly FieldInfo? RunStateField =
        typeof(NGameOverScreen).GetField("_runState",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? ScoreField =
        typeof(NGameOverScreen).GetField("_score",
            BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Builds the game over state from the current <see cref="NGameOverScreen" />.
    ///     Returns null if the game over screen is not found in the overlay stack.
    /// </summary>
    public static GameOverStateDto? Build()
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null)
        {
            Logger.Warning("NOverlayStack.Instance is null");
            return null;
        }

        var screen = overlayStack.Peek() as NGameOverScreen;
        if (screen == null)
        {
            Logger.Warning("NGameOverScreen not found in overlay stack");
            return null;
        }

        var runState = RunStateField?.GetValue(screen);
        var score = ScoreField?.GetValue(screen) as int? ?? 0;

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
            CanReturnToMenu = screen.GetNodeOrNull<Node>("%MainMenuButton") != null,
            CanContinue = screen.GetNodeOrNull<Node>("%ContinueButton") != null
        };
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
        var characters = runState.GetType().GetProperty("Characters")?.GetValue(runState) as IList;
        if (characters is not { Count: > 0 }) return null;

        var id = characters[0]?.GetType().GetProperty("Id")?.GetValue(characters[0]);
        return id?.GetType().GetProperty("Entry")?.GetValue(id) as string;
    }
}
