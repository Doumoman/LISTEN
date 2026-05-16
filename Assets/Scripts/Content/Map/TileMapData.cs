using System.Collections.Generic;
using UnityEngine;

public enum TileType { Ground, Ladder, Pushable, Door, LockedBlock }

[System.Serializable]
public class TileData
{
    public Vector2Int gridPos;
    public TileType type;
}

public class TileMapData : MonoBehaviour
{
    [SerializeField] private List<TileData> _tiles = new List<TileData>();

    public static readonly Dictionary<TileType, Color> Colors = new Dictionary<TileType, Color>
    {
        { TileType.Ground,      Color.white },
        { TileType.Ladder,      new Color(0.55f, 0.27f, 0.07f) },
        { TileType.Pushable,    Color.black },
        { TileType.Door,        Color.gray },
        { TileType.LockedBlock, Color.green },
    };

    // Door, LockedBlock 레이어는 Project Settings > Tags and Layers에서 추가 필요
    public static readonly Dictionary<TileType, string> LayerNames = new Dictionary<TileType, string>
    {
        { TileType.Ground,      "Ground" },
        { TileType.Ladder,      "Ladder" },
        { TileType.Pushable,    "Pushable" },
        { TileType.Door,        "Door" },
        { TileType.LockedBlock, "LockedBlock" },
    };

    public IReadOnlyList<TileData> Tiles => _tiles;

    public bool HasTile(Vector2Int pos) => _tiles.Exists(t => t.gridPos == pos);

    public TileData GetTile(Vector2Int pos) => _tiles.Find(t => t.gridPos == pos);

    public void AddOrReplace(Vector2Int pos, TileType type)
    {
        int idx = _tiles.FindIndex(t => t.gridPos == pos);
        if (idx >= 0)
            _tiles[idx].type = type;
        else
            _tiles.Add(new TileData { gridPos = pos, type = type });
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

    private void OnDrawGizmos()
    {
        foreach (var tile in _tiles)
        {
            Color c = Colors[tile.type];
            Vector3 center = GridToWorld(tile.gridPos);

            Gizmos.color = new Color(c.r, c.g, c.b, 0.35f);
            Gizmos.DrawCube(center, new Vector3(0.98f, 0.98f, 0.01f));

            Gizmos.color = c;
            Gizmos.DrawWireCube(center, new Vector3(1f, 1f, 0.01f));
        }
    }
}
