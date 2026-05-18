using System.Collections.Generic;
using UnityEngine;

public enum TileType { Ground, Ladder, Pushable, Door, LockedBlock, Water, Lava }

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

    public static readonly Dictionary<TileType, Color> Colors = new Dictionary<TileType, Color>
    {
        { TileType.Ground,      Color.white },
        { TileType.Ladder,      new Color(0.55f, 0.27f, 0.07f) },
        { TileType.Pushable,    Color.black },
        { TileType.Door,        Color.gray },
        { TileType.LockedBlock, Color.green },
        { TileType.Water,       new Color(0.1f, 0.45f, 1f, 0.8f) },
        { TileType.Lava,        new Color(1f, 0.25f, 0f, 0.9f) },
    };

    // Door, LockedBlock 레이어는 Project Settings > Tags and Layers에서 추가 필요
    public static readonly Dictionary<TileType, string> LayerNames = new Dictionary<TileType, string>
    {
        { TileType.Ground,      "Ground" },
        { TileType.Ladder,      "Ladder" },
        { TileType.Pushable,    "Pushable" },
        { TileType.Door,        "Door" },
        { TileType.LockedBlock, "LockedBlock" },
        { TileType.Water,       "Water" },
        { TileType.Lava,        "Lava" },
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
            _tiles.Add(new TileData { gridPos = pos, type = type, colliderSize = colliderSize });
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
