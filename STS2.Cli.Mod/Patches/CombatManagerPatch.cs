using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using STS2.Cli.Mod.Actions;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Patches;

/// <summary>
///     Harmony patch for CombatManager to execute CLI actions on the main thread.
/// </summary>
public static class CombatManagerPatch
{
    private static readonly ModLogger Logger = new("CombatManagerPatch");
    private static bool _patched;

    /// <summary>
    ///     Applies the Harmony patch to CombatManager.Update.
    /// </summary>
    public static void Apply(Harmony harmony)
    {
        if (_patched) return;

        try
        {
            // Get CombatManager.Update method directly
            var updateMethod = typeof(CombatManager).GetMethod("Update",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (updateMethod == null)
            {
                Logger.Error("Could not find CombatManager.Update method");
                return;
            }

            // Patch the Update method
            var postfixMethod = typeof(CombatManagerPatch).GetMethod(nameof(Postfix),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfixMethod));

            _patched = true;
            Logger.Info("CombatManager.Update patched successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to apply patch: {ex.Message}");
        }
    }

    /// <summary>
    ///     Postfix that runs after CombatManager.Update to execute pending actions.
    /// </summary>
    public static void Postfix()
    {
        // Execute any pending actions queued by CLI
        ActionExecutor.ExecutePendingActions();
    }
}
