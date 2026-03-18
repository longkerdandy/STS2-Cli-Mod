using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using STS2.Cli.Mod.Patches;
using STS2.Cli.Mod.Server;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod;

/// <summary>
/// Entry point for the STS2 CLI Mod.
/// Loaded by the game's native mod loader via [ModInitializer] attribute.
/// </summary>
[ModInitializer("Initialize")]
public static class CliModEntry
{
    private static readonly ModLogger Logger = new("ModEntry");
    private static PipeServer? _pipeServer;
    private static Harmony? _harmony;

    /// <summary>
    /// Initializes the mod and starts the pipe server.
    /// </summary>
    public static void Initialize()
    {
        Logger.Info("========================================");
        Logger.Info("STS2.Cli.Mod loaded successfully!");
        Logger.Info("Version: 0.1.0");
        Logger.Info("========================================");

        // Initialize Harmony
        try
        {
            _harmony = new Harmony("sts2.climod");
            CombatManagerPatch.Apply(_harmony);
            Logger.Info("Harmony patches applied");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to apply Harmony patches: {ex.Message}");
        }

        // Initialize and start the pipe server (fire and forget)
        try
        {
            _pipeServer = new PipeServer();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _pipeServer.StartAsync();
                    Logger.Info("Named Pipe server started on 'sts2-cli-mod'");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to start pipe server: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize pipe server: {ex.Message}");
            Logger.Error($"Stack trace: {ex.StackTrace}");
        }
    }
}
