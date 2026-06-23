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

    /// <summary>
    /// 4-연결 그룹마다 외곽(CCW) 닫힌 윤곽 1개를 만든다.
    /// 각 루프는 격자 모서리 좌표의 닫힌 CCW 다각형이며, 직선 점은 합쳐지고
    /// 첫 점은 끝에서 중복되지 않는다. 내부 구멍(CW 루프)은 제외한다.
    /// </summary>
    public static List<List<Vector2>> BuildOuterLoops(IReadOnlyCollection<Vector2Int> cells)
    {
        List<List<Vector2>> result = new List<List<Vector2>>();

        foreach (List<Vector2Int> group in GroupConnected(cells))
        {
            foreach (List<Vector2> loop in BuildLoops(group))
            {
                if (SignedArea(loop) > 0f) // 외곽 루프 = CCW = 양의 넓이
                    result.Add(Collapse(loop));
            }
        }

        return result;
    }

    // 그룹의 경계 에지를 모아 닫힌 루프(들)로 잇는다. 내부가 왼쪽에 오도록 방향 부여.
    private static List<List<Vector2>> BuildLoops(List<Vector2Int> group)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(group);
        Dictionary<Vector2, Vector2> next = new Dictionary<Vector2, Vector2>();

        foreach (Vector2Int c in group)
        {
            Vector2 bl = new Vector2(c.x, c.y);
            Vector2 br = new Vector2(c.x + 1, c.y);
            Vector2 tr = new Vector2(c.x + 1, c.y + 1);
            Vector2 tl = new Vector2(c.x, c.y + 1);

            if (!set.Contains(c + Vector2Int.down))  next[bl] = br; // 아래변
            if (!set.Contains(c + Vector2Int.right)) next[br] = tr; // 오른변
            if (!set.Contains(c + Vector2Int.up))    next[tr] = tl; // 윗변
            if (!set.Contains(c + Vector2Int.left))  next[tl] = bl; // 왼변
        }

        List<List<Vector2>> loops = new List<List<Vector2>>();
        HashSet<Vector2> used = new HashSet<Vector2>();

        foreach (KeyValuePair<Vector2, Vector2> kv in next)
        {
            if (used.Contains(kv.Key)) continue;

            List<Vector2> loop = new List<Vector2>();
            Vector2 cur = kv.Key;

            while (!used.Contains(cur))
            {
                used.Add(cur);
                loop.Add(cur);
                cur = next[cur];
            }

            loops.Add(loop);
        }

        return loops;
    }

    // 직선으로 이어지는 점 제거(코너만 유지).
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

    // 신발끈 공식. CCW면 양수, CW(구멍)면 음수.
    private static float SignedArea(List<Vector2> loop)
    {
        float a = 0f;
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = loop[i];
            Vector2 q = loop[(i + 1) % n];
            a += p.x * q.y - q.x * p.y;
        }
        return a * 0.5f;
    }
}
