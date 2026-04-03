using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility methods for locating game screens and UI elements via game singletons.
///     Provides a centralized place for screen lookup logic used by both
///     <c>StateHandler</c> (screen detection) and action handlers (guard clauses).
/// </summary>
public static class ScreenUtils
{
    private static readonly ModLogger Logger = new("ScreenUtils");

    /// <summary>
    ///     Finds the <see cref="NMainMenu" /> instance via <see cref="NGame.Instance" />.
    /// </summary>
    /// <returns>The main menu node, or <c>null</c> if not on the main menu.</returns>
    public static NMainMenu? FindMainMenu()
    {
        try
        {
            return NGame.Instance?.MainMenu;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to find main menu: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Finds the <see cref="NSingleplayerSubmenu" /> if it is currently at the top of
    ///     the main menu's submenu stack.
    /// </summary>
    /// <returns>
    ///     The singleplayer submenu, or <c>null</c> if no submenu is open
    ///     or the top submenu is not <see cref="NSingleplayerSubmenu" />.
    /// </returns>
    public static NSingleplayerSubmenu? FindSingleplayerSubmenu()
    {
        try
        {
            var submenuStack = FindMainMenu()?.SubmenuStack;
            if (submenuStack is not { SubmenusOpen: true }) return null;
            return submenuStack.Peek() as NSingleplayerSubmenu;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to find singleplayer submenu: {ex.Message}");
            return null;
        }
    }
}