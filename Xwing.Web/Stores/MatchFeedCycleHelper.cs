namespace Xwing.Web.Stores;

internal static class MatchFeedCycleHelper
{
    public static bool WouldCreateCycle(int sourceMatchId, int targetMatchId, IReadOnlyList<(int Source, int Target)> edges)
    {
        if (sourceMatchId == targetMatchId)
        {
            return true;
        }

        Dictionary<int, List<int>> adjacency = [];
        foreach ((int source, int target) in edges)
        {
            if (!adjacency.TryGetValue(source, out List<int>? targets))
            {
                targets = [];
                adjacency[source] = targets;
            }

            targets.Add(target);
        }

        var stack = new Stack<int>();
        stack.Push(targetMatchId);
        var visited = new HashSet<int>();
        while (stack.Count > 0)
        {
            int node = stack.Pop();
            if (node == sourceMatchId)
            {
                return true;
            }

            if (!visited.Add(node))
            {
                continue;
            }

            if (adjacency.TryGetValue(node, out List<int>? next))
            {
                foreach (int child in next)
                {
                    stack.Push(child);
                }
            }
        }

        return false;
    }
}
