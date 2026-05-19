using System.Collections.Generic;
using UnityEngine;

public enum TileType { Ground, Ladder, Pushable, Door, LockedBlock, Water, Lava, MovingPlatform, FallingPlatform }

[System.Serializable]
public class TileData
{
    public Vector2Int gridPos;
    public TileType type;
    public Vector2 colliderSize = Vector2.one;
}

public class TileMapData : MonoBehaviour
{
    [SerializeField] private List<TileData> _tiles = new List<TileData>();

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
    };

    public IReadOnlyList<TileData> Tiles => _tiles;

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
    }
}