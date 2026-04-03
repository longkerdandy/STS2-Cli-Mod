using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for finding UI nodes and game screens.
///     Provides generic tree-traversal helpers (<see cref="FindAll{T}" />, <see cref="FindFirst{T}" />),
///     overlay-stack screen lookup (<see cref="FindScreenInOverlay{T}" />),
///     and game-API screen finders (<see cref="FindMainMenu" />, <see cref="FindCharacterSelectScreen" />, etc.).
/// </summary>
public static class UiUtils
{
    private static readonly ModLogger Logger = new("UiUtils");

    // ── Generic tree-traversal helpers ────────────────────────────────

    /// <summary>
    ///     Recursively finds all descendant nodes of type <typeparamref name="T" />
    ///     within <paramref name="parent" /> (depth-first, inclusive).
    /// </summary>
    /// <typeparam name="T">The Godot node type to search for.</typeparam>
    /// <param name="parent">The root node to start the search from.</param>
    /// <returns>A list of all matching nodes; empty if none found.</returns>
    public static List<T> FindAll<T>(Node parent) where T : Node
    {
        var results = new List<T>();
        FindAllRecursive(parent, results);
        return results;
    }

    /// <summary>
    ///     Recursively finds the first descendant node of type <typeparamref name="T" />
    ///     within <paramref name="parent" /> (depth-first, inclusive).
    /// </summary>
    /// <typeparam name="T">The Godot node type to search for.</typeparam>
    /// <param name="parent">The root node to start the search from.</param>
    /// <returns>The first matching node, or <c>null</c> if none found.</returns>
    public static T? FindFirst<T>(Node parent) where T : Node
    {
        if (parent is T typedNode)
            return typedNode;

        foreach (var child in parent.GetChildren())
        {
            var result = FindFirst<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    // ── Overlay-stack screen lookup ──────────────────────────────────

    /// <summary>
    ///     Finds a screen of type <typeparamref name="T" /> in the game's <see cref="NOverlayStack" />.
    ///     Checks the top overlay first (fast path), then iterates all children (slow path).
    /// </summary>
    /// <typeparam name="T">The screen type to find (must extend <see cref="Node" />).</typeparam>
    /// <returns>The screen instance if found, or <c>null</c>.</returns>
    public static T? FindScreenInOverlay<T>() where T : Node
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        // Fast path: the top overlay is the screen we want
        if (overlayStack.Peek() is T screen)
            return screen;

        // Slow path: search children (another screen may be on top)
        foreach (var child in overlayStack.GetChildren())
            if (child is T found)
                return found;

        return null;
    }

    // ── Game-API screen finders ─────────────────────────────────────

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

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    ///     Recursively collects all descendant nodes of type <typeparamref name="T" />.
    /// </summary>
    private static void FindAllRecursive<T>(Node node, List<T> results) where T : Node
    {
        if (node is T typedNode)
            results.Add(typedNode);

        foreach (var child in node.GetChildren())
            FindAllRecursive(child, results);
    }
}
