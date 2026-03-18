using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Common utilities for action classes.
/// </summary>
public static class ActionUtils
{
    private static readonly ModLogger Logger = new("ActionUtils");

    /// <summary>
    ///     Gets the local player from the current run.
    ///     In single player mode, returns the first player.
    /// </summary>
    public static Player? GetLocalPlayer()
    {
        try
        {
            if (!RunManager.Instance.IsInProgress)
                return null;

            var runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null)
                return null;

            // In single player, get the first player
            return runState.Players.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get local player: {ex.Message}");
            return null;
        }
    }
}
