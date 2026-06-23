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
    public void BuildTopProfiles_SingleCell_TopSurfacePlusDepthBottom()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0) };

        // 표면 Y=1, depth=2 → 바닥 Y=-1
        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, -1), new Vector2(1, -1),
                new Vector2(1, 1), new Vector2(0, 1)
            },
            loops[0]);
    }

    [Test]
    public void BuildTopProfiles_HorizontalRun_TopEdgeIsFlat_AndCollapsed()
    {
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0)
        };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f);

        Assert.AreEqual(1, loops.Count);
        // 표면 Y=1, 바닥 Y=-1 인 직사각형(코너 4개로 압축)
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, -1), new Vector2(3, -1),
                new Vector2(3, 1), new Vector2(0, 1)
            },
            loops[0]);
        // 윗변 양 끝 꼭짓점의 Y 가 동일(평평) — 경사 없음
        Assert.AreEqual(loops[0][2].y, loops[0][3].y);
    }

    [Test]
    public void BuildTopProfiles_Step_FollowsTopSurfaceWithFlatBottom()
    {
        // (0,0),(1,0),(1,1): 0열 표면 Y=1, 1열 표면 Y=2. depth=2 → 바닥 Y=minTop(1)-2=-1
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, -1), new Vector2(2, -1),
                new Vector2(2, 2), new Vector2(1, 2),
                new Vector2(1, 1), new Vector2(0, 1)
            },
            loops[0]);
    }

    [Test]
    public void BuildTopProfiles_TwoDiagonalGroups_ProduceTwoLoops()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(2, 2) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f);

        Assert.AreEqual(2, loops.Count);
    }
}
