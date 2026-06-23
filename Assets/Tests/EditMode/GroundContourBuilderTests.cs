using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GroundContourBuilderTests
{
    [Test]
    public void GroupConnected_DiagonalCells_AreSeparateGroups()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 1) };

        List<List<Vector2Int>> groups = GroundContourBuilder.GroupConnected(cells);

        Assert.AreEqual(2, groups.Count);
    }

    [Test]
    public void GroupConnected_OrthogonalCells_AreOneGroup()
    {
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        List<List<Vector2Int>> groups = GroundContourBuilder.GroupConnected(cells);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(3, groups[0].Count);
    }

    [Test]
    public void BuildOuterLoops_SingleCell_IsUnitSquareCCW()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            },
            loops[0]);
    }

    [Test]
    public void BuildOuterLoops_HorizontalRun_TopEdgeIsFlat_AndCollapsed()
    {
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0)
        };

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 0), new Vector2(3, 0),
                new Vector2(3, 1), new Vector2(0, 1)
            },
            loops[0]);
        Assert.AreEqual(loops[0][2].y, loops[0][3].y);
    }

    [Test]
    public void BuildOuterLoops_Donut_KeepsOnlyOuterLoop()
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                if (!(x == 1 && y == 1))
                    cells.Add(new Vector2Int(x, y));

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 0), new Vector2(3, 0),
                new Vector2(3, 3), new Vector2(0, 3)
            },
            loops[0]);
    }

    [Test]
    public void BuildOuterLoops_TwoDiagonalGroups_ProduceTwoLoops()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(2, 2) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(2, loops.Count);
    }
}
