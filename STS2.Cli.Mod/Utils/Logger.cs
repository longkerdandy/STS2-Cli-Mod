using Godot;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Simple logger wrapper around Godot's Logger.
/// </summary>
public class ModLogger
{
    private readonly string _prefix;

    public ModLogger(string name)
    {
        _prefix = $"[STS2.Cli.Mod][{name}]";
    }

    public void Info(string message)
    {
        GD.Print($"{_prefix} {message}");
    }

    public void Warning(string message)
    {
        GD.PushWarning($"{_prefix} {message}");
    }

    public void Error(string message)
    {
        GD.PushError($"{_prefix} {message}");
    }
}
