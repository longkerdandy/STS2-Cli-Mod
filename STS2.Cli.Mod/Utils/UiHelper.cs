using Godot;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility class for finding UI nodes in the scene tree.
/// </summary>
public static class UiHelper
{
    /// <summary>
    ///     Finds all nodes of type T within the specified parent node.
    /// </summary>
    /// <typeparam name="T">The type of nodes to find.</typeparam>
    /// <param name="parent">The parent node to search within.</param>
    /// <returns>List of nodes of type T.</returns>
    public static List<T> FindAll<T>(Node parent) where T : Node
    {
        var results = new List<T>();
        FindAllRecursive(parent, results);
        return results;
    }

    /// <summary>
    ///     Finds the first node of type T within the specified parent node.
    /// </summary>
    /// <typeparam name="T">The type of node to find.</typeparam>
    /// <param name="parent">The parent node to search within.</param>
    /// <returns>First node of type T, or null if not found.</returns>
    public static T? FindFirst<T>(Node parent) where T : Node
    {
        if (parent is T typedNode)
        {
            return typedNode;
        }

        foreach (Node child in parent.GetChildren())
        {
            var result = FindFirst<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void FindAllRecursive<T>(Node node, List<T> results) where T : Node
    {
        if (node is T typedNode)
        {
            results.Add(typedNode);
        }

        foreach (Node child in node.GetChildren())
        {
            FindAllRecursive(child, results);
        }
    }
}
