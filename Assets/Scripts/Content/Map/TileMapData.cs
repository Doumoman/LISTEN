using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

// 주의: enum 순서는 직렬화 정수값이므로 새 타입은 항상 끝에 추가한다.
public enum TileType { Ground, Ladder, Pushable, Door, LockedBlock, Water, Lava, MovingPlatform, FallingPlatform, Spike, Gas, Disguised, Fruit, Portal }

[System.Serializable]
public class TileData
{
    public Vector2Int gridPos;
    public TileType type;
    public Vector2 colliderSize = Vector2.one;

    // Disguised 전용: 0 = Solid(안전), 1 = Deadly(치명), 2 = Fake(허상)
    public int variant;
}

[System.Serializable]
public class CameraRoomData
{
    public string roomName;
    public Vector2Int startGridPos;
    public Vector2Int endGridPos;

    // 룸 단위 리스폰 지점(셀레스트식 체크포인트). hasSpawn 이 true 일 때만 사용.
    public bool hasSpawn;
    public Vector2Int spawnGridPos;
}

public class TileMapData : MonoBehaviour
{
    [SerializeField] private List<TileData> _tiles = new List<TileData>();
    [SerializeField] private List<CameraRoomData> _cameraRooms = new List<CameraRoomData>();

    [Header("Ground SpriteShape")]
    [SerializeField] private SpriteShape _groundProfile;        // 2D Fantasy 번들의 지형 프로필 드래그
    [SerializeField] private Vector2 _groundShapeOffset = Vector2.zero; // 비주얼-콜라이더 정렬용 전역 오프셋

    private const string WaterFluidPrefabPath = "Prefabs/Map/Fluid/WaterFluid";
    private const string LavaFluidPrefabPath = "Prefabs/Map/Fluid/LavaFluid";

    private const string MovingPlatformPrefabPath = "Prefabs/Map/MovingPlatform/MovingPlatform";
    private const string MovingPlatformPointPrefabPath = "Prefabs/Map/MovingPlatform/MovingPlatformPoint";
    private const string FallingPlatformPrefabPath = "Prefabs/Map/FallingPlatform/FallingPlatform";

    public static readonly Dictionary<TileType, Color> Colors = new Dictionary<TileType, Color>
    {
        { TileType.Ground,         Color.white },
        { TileType.Ladder,         new Color(0.55f, 0.27f, 0.07f) },
        { TileType.Pushable,       Color.black },
        { TileType.Door,           Color.gray },
        { TileType.LockedBlock,    Color.green },
        { TileType.Water,          new Color(0.1f, 0.45f, 1f, 0.8f) },
        { TileType.Lava,           new Color(1f, 0.25f, 0f, 0.9f) },
        { TileType.MovingPlatform, new Color(1f, 0.85f, 0.15f, 0.9f) },
        { TileType.FallingPlatform, new Color(0.75f, 0.45f, 0.2f, 0.9f) },
        { TileType.Spike,          new Color(0.95f, 0.2f, 0.2f, 0.9f) },
        { TileType.Gas,            new Color(0.55f, 0.85f, 0.2f, 0.55f) },
        // Disguised 는 일부러 Ground 와 동일한 흰색 — 겉모습으로 정체를 알 수 없게.
        { TileType.Disguised,      Color.white },
        { TileType.Fruit,          new Color(0.4f, 0.9f, 0.5f, 0.95f) },
        { TileType.Portal,         new Color(0.4f, 0.8f, 1f, 0.95f) },
    };

    public static readonly Dictionary<TileType, string> LayerNames = new Dictionary<TileType, string>
    {
        { TileType.Ground,         "Ground" },
        { TileType.Ladder,         "Ladder" },
        { TileType.Pushable,       "Pushable" },
        { TileType.Door,           "Door" },
        { TileType.LockedBlock,    "LockedBlock" },
        { TileType.Water,          "Water" },
        { TileType.Lava,           "Lava" },
        { TileType.MovingPlatform, "Ground" },
        { TileType.FallingPlatform, "Ground" },
        { TileType.Spike,          "Default" }, // 트리거(킬존)라 충돌 레이어와 무관
        { TileType.Gas,            "Default" },
        { TileType.Disguised,      "Ground" },  // Solid/Deadly 는 밟을 수 있어야 함
        { TileType.Fruit,          "Default" },
        { TileType.Portal,         "Default" },
    };

    public IReadOnlyList<TileData> Tiles => _tiles;
    public IReadOnlyList<CameraRoomData> CameraRooms => _cameraRooms;

    public bool HasTile(Vector2Int pos) => _tiles.Exists(t => t.gridPos == pos);

    public TileData GetTile(Vector2Int pos) => _tiles.Find(t => t.gridPos == pos);

    public void AddOrReplace(Vector2Int pos, TileType type, Vector2 colliderSize)
    {
        int idx = _tiles.FindIndex(t => t.gridPos == pos);

        if (idx >= 0)
        {
            _tiles[idx].type = type;
            _tiles[idx].colliderSize = colliderSize;
        }
        else
        {
            _tiles.Add(new TileData
            {
                gridPos = pos,
                type = type,
                colliderSize = colliderSize
            });
        }
    }

    public bool RemoveTile(Vector2Int pos)
    {
        int idx = _tiles.FindIndex(t => t.gridPos == pos);
        if (idx < 0) return false;

        _tiles.RemoveAt(idx);
        return true;
    }

    public void ClearAll() => _tiles.Clear();

    public void AddCameraRoom(Vector2Int startGridPos, Vector2Int endGridPos)
    {
        Vector2Int min = Vector2Int.Min(startGridPos, endGridPos);
        Vector2Int max = Vector2Int.Max(startGridPos, endGridPos);

        _cameraRooms.Add(new CameraRoomData
        {
            roomName = $"Room {_cameraRooms.Count + 1}",
            startGridPos = min,
            endGridPos = max
        });
    }

    public bool RemoveCameraRoomAt(Vector2Int gridPos)
    {
        int idx = _cameraRooms.FindIndex(room => ContainsGrid(room, gridPos));
        if (idx < 0) return false;

        _cameraRooms.RemoveAt(idx);
        return true;
    }

    public void ClearCameraRooms() => _cameraRooms.Clear();

    public Rect GetCameraRoomWorldRect(CameraRoomData room)
    {
        Vector2Int min = Vector2Int.Min(room.startGridPos, room.endGridPos);
        Vector2Int max = Vector2Int.Max(room.startGridPos, room.endGridPos);
        Vector3 origin = transform.position;

        float xMin = origin.x + min.x;
        float yMin = origin.y + min.y;
        float width = max.x - min.x + 1f;
        float height = max.y - min.y + 1f;

        return new Rect(xMin, yMin, width, height);
    }

    public bool TryGetCameraRoom(Vector3 worldPos, out CameraRoomData room, out Rect worldRect)
    {
        Vector2Int gridPos = WorldToGrid(worldPos);

        for (int i = 0; i < _cameraRooms.Count; i++)
        {
            CameraRoomData candidate = _cameraRooms[i];

            if (!ContainsGrid(candidate, gridPos))
                continue;

            room = candidate;
            worldRect = GetCameraRoomWorldRect(candidate);
            return true;
        }

        room = null;
        worldRect = default;
        return false;
    }

    private bool ContainsGrid(CameraRoomData room, Vector2Int gridPos)
    {
        Vector2Int min = Vector2Int.Min(room.startGridPos, room.endGridPos);
        Vector2Int max = Vector2Int.Max(room.startGridPos, room.endGridPos);

        return gridPos.x >= min.x
            && gridPos.x <= max.x
            && gridPos.y >= min.y
            && gridPos.y <= max.y;
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
        => transform.position + new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0f);

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
    }

    public bool IsFluidType(TileType type)
    {
        return type == TileType.Water || type == TileType.Lava;
    }

    public bool IsMovingPlatformType(TileType type)
    {
        return type == TileType.MovingPlatform;
    }
    public bool IsFallingPlatformType(TileType type)
    {
        return type == TileType.FallingPlatform;
    }

    #region Moving Platform 관련 메서드
    public void CreateMovingPlatform(Vector2Int gridPos, Vector2 colliderSize)
    {
        Transform parent = GetOrCreateMovingPlatformParent();

        string platformName = GetMovingPlatformName(gridPos);
        string pointAName = GetMovingPlatformPointAName(gridPos);
        string pointBName = GetMovingPlatformPointBName(gridPos);

        RemoveMovingPlatformObjects(gridPos);

        GameObject platformPrefab = Resources.Load<GameObject>(MovingPlatformPrefabPath);
        GameObject pointPrefab = Resources.Load<GameObject>(MovingPlatformPointPrefabPath);

        if (platformPrefab == null)
        {
            Debug.LogWarning(
                $"MovingPlatform prefab을 찾을 수 없습니다.\n" +
                $"필요 경로: Assets/Resources/{MovingPlatformPrefabPath}.prefab"
            );
        }

        if (pointPrefab == null)
        {
            Debug.LogWarning(
                $"MovingPlatformPoint prefab을 찾을 수 없습니다.\n" +
                $"필요 경로: Assets/Resources/{MovingPlatformPointPrefabPath}.prefab\n" +
                $"빈 GameObject로 대체 생성합니다."
            );
        }

        Vector3 center = GridToWorld(gridPos);
        Vector3 pointAPos = center;
        Vector3 pointBPos = center + Vector3.right * 3f;

        GameObject platform = CreateObjectFromResource(platformPrefab, parent, platformName);
        GameObject pointA = CreateObjectFromResource(pointPrefab, parent, pointAName);
        GameObject pointB = CreateObjectFromResource(pointPrefab, parent, pointBName);

        platform.transform.position = center;
        pointA.transform.position = pointAPos;
        pointB.transform.position = pointBPos;

        ApplyPlatformColliderSize(platform, colliderSize);

        MovingPlatformController controller = platform.GetComponent<MovingPlatformController>();

        if (controller == null)
            controller = platform.AddComponent<MovingPlatformController>();

        controller.Init(pointA.transform, pointB.transform);

        int layer = LayerMask.NameToLayer(LayerNames[TileType.MovingPlatform]);
        if (layer >= 0)
            SetLayerRecursively(platform, layer);
    }

    public void RemoveMovingPlatformObjects(Vector2Int gridPos)
    {
        Transform parent = transform.Find("MovingPlatforms");
        if (parent == null) return;

        DestroyChildIfExists(parent, GetMovingPlatformName(gridPos));
        DestroyChildIfExists(parent, GetMovingPlatformPointAName(gridPos));
        DestroyChildIfExists(parent, GetMovingPlatformPointBName(gridPos));
    }
    public bool HasMovingPlatformObject(Vector2Int gridPos)
    {
        Transform parent = transform.Find("MovingPlatforms");
        if (parent == null) return false;

        return parent.Find(GetMovingPlatformName(gridPos)) != null;
    }

    public bool TrySetMovingPlatformDestination(Vector2Int platformGridPos, Vector2Int destinationGridPos)
    {
        Transform parent = transform.Find("MovingPlatforms");

        if (parent == null)
            return false;

        Transform platform = parent.Find(GetMovingPlatformName(platformGridPos));
        Transform pointA = parent.Find(GetMovingPlatformPointAName(platformGridPos));
        Transform pointB = parent.Find(GetMovingPlatformPointBName(platformGridPos));

        if (platform == null || pointA == null || pointB == null)
        {
            TileData tile = GetTile(platformGridPos);

            if (tile == null || tile.type != TileType.MovingPlatform)
                return false;

            CreateMovingPlatform(platformGridPos, tile.colliderSize);

            parent = transform.Find("MovingPlatforms");
            if (parent == null) return false;

            platform = parent.Find(GetMovingPlatformName(platformGridPos));
            pointA = parent.Find(GetMovingPlatformPointAName(platformGridPos));
            pointB = parent.Find(GetMovingPlatformPointBName(platformGridPos));
        }

        if (platform == null || pointA == null || pointB == null)
            return false;

        pointB.position = GridToWorld(destinationGridPos);

        MovingPlatformController controller = platform.GetComponent<MovingPlatformController>();

        if (controller != null)
            controller.Init(pointA, pointB);

        return true;
    }

    public Transform GetMovingPlatformTransform(Vector2Int gridPos)
    {
        Transform parent = transform.Find("MovingPlatforms");
        if (parent == null) return null;

        return parent.Find(GetMovingPlatformName(gridPos));
    }

    public Transform GetMovingPlatformPointATransform(Vector2Int gridPos)
    {
        Transform parent = transform.Find("MovingPlatforms");
        if (parent == null) return null;

        return parent.Find(GetMovingPlatformPointAName(gridPos));
    }

    public Transform GetMovingPlatformPointBTransform(Vector2Int gridPos)
    {
        Transform parent = transform.Find("MovingPlatforms");
        if (parent == null) return null;

        return parent.Find(GetMovingPlatformPointBName(gridPos));
    }
    private void ApplyPlatformColliderSize(GameObject platform, Vector2 colliderSize)
    {
        BoxCollider2D box = platform.GetComponent<BoxCollider2D>();

        if (box == null)
            box = platform.AddComponent<BoxCollider2D>();

        box.size = colliderSize;
        box.offset = Vector2.zero;

        SpriteRenderer sr = platform.GetComponent<SpriteRenderer>();

        if (sr != null)
            platform.transform.localScale = new Vector3(colliderSize.x, colliderSize.y, 1f);
    }

    private GameObject CreateObjectFromResource(GameObject prefab, Transform parent, string objectName)
    {
        GameObject go;

#if UNITY_EDITOR
        if (!Application.isPlaying && prefab != null)
        {
            go = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

            if (go != null)
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Moving Platform Object");
        }
        else
        {
            go = prefab != null ? Instantiate(prefab, parent) : new GameObject();
        }
#else
        go = prefab != null ? Instantiate(prefab, parent) : new GameObject();
#endif

        if (go == null)
            go = new GameObject();

        go.name = objectName;
        go.transform.SetParent(parent, true);

        return go;
    }

    private void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform found = parent.Find(childName);
        if (found == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.DestroyObjectImmediate(found.gameObject);
        else
            Destroy(found.gameObject);
#else
        Destroy(found.gameObject);
#endif
    }

    private Transform GetOrCreateMovingPlatformParent()
    {
        Transform found = transform.Find("MovingPlatforms");
        if (found != null) return found;

        GameObject go = new GameObject("MovingPlatforms");
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;

        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public static string GetMovingPlatformName(Vector2Int pos)
        => $"MovingPlatform_{pos.x}_{pos.y}";

    public static string GetMovingPlatformPointAName(Vector2Int pos)
        => $"MovingPlatformPoint_{pos.x}_{pos.y}_A";

    public static string GetMovingPlatformPointBName(Vector2Int pos)
        => $"MovingPlatformPoint_{pos.x}_{pos.y}_B";

    #endregion

    #region  낙하 플랫폼 관련 메서드
    public void CreateFallingPlatform(Vector2Int gridPos, Vector2 colliderSize)
    {
        Transform parent = GetOrCreateFallingPlatformParent();

        string platformName = GetFallingPlatformName(gridPos);

        RemoveFallingPlatformObjects(gridPos);

        GameObject prefab = Resources.Load<GameObject>(FallingPlatformPrefabPath);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"FallingPlatform prefab을 찾을 수 없습니다.\n" +
                $"필요 경로: Assets/Resources/{FallingPlatformPrefabPath}.prefab"
            );
        }

        Vector3 center = GridToWorld(gridPos);

        GameObject platform = CreateObjectFromResource(prefab, parent, platformName);
        platform.transform.position = center;

        FallingPlatformController controller = platform.GetComponent<FallingPlatformController>();

        if (controller == null)
            controller = platform.AddComponent<FallingPlatformController>();

        controller.Init(colliderSize);

        int layer = LayerMask.NameToLayer(LayerNames[TileType.FallingPlatform]);

        if (layer >= 0)
            platform.layer = layer;
    }

    public void RemoveFallingPlatformObjects(Vector2Int gridPos)
    {
        Transform parent = transform.Find("FallingPlatforms");
        if (parent == null) return;

        DestroyChildIfExists(parent, GetFallingPlatformName(gridPos));
    }

    private Transform GetOrCreateFallingPlatformParent()
    {
        Transform found = transform.Find("FallingPlatforms");

        if (found != null)
            return found;

        GameObject go = new GameObject("FallingPlatforms");
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    public static string GetFallingPlatformName(Vector2Int pos)
        => $"FallingPlatform_{pos.x}_{pos.y}";
    #endregion

    #region 유체 관련 메서드
    public void RebuildFluids()
    {
        Transform parent = GetOrCreateFluidParent();

        ClearFluidObjects(parent);
        ClearDynamicFluidObjects();

        foreach (var tile in _tiles)
        {
            if (!IsFluidType(tile.type))
                continue;

            CreateFluid(tile.gridPos, tile.type, parent);
        }
    }

    private void CreateFluid(Vector2Int gridPos, TileType fluidType, Transform parent)
    {
        FluidController prefab = GetFluidPrefab(fluidType);

        if (prefab == null)
            return;

#if UNITY_EDITOR
        FluidController fluid;

        if (!Application.isPlaying)
        {
            GameObject prefabGO = prefab.gameObject;
            GameObject instanceGO = UnityEditor.PrefabUtility.InstantiatePrefab(prefabGO, parent) as GameObject;

            if (instanceGO == null)
            {
                Debug.LogWarning($"{fluidType} 프리팹 생성에 실패했습니다.");
                return;
            }

            UnityEditor.Undo.RegisterCreatedObjectUndo(instanceGO, "Create Fluid");
            fluid = instanceGO.GetComponent<FluidController>();
        }
        else
        {
            fluid = Instantiate(prefab, parent);
        }
#else
        FluidController fluid = Instantiate(prefab, parent);
#endif

        fluid.transform.position = GridToWorld(gridPos);
        fluid.name = $"{fluidType}_{gridPos.x}_{gridPos.y}";
        fluid.Init(fluidType, gridPos);
    }

    private FluidController GetFluidPrefab(TileType type)
    {
        string path = null;

        if (type == TileType.Water)
            path = WaterFluidPrefabPath;
        else if (type == TileType.Lava)
            path = LavaFluidPrefabPath;

        if (string.IsNullOrEmpty(path))
            return null;

        FluidController prefab = Resources.Load<FluidController>(path);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"Fluid prefab을 찾을 수 없습니다.\n" +
                $"필요 경로: Assets/Resources/{path}.prefab"
            );
        }

        return prefab;
    }

    private Transform GetOrCreateFluidParent()
    {
        Transform found = transform.Find("Fluids");

        if (found != null)
            return found;

        GameObject go = new GameObject("Fluids");
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void ClearFluidObjects(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child);
            else
                Destroy(child);
#else
            Destroy(child);
#endif
        }
    }

    private void ClearDynamicFluidObjects()
    {
        Transform dynamicParent = transform.Find("DynamicFluids");

        if (dynamicParent == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(dynamicParent.gameObject);
        else
            Destroy(dynamicParent.gameObject);
#else
        Destroy(dynamicParent.gameObject);
#endif
    }
    #endregion


    #region Ladder 병합 빌드

    /// <summary>
    /// 4방향 인접한 Ladder 타일들을 연결 컴포넌트(BFS)로 묶어 반환한다.
    /// </summary>
    private List<List<TileData>> GetLadderConnectedGroups()
    {
        List<TileData> ladderTiles = _tiles.FindAll(t => t.type == TileType.Ladder);

        Dictionary<Vector2Int, TileData> ladderMap = new Dictionary<Vector2Int, TileData>();
        foreach (TileData tile in ladderTiles)
            ladderMap[tile.gridPos] = tile;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<List<TileData>> groups = new List<List<TileData>>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (TileData tile in ladderTiles)
        {
            if (visited.Contains(tile.gridPos)) continue;

            List<TileData> group = new List<TileData>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(tile.gridPos);
            visited.Add(tile.gridPos);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                group.Add(ladderMap[cur]);

                foreach (Vector2Int d in dirs)
                {
                    Vector2Int nb = cur + d;
                    if (!visited.Contains(nb) && ladderMap.ContainsKey(nb))
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
    /// 연결된 Ladder 타일 그룹마다 하나의 GameObject를 생성한다.
    /// 콜라이더는 그룹의 바운딩 박스 크기의 단일 BoxCollider2D(isTrigger).
    /// RebuildAll / RebuildColliders(TileMapEditor) 양쪽에서 호출된다.
    /// </summary>
    public void BuildMergedLadders()
    {
        List<List<TileData>> groups = GetLadderConnectedGroups();
        if (groups.Count == 0) return;

        string layerName = LayerNames[TileType.Ladder];
        int layer = LayerMask.NameToLayer(layerName);
        Transform parent = GetOrCreateBuildParent("Ladder", layerName, false);

        foreach (List<TileData> group in groups)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (TileData t in group)
            {
                if (t.gridPos.x < minX) minX = t.gridPos.x;
                if (t.gridPos.y < minY) minY = t.gridPos.y;
                if (t.gridPos.x > maxX) maxX = t.gridPos.x;
                if (t.gridPos.y > maxY) maxY = t.gridPos.y;
            }

            float w = maxX - minX + 1f;
            float h = maxY - minY + 1f;

            string goName = group.Count == 1
                ? $"Tile_{group[0].gridPos.x}_{group[0].gridPos.y}"
                : $"Ladder_{minX}_{minY}_to_{maxX}_{maxY}";

            GameObject go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            // GO 원점 = 그룹 바운딩 박스의 좌하단
            go.transform.position = transform.position + new Vector3(minX, minY, 0f);

            if (layer >= 0) go.layer = layer;

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            // 바운딩 박스 전체를 하나의 트리거로
            box.offset = new Vector2(w * 0.5f, h * 0.5f);
            box.size = new Vector2(w, h);

            // 각 타일 위치에 개별 시각 스프라이트 추가
            Sprite sq = GetSquareSprite();
            if (sq != null)
            {
                Color c = Colors[TileType.Ladder];
                foreach (TileData t in group)
                {
                    GameObject vis = new GameObject("Visual");
                    vis.transform.SetParent(go.transform, false);
                    vis.transform.position = GridToWorld(t.gridPos);
                    vis.transform.localScale = new Vector3(t.colliderSize.x, t.colliderSize.y, 1f);
                    SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
                    sr.sprite = sq;
                    sr.color = c;
                    sr.sortingOrder = 5;
                }
            }
        }
    }

    #endregion

    #region 절차적 일괄 빌드 (씬 빌더 / 런타임 공용)

    private const string SquareSpritePath = "Sprites/Square";
    private static Sprite _squareSprite;

    private static readonly string[] StandardParents =
    {
        "Ground", "Ladder", "Pushable", "Door", "LockedBlock", "Water", "Lava",
        "Spikes", "GasZones", "Disguised", "Fruits", "Portals"
    };

    /// <summary>
    /// Ground 타일 그리드에서 경사 없는 닫힌 SpriteShape(시각 전용)를 생성한다.
    /// 콜라이더는 만들지 않으며(기존 Box+Composite 유지), 멱등하게 재생성한다.
    /// </summary>
    public void RebuildGroundSpriteShapes()
    {
        DestroyChildByName("GroundShapes");

        if (_groundProfile == null)
        {
            Debug.LogWarning("Ground SpriteShape Profile 이 지정되지 않았습니다. TileMapData 인스펙터의 'Ground SpriteShape > Ground Profile' 에 번들 프로필을 드래그하세요.");
            return;
        }

        List<Vector2Int> groundCells = new List<Vector2Int>();
        foreach (TileData tile in _tiles)
            if (tile.type == TileType.Ground)
                groundCells.Add(tile.gridPos);

        if (groundCells.Count == 0)
            return;

        Transform parent = new GameObject("GroundShapes").transform;
        parent.SetParent(transform, false);

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(groundCells);

        for (int i = 0; i < loops.Count; i++)
            CreateGroundShape(parent, loops[i]);
    }

    private void CreateGroundShape(Transform parent, List<Vector2> loop)
    {
        if (loop.Count < 3) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        foreach (Vector2 p in loop)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
        }

        GameObject go = new GameObject($"GroundShape_{(int)minX}_{(int)minY}");
        go.transform.SetParent(parent, false);
        // 스플라인 점은 격자 모서리(콜라이더 기준선)에 두고, 오브젝트만 오프셋만큼 이동 → 비주얼 정렬
        go.transform.localPosition = new Vector3(_groundShapeOffset.x, _groundShapeOffset.y, 0f);

        SpriteShapeController ctrl = go.AddComponent<SpriteShapeController>();
        ctrl.spriteShape = _groundProfile;
        ctrl.autoUpdateCollider = false; // 시각 전용 — 콜라이더 생성 안 함

        Spline spline = ctrl.spline;
        spline.Clear();
        for (int i = 0; i < loop.Count; i++)
        {
            spline.InsertPointAt(i, new Vector3(loop[i].x, loop[i].y, 0f));
            spline.SetTangentMode(i, ShapeTangentMode.Linear); // 직선 → 경사/곡선 0
        }
        spline.isOpenEnded = false; // 닫힌 윤곽

        SpriteShapeRenderer ssr = go.GetComponent<SpriteShapeRenderer>();
        if (ssr != null)
            ssr.sortingOrder = 6; // 기존 Ground 사각 비주얼(5)보다 위

        ctrl.RefreshSpriteShape();
    }

    /// <summary>
    /// 타일 데이터를 기반으로 콜라이더 + 가시 스프라이트 + 기믹 컴포넌트를 모두 생성한다.
    /// 새 데모 씬을 코드로 구성할 때 사용. 멱등(여러 번 호출해도 안전).
    /// </summary>
    public void RebuildAll()
    {
        ClearBuiltChildren();

        foreach (TileData tile in _tiles)
        {
            switch (tile.type)
            {
                case TileType.MovingPlatform:
                case TileType.FallingPlatform:
                    break; // 아래 플랫폼 단계에서 처리
                case TileType.Ladder:
                    break; // BuildMergedLadders() 에서 일괄 처리
                case TileType.Spike:
                    BuildSpike(tile);
                    break;
                case TileType.Gas:
                    BuildGas(tile);
                    break;
                case TileType.Disguised:
                    BuildDisguised(tile);
                    break;
                case TileType.Fruit:
                    BuildFruit(tile);
                    break;
                case TileType.Portal:
                    BuildPortal(tile);
                    break;
                default:
                    BuildStandardTile(tile);
                    break;
            }
        }

        BuildMergedLadders();

        RebuildFluids();

        foreach (TileData tile in _tiles)
        {
            if (tile.type == TileType.MovingPlatform)
                CreateMovingPlatform(tile.gridPos, tile.colliderSize);
            else if (tile.type == TileType.FallingPlatform)
                CreateFallingPlatform(tile.gridPos, tile.colliderSize);
        }

        RebuildGroundSpriteShapes();
    }

    private void ClearBuiltChildren()
    {
        foreach (string parentName in StandardParents)
            DestroyChildByName(parentName);

        DestroyChildByName("Fluids");
        DestroyChildByName("DynamicFluids");
        DestroyChildByName("MovingPlatforms");
        DestroyChildByName("FallingPlatforms");
        DestroyChildByName("GroundShapes");
    }

    private void DestroyChildByName(string childName)
    {
        Transform found = transform.Find(childName);
        if (found == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(found.gameObject);
        else
            Destroy(found.gameObject);
#else
        Destroy(found.gameObject);
#endif
    }

    private void BuildStandardTile(TileData tile)
    {
        string layerName = LayerNames[tile.type];
        bool isGround = tile.type == TileType.Ground;
        bool isTrigger = tile.type == TileType.Water || tile.type == TileType.Lava || tile.type == TileType.Ladder;

        Transform parent = GetOrCreateBuildParent(tile.type.ToString(), layerName, isGround);

        GameObject go = CreateTileObject(parent, tile, layerName);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = tile.colliderSize;
        box.offset = Vector2.zero;
        box.isTrigger = isTrigger;

        if (isGround)
            box.compositeOperation = Collider2D.CompositeOperation.Merge;

        // 사다리/유체 트리거는 부모 이름 기반 감지(PlayerFSM.IsNamedFluid)와 호환되도록 이름 유지
        // 프로필이 지정돼 있으면 Ground 시각은 SpriteShape 가 담당 → 사각 비주얼 생략(콜라이더는 유지)
        if (!(isGround && _groundProfile != null))
            AddTileVisual(go, tile.colliderSize, Colors[tile.type], 5);
    }

    private void BuildSpike(TileData tile)
    {
        Transform parent = GetOrCreateBuildParent("Spikes", "Default", false);
        GameObject go = CreateTileObject(parent, tile, "Default");

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = tile.colliderSize;
        box.offset = Vector2.zero;
        box.isTrigger = true;

        go.AddComponent<KillZone>();
        AddTileVisual(go, tile.colliderSize, Colors[TileType.Spike], 6);
    }

    private void BuildGas(TileData tile)
    {
        Transform parent = GetOrCreateBuildParent("GasZones", "Default", false);
        GameObject go = CreateTileObject(parent, tile, "Default");

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = tile.colliderSize;
        box.offset = Vector2.zero;
        box.isTrigger = true;

        GasZone gas = go.AddComponent<GasZone>();
        if (tile.variant == 1) // Pulsing
            gas.Configure(0.8f, true, 1.4f, 1.2f, 0f);

        AddTileVisual(go, tile.colliderSize, Colors[TileType.Gas], 7);
    }

    // variant: 0 = Solid, 1 = Deadly, 2 = Fake
    private void BuildDisguised(TileData tile)
    {
        bool solidLike = tile.variant != 2; // Fake 만 비-솔리드
        string layerName = solidLike ? "Ground" : "Default";

        Transform parent = GetOrCreateBuildParent("Disguised", null, false);
        GameObject go = CreateTileObject(parent, tile, layerName);

        if (tile.variant == 0) // Solid
        {
            BoxCollider2D solid = go.AddComponent<BoxCollider2D>();
            solid.size = tile.colliderSize;
            solid.offset = Vector2.zero;
        }
        else if (tile.variant == 1) // Deadly: 솔리드(밟힘) + 약간 큰 트리거(접촉 사망)
        {
            BoxCollider2D solid = go.AddComponent<BoxCollider2D>();
            solid.size = tile.colliderSize;
            solid.offset = Vector2.zero;

            BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
            trigger.size = tile.colliderSize + new Vector2(0.2f, 0.2f);
            trigger.offset = Vector2.zero;
            trigger.isTrigger = true;
        }
        else // Fake: 트리거만(통과+추락), DisguisedBlock 이 페이드 처리
        {
            BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
            trigger.size = tile.colliderSize;
            trigger.offset = Vector2.zero;
            trigger.isTrigger = true;
        }

        DisguisedBlock disguised = go.AddComponent<DisguisedBlock>();
        disguised.SetKind((DisguiseKind)tile.variant);

        // Disguised 는 의도적으로 Ground 와 동일한 흰색.
        AddTileVisual(go, tile.colliderSize, Color.white, 5);
    }

    // 미각 — 열매. variant = TasteKind (0 None,1 Antidote,2 Vision,3 Phase,4 Heat)
    private void BuildFruit(TileData tile)
    {
        Transform parent = GetOrCreateBuildParent("Fruits", "Default", false);
        GameObject go = CreateTileObject(parent, tile, "Default");

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.55f;
        col.isTrigger = true;

        TasteKind kind = (TasteKind)Mathf.Clamp(tile.variant, 0, 4);
        Fruit fruit = go.AddComponent<Fruit>();
        fruit.Configure(kind, FruitDuration(kind), true); // 데모: 재섭취 가능

        AddTileVisual(go, Vector2.one * 0.7f, FruitColor(kind), 12);
    }

    // 청각/이동 — 포탈. variant: 0 = Decoy(가짜), 1 = Real(진짜, 다음 룸으로 워프 + 소리 비콘)
    private void BuildPortal(TileData tile)
    {
        bool isReal = tile.variant == 1;

        Transform parent = GetOrCreateBuildParent("Portals", "Default", false);
        GameObject go = CreateTileObject(parent, tile, "Default");

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.7f;
        col.isTrigger = true;

        Transform target = null;
        if (isReal)
        {
            GameObject t = new GameObject("Target");
            t.transform.SetParent(go.transform, false);
            t.transform.position = GetNextRoomSpawnWorld(tile.gridPos);
            target = t.transform;

            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            go.AddComponent<SoundBeacon>();
        }

        Portal portal = go.AddComponent<Portal>();
        portal.Configure(isReal, target, false);

        AddTileVisual(go, Vector2.one * 1.1f,
            isReal ? Colors[TileType.Portal] : new Color(0.5f, 0.5f, 0.6f, 0.9f), 9);
    }

    // 포탈이 속한 룸의 '다음 룸' 스폰을 목표로(없으면 첫 룸으로 루프).
    private Vector3 GetNextRoomSpawnWorld(Vector2Int gridPos)
    {
        if (_cameraRooms.Count == 0)
            return GridToWorld(gridPos) + Vector3.right * 3f;

        int idx = _cameraRooms.FindIndex(r => ContainsGrid(r, gridPos));
        int nextIdx = idx < 0 ? 0 : (idx + 1) % _cameraRooms.Count;
        CameraRoomData next = _cameraRooms[nextIdx];

        return next.hasSpawn ? GridToWorld(next.spawnGridPos) : GridToWorld(gridPos);
    }

    private static float FruitDuration(TasteKind kind)
    {
        switch (kind)
        {
            case TasteKind.Vision: return 14f;
            case TasteKind.Antidote: return 9f;
            default: return 10f;
        }
    }

    private static Color FruitColor(TasteKind kind)
    {
        switch (kind)
        {
            case TasteKind.Antidote: return new Color(0.4f, 0.9f, 0.5f);
            case TasteKind.Vision: return new Color(1f, 0.95f, 0.4f);
            case TasteKind.Phase: return new Color(0.7f, 0.5f, 1f);
            case TasteKind.Heat: return new Color(1f, 0.55f, 0.25f);
            default: return Color.white;
        }
    }

    private GameObject CreateTileObject(Transform parent, TileData tile, string layerName)
    {
        GameObject go = new GameObject(ColliderObjectName(tile.gridPos));
        go.transform.SetParent(parent, false);
        go.transform.position = GridToWorld(tile.gridPos);

        if (!string.IsNullOrEmpty(layerName))
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
        }

        return go;
    }

    private SpriteRenderer AddTileVisual(GameObject parentGo, Vector2 size, Color color, int sortingOrder)
    {
        Sprite sprite = GetSquareSprite();
        if (sprite == null) return null;

        GameObject visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(parentGo.transform, false);
        visualGo.transform.localPosition = Vector3.zero;
        visualGo.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = visualGo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        return sr;
    }

    private Transform GetOrCreateBuildParent(string parentName, string layerName, bool ground)
    {
        Transform existing = transform.Find(parentName);
        if (existing != null) return existing;

        GameObject parentGo = new GameObject(parentName);
        parentGo.transform.SetParent(transform, false);

        if (!string.IsNullOrEmpty(layerName))
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) parentGo.layer = layer;
        }

        if (ground)
        {
            Rigidbody2D rb = parentGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            parentGo.AddComponent<CompositeCollider2D>();
        }

        return parentGo.transform;
    }

    private static Sprite GetSquareSprite()
    {
        if (_squareSprite == null)
            _squareSprite = Resources.Load<Sprite>(SquareSpritePath);

        return _squareSprite;
    }

    private static string ColliderObjectName(Vector2Int pos) => $"Tile_{pos.x}_{pos.y}";

    #endregion

    private void OnDrawGizmos()
    {
        foreach (var tile in _tiles)
        {
            Color c = Colors[tile.type];
            Vector3 center = GridToWorld(tile.gridPos);
            Vector2 sz = tile.colliderSize;

            Gizmos.color = new Color(c.r, c.g, c.b, 0.35f);
            Gizmos.DrawCube(center, new Vector3(sz.x * 0.98f, sz.y * 0.98f, 0.01f));

            Gizmos.color = c;
            Gizmos.DrawWireCube(center, new Vector3(sz.x, sz.y, 0.01f));
        }

        foreach (var room in _cameraRooms)
        {
            Rect rect = GetCameraRoomWorldRect(room);
            Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
            Vector3 size = new Vector3(rect.width, rect.height, 0.01f);

            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.12f);
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
