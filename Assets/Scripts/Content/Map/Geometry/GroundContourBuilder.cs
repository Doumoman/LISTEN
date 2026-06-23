using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground 타일(단위 격자 셀) 집합으로부터 경사 없는 닫힌 윤곽을 계산하는 순수 로직.
/// 셀 gridPos 는 격자 모서리 좌표로 gridPos ~ gridPos+(1,1) 영역을 차지한다.
/// </summary>
public static class GroundContourBuilder
{
    private static readonly Vector2Int[] Dirs4 =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>4방향(상하좌우) 인접 셀을 연결 컴포넌트로 묶는다.</summary>
    public static List<List<Vector2Int>> GroupConnected(IReadOnlyCollection<Vector2Int> cells)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(cells);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<List<Vector2Int>> groups = new List<List<Vector2Int>>();

        foreach (Vector2Int start in set)
        {
            if (visited.Contains(start)) continue;

            List<Vector2Int> group = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                group.Add(cur);

                foreach (Vector2Int d in Dirs4)
                {
                    Vector2Int nb = cur + d;
                    if (set.Contains(nb) && !visited.Contains(nb))
                    {
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }
}
