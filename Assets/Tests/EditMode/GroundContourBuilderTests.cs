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
}
