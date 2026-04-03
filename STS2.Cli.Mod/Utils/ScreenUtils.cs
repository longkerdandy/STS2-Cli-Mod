using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility methods for locating game screens and UI elements via game singletons.
///     Provides a centralized place for screen lookup logic used by both
///     <c>StateHandler</c> (screen detection) and action handlers (guard clauses).
/// </summary>
/// <remarks>
///     All lookups use the game API directly (e.g., <c>NGame.Instance.MainMenu</c>,
///     <c>SubmenuStack.Peek()</c>) rather than scene tree traversal.
/// </remarks>
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

    /// <summary>
    ///     Finds the <see cref="NCharacterSelectScreen" /> if it is currently at the top of
    ///     the main menu's submenu stack.
    /// </summary>
    /// <returns>
    ///     The character select screen, or <c>null</c> if the top submenu is not
    ///     <see cref="NCharacterSelectScreen" />.
    /// </returns>
    /// <remarks>
    ///     <see cref="NCharacterSelectScreen" /> is an <see cref="NSubmenu" /> pushed onto
    ///     <see cref="NMainMenu.SubmenuStack" /> — it is NOT a root scene.
    ///     It may sit above <see cref="NSingleplayerSubmenu" /> on the stack (normal flow)
    ///     or be pushed directly when <c>NumberOfRuns == 0</c> (first-run shortcut).
    /// </remarks>
    public static NCharacterSelectScreen? FindCharacterSelectScreen()
    {
        try
        {
            var submenuStack = FindMainMenu()?.SubmenuStack;
            if (submenuStack is not { SubmenusOpen: true }) return null;
            return submenuStack.Peek() as NCharacterSelectScreen;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to find character select screen: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Finds a <see cref="NCardGridSelectionScreen" /> in the overlay stack
    ///     by reverse-iterating children (topmost overlay wins).
    /// </summary>
    /// <returns>The grid selection screen, or <c>null</c> if none is found.</returns>
    public static NCardGridSelectionScreen? FindGridSelectionScreen()
    {
        try
        {
            var overlayStack = NOverlayStack.Instance;
            if (overlayStack == null) return null;

            var children = overlayStack.GetChildren();
            for (var i = children.Count - 1; i >= 0; i--)
                if (children[i] is NCardGridSelectionScreen gridScreen)
                    return gridScreen;

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to find grid selection screen: {ex.Message}");
            return null;
        }
    }
}