using Game.AI;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AStarSelfTest
{
    // 在菜单栏造一个按钮：Tools > A* 自测。点它就会跑下面的方法。
    [MenuItem("Tools/A* 自测")]
    public static void Run()
    {
        AStar.Node[,] grid = MakeTestGrid();   // 5x5，中间下方一堵墙（上方留洞）
        AStar.Node start = grid[0, 0];         // 起点：左下角
        AStar.Node goal = grid[4, 0];          // 终点：右下角（直线被墙挡住）

        List<AStar.Node> path = AStar.FindPath(grid, start, goal);

        if (path == null)
        {
            Debug.LogError("A* 自测失败：明明有路却没找到");
            return;
        }

        // 打印整条路径
        string s = "路径: ";
        foreach (var n in path) s += $"({n.X},{n.Y}) ";
        Debug.Log(s);

        // 检查路径有没有"穿墙"（墙是 x=2 且 y<=2 那几格）
        bool bad = false;
        foreach (var n in path)
            if (n.X == 2 && n.Y <= 2) { bad = true; break; }

        Debug.Log(bad ? "失败：路径穿过了墙" : "通过：绕开了墙走到了终点");
    }

    // 第二个自测：完全堵死的场景，应当返回 null（验证"无路返回空"）
    [MenuItem("Tools/A* 自测2：无路")]
    public static void RunNoPath()
    {
        AStar.Node[,] grid = MakeTestGrid();
        for (int y = 0; y < 5; y++) grid[2, y].Walkable = false;   // 这次整列都堵死

        var path = AStar.FindPath(grid, grid[0, 0], grid[4, 0]);
        Debug.Log(path == null ? "通过：正确地返回了 null（无路）" : "失败：明明无路却找到了路");
    }

    private static AStar.Node[,] MakeTestGrid()
    {
        var grid = new AStar.Node[5, 5];
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                grid[x, y] = new AStar.Node { X = x, Y = y, Walkable = true };

        // 在 x=2 这一列的下方 3 格放墙（y=3、y=4 留洞）
        grid[2, 0].Walkable = false;
        grid[2, 1].Walkable = false;
        grid[2, 2].Walkable = false;
        return grid;
    }
}