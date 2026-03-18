using HarmonyLib;
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
    ///     Applies the Harmony patch.
    /// </summary>
    public static void Apply(Harmony harmony)
    {
        if (_patched) return;

        try
        {
            // Find CombatManager type
            var combatManagerType = FindType("CombatManager", "BattleManager", "Game.Combat.CombatManager");
            if (combatManagerType == null)
            {
                Logger.Error("Could not find CombatManager type to patch");
                return;
            }

            // Find Update method
            var updateMethod = combatManagerType.GetMethod("Update",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (updateMethod == null)
            {
                Logger.Error("Could not find CombatManager.Update method");
                return;
            }

            // Patch the Update method
            var postfixMethod = typeof(CombatManagerPatch).GetMethod("Postfix",
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

    /// <summary>
    ///     Finds a type by name from all loaded assemblies.
    /// </summary>
    private static Type? FindType(params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(name);
                    if (type != null) return type;

                    type = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name == name || t.FullName?.EndsWith($".{name}") == true);
                    if (type != null) return type;
                }
                catch
                {
                    // Some assemblies might not support GetTypes()
                    continue;
                }
            }
        }
        return null;
    }
}
