using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using STS2.Cli.Mod.Server;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod;

/// <summary>
///     Entry point for the STS2 CLI Mod.
///     Loaded by the game's native mod loader via [ModInitializer] attribute.
/// </summary>
[ModInitializer("Initialize")]
public static class CliModEntry
{
    private static readonly ModLogger Logger = new("ModEntry");

    /// <summary>
    ///     Initializes the mod and starts the pipe server.
    /// </summary>
    public static void Initialize()
    {
        var version = typeof(CliModEntry).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        Logger.Info("========================================");
        Logger.Info("STS2.Cli.Mod loaded successfully!");
        Logger.Info($"Version: {version}");
        Logger.Info("========================================");

        // Initialize the main thread executor (required for game actions)
        MainThreadExecutor.Initialize();

        // Start the pipe server background loop
        try
        {
            PipeServer.Start();
            Logger.Info("Named Pipe server started on 'sts2-cli-mod'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to start pipe server: {ex.Message}");
        }
    }
}