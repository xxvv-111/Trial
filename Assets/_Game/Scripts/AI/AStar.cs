using System.Collections.Generic;

namespace Game.AI
{
    public class AStar
    {
        public class Node
        {
            public int X, Y;
            public bool Walkable;
            public float G;//起点到该点
            public float F;//f=g+h；
            public Node Parent;
        }
        private static readonly (int dx, int dy)[] Dir4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public static List<Node> FindPath(Node[,] grid, Node start, Node goal)
        {
            int rows = grid.GetLength(0);
            int cols = grid.GetLength(1);

            var open = new List<Node>();
            var closed = new HashSet<Node>();

            start.G = 0f;
            start.F = Manhattan(start, goal);
            start.Parent = null;
            open.Add(start);

            while (open.Count > 0)
            {
                Node cur = open[0];
                foreach (var n in open) if (n.F < cur.F) cur = n;

                if (cur == goal) return Reconstruct(cur);

                open.Remove(cur);
                closed.Add(cur);

                foreach (var (dx, dy) in Dir4)
                {
                    int nx = cur.X + dx;
                    int ny = cur.Y + dy;

                    if (nx < 0 || ny < 0 || nx >= rows || ny >= cols) continue;

                    Node nb = grid[nx, ny];
                    if (!nb.Walkable || closed.Contains(nb)) continue;

                    float tentative = cur.G + 1f;

                    if (tentative < nb.G || !open.Contains(nb))
                    {
                        nb.G = tentative;
                        nb.F = tentative + Manhattan(nb, goal);

                        nb.Parent = cur;

                        if (!open.Contains(nb)) open.Add(nb);
                    }
                }
            }
            return null;
        }

        private static List<Node> Reconstruct(Node n)
        {
            var path = new List<Node>();
            while (n != null) { path.Add(n); n = n.Parent; }
            path.Reverse();
            return path;
        }

        private static float Manhattan(Node a, Node b)
            => System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
    }
}
