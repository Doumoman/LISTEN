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
        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0f);

        Assert.AreEqual(1, loops.Count);
        // winding 반전: 위 → 아래 순서
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(1, -1), new Vector2(0, -1)
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

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0f);

        Assert.AreEqual(1, loops.Count);
        // 표면 Y=1, 바닥 Y=-1 인 직사각형(코너 4개로 압축, winding 반전)
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 1), new Vector2(3, 1),
                new Vector2(3, -1), new Vector2(0, -1)
            },
            loops[0]);
        // 윗변 양 끝 꼭짓점의 Y 가 동일(평평) — 경사 없음
        Assert.AreEqual(loops[0][0].y, loops[0][1].y);
    }

    [Test]
    public void BuildTopProfiles_Step_FollowsTopSurfaceWithFlatBottom()
    {
        // (0,0),(1,0),(1,1): 0열 표면 Y=1, 1열 표면 Y=2. depth=2 → 바닥 Y=minTop(1)-2=-1
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0f);

        Assert.AreEqual(1, loops.Count);
        // winding 반전 순서
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(1, 2), new Vector2(2, 2),
                new Vector2(2, -1), new Vector2(0, -1)
            },
            loops[0]);
    }

    [Test]
    public void BuildTopProfiles_EdgeInset_PullsEndWallsInward()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0) };

        // 표면 Y=1, depth=2 → 바닥 Y=-1, 양끝 X 를 0.25 안으로 당김
        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0.25f);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0.25f, 1), new Vector2(0.75f, 1),
                new Vector2(0.75f, -1), new Vector2(0.25f, -1)
            },
            loops[0]);
    }

    [Test]
    public void BuildTopProfiles_EdgeInset_AppliesToInteriorStepRiser()
    {
        // (0,0),(1,0),(1,1): 0열 표면 Y=1, 1열 표면 Y=2 인 계단. depth=2, inset=0.25.
        // 양끝 벽뿐 아니라 x=1 의 단차 리저도 안으로(오른쪽으로) 당겨져야 한다.
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0.25f);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0.25f, 1), new Vector2(1.25f, 1),
                new Vector2(1.25f, 2), new Vector2(1.75f, 2),
                new Vector2(1.75f, -1), new Vector2(0.25f, -1)
            },
            loops[0]);
    }

    [Test]
    public void BuildTopProfiles_InsetCollapsesNarrowColumn_ProducesDegenerateLoop()
    {
        // 폭 1 기둥에 inset 0.5 → 윗변 폭이 0 으로 붕괴.
        // 근접 점이 병합되어 점 3개 미만 → 호출측(CreateGroundShape)이 건너뛴다.
        var cells = new List<Vector2Int> { new Vector2Int(0, 0) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0.5f);

        Assert.AreEqual(1, loops.Count);
        Assert.Less(loops[0].Count, 3);
    }

    [Test]
    public void BuildTopProfiles_TwoDiagonalGroups_ProduceTwoLoops()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(2, 2) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildTopProfiles(cells, 2f, 0f);

        Assert.AreEqual(2, loops.Count);
    }
}
