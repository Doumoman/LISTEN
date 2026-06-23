using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground 타일(단위 격자 셀) 집합으로부터 경사 없는 SpriteShape 윤곽을 계산하는 순수 로직.
/// 셀 gridPos 는 격자 모서리 좌표로 gridPos ~ gridPos+(1,1) 영역을 차지한다.
/// 플레이어가 밟는 윗면만 칠하는 워크플로를 위해, 각 영역의 윗면 프로파일을 따고
/// 그 아래로 depth 만큼 평평한 자동 바닥을 붙여 닫힌 흙 몸통을 만든다.
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

    /// <summary>
    /// 4-연결 그룹마다 닫힌 윤곽 1개를 만든다.
    /// 윗면은 각 열의 최상단 셀 표면을 따르는 계단형 프로파일,
    /// 아래는 그룹 최저 표면에서 depth 만큼 내린 평평한 자동 바닥.
    /// 각 루프는 격자 모서리 좌표(바닥 Y만 depth 반영)의 CCW 다각형이며 직선 점은 합쳐진다.
    /// </summary>
    public static List<List<Vector2>> BuildTopProfiles(IReadOnlyCollection<Vector2Int> cells, float depth)
    {
        List<List<Vector2>> result = new List<List<Vector2>>();

        foreach (List<Vector2Int> group in GroupConnected(cells))
            result.Add(BuildTopProfile(group, depth));

        return result;
    }

    private static List<Vector2> BuildTopProfile(List<Vector2Int> group, float depth)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        foreach (Vector2Int c in group)
        {
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
        }

        // 열(column)마다 최상단 셀의 윗면 Y (= 셀.y + 1)
        Dictionary<int, int> topY = new Dictionary<int, int>();
        foreach (Vector2Int c in group)
        {
            int t = c.y + 1;
            if (!topY.TryGetValue(c.x, out int cur) || t > cur)
                topY[c.x] = t;
        }

        int minTop = int.MaxValue;
        for (int x = minX; x <= maxX; x++)
            if (topY[x] < minTop) minTop = topY[x];

        float bottomY = minTop - depth;

        List<Vector2> pts = new List<Vector2>();

        // 바닥 좌→우, 오른쪽 변을 타고 윗면 우측 끝까지
        pts.Add(new Vector2(minX, bottomY));
        pts.Add(new Vector2(maxX + 1, bottomY));
        pts.Add(new Vector2(maxX + 1, topY[maxX]));

        // 윗면 프로파일 우→좌 (계단)
        for (int x = maxX; x >= minX; x--)
        {
            pts.Add(new Vector2(x, topY[x]));                 // 열 x 윗면의 좌측 끝
            if (x > minX && topY[x - 1] != topY[x])
                pts.Add(new Vector2(x, topY[x - 1]));         // 경계 x 의 수직 단차
        }
        // (minX, topY[minX]) 도달 → 왼쪽 변을 타고 바닥으로 닫힘(Collapse 가 순환 처리)

        List<Vector2> corners = Collapse(pts);
        corners.Reverse(); // SpriteShape edge 가 바깥(위)을 향하도록 winding 반전
        return corners;
    }

    // 직선으로 이어지는 점 제거(코너만 유지). 폐곡선으로 순환 처리.
    private static List<Vector2> Collapse(List<Vector2> loop)
    {
        int n = loop.Count;
        List<Vector2> outPts = new List<Vector2>();

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = loop[(i - 1 + n) % n];
            Vector2 cur = loop[i];
            Vector2 nxt = loop[(i + 1) % n];

            Vector2 d1 = cur - prev;
            Vector2 d2 = nxt - cur;

            float cross = d1.x * d2.y - d1.y * d2.x;
            if (cross != 0f) // 방향이 꺾이는 점만 코너
                outPts.Add(cur);
        }

        return outPts;
    }
}
