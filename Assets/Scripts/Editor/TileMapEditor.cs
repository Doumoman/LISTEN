using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TileMapData))]
public class TileMapEditor : Editor
{
    private TileType _selected = TileType.Ground;

    private static readonly string[] Names = { "Ground", "Ladder", "Pushable", "Door", "LockedBlock", "Hangable" };

    // ── Inspector ─────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        TileMapData map = (TileMapData)target;

        EditorGUILayout.LabelField("Tile Palette", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        int cols = 3;
        int count = Names.Length;
        for (int row = 0; row * cols < count; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < cols && row * cols + col < count; col++)
            {
                int i = row * cols + col;
                TileType t = (TileType)i;
                bool active = _selected == t;

                Color tileColor = TileMapData.Colors[t];
                Color btnColor = active
                    ? tileColor
                    : Color.Lerp(tileColor, new Color(0.25f, 0.25f, 0.25f), 0.45f);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = btnColor;

                GUIStyle style = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = active ? FontStyle.Bold : FontStyle.Normal
                };
                string label = active ? $"► {Names[i]}" : Names[i];
                if (GUILayout.Button(label, style, GUILayout.Height(26)))
                    _selected = t;

                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            $"선택: {_selected}\n" +
            "LMB = 페인트   RMB / Shift+LMB = 지우기",
            MessageType.None);

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Rebuild Colliders"))
            RebuildColliders(map);

        EditorGUILayout.Space(2);

        if (GUILayout.Button("Clear All Tiles") &&
            EditorUtility.DisplayDialog("Clear All Tiles", "모든 타일을 삭제합니다.", "삭제", "취소"))
        {
            Undo.RecordObject(target, "Clear All Tiles");
            for (int i = map.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(map.transform.GetChild(i).gameObject);
            map.ClearAll();
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"타일 수: {map.Tiles.Count}", EditorStyles.miniLabel);
    }

    // ── Scene GUI ─────────────────────────────────────────────

    private void OnSceneGUI()
    {
        TileMapData map = (TileMapData)target;
        Event e = Event.current;

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Vector2Int gridPos = map.WorldToGrid(ray.origin);

        DrawGridOverlay(map);
        DrawCursor(map, gridPos);

        bool isErase = e.shift || e.button == 1;

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            && (e.button == 0 || e.button == 1))
        {
            Undo.RecordObject(target, isErase ? "Erase Tile" : "Paint Tile");

            if (isErase)
            {
                if (map.RemoveTile(gridPos))
                    RemoveCollider(map, gridPos);
            }
            else
            {
                bool replacing = map.HasTile(gridPos);
                TileType prevType = replacing ? map.GetTile(gridPos).type : default;
                map.AddOrReplace(gridPos, _selected);

                if (!replacing || prevType != _selected)
                    UpdateCollider(map, gridPos, _selected);
            }

            EditorUtility.SetDirty(target);
            e.Use();
        }

        SceneView.RepaintAll();
    }

    // ── Gizmo helpers ─────────────────────────────────────────

    private void DrawCursor(TileMapData map, Vector2Int gridPos)
    {
        Color c = TileMapData.Colors[_selected];
        Vector3 bl = map.GridToWorld(gridPos) - new Vector3(0.5f, 0.5f, 0f);

        Handles.DrawSolidRectangleWithOutline(
            new Rect(bl.x, bl.y, 1f, 1f),
            new Color(c.r, c.g, c.b, 0.2f),
            c);
    }

    private void DrawGridOverlay(TileMapData map)
    {
        SceneView sv = SceneView.currentDrawingSceneView;
        if (sv == null || !sv.camera.orthographic) return;

        Camera cam = sv.camera;
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 camPos = cam.transform.position;
        Vector3 origin = map.transform.position;

        int x0 = Mathf.FloorToInt(camPos.x - w - origin.x) - 1;
        int x1 = Mathf.CeilToInt(camPos.x + w - origin.x) + 1;
        int y0 = Mathf.FloorToInt(camPos.y - h - origin.y) - 1;
        int y1 = Mathf.CeilToInt(camPos.y + h - origin.y) + 1;

        Handles.color = new Color(1f, 1f, 1f, 0.07f);
        for (int x = x0; x <= x1; x++)
            Handles.DrawLine(origin + new Vector3(x, y0), origin + new Vector3(x, y1));
        for (int y = y0; y <= y1; y++)
            Handles.DrawLine(origin + new Vector3(x0, y), origin + new Vector3(x1, y));
    }

    // ── Collider management ───────────────────────────────────

    private Transform GetOrCreateLayerParent(TileMapData map, TileType type, bool useUndo = true)
    {
        string parentName = type.ToString();
        Transform existing = map.transform.Find(parentName);
        if (existing != null) return existing;

        var parentGO = new GameObject(parentName);
        if (useUndo)
            Undo.RegisterCreatedObjectUndo(parentGO, "Create Layer Parent");
        parentGO.transform.SetParent(map.transform, false);

        int layer = LayerMask.NameToLayer(TileMapData.LayerNames[type]);
        if (layer >= 0) parentGO.layer = layer;

        if (type == TileType.Ground)
        {
            var rb = parentGO.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            parentGO.AddComponent<CompositeCollider2D>();
        }

        return parentGO.transform;
    }

    private void UpdateCollider(TileMapData map, Vector2Int pos, TileType type)
    {
        RemoveCollider(map, pos);

        Transform layerParent = GetOrCreateLayerParent(map, type);

        var go = new GameObject(ColliderName(pos));
        Undo.RegisterCreatedObjectUndo(go, "Create Tile Collider");
        go.transform.SetParent(layerParent);
        go.transform.position = map.GridToWorld(pos);

        var box = go.AddComponent<BoxCollider2D>();
        if (type == TileType.Ground)
            box.compositeOperation = Collider2D.CompositeOperation.Merge;

        int layer = LayerMask.NameToLayer(TileMapData.LayerNames[type]);
        if (layer >= 0) go.layer = layer;
    }

    private void RemoveCollider(TileMapData map, Vector2Int pos)
    {
        string name = ColliderName(pos);
        foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
        {
            Transform layerParent = map.transform.Find(type.ToString());
            if (layerParent == null) continue;
            Transform found = layerParent.Find(name);
            if (found != null)
            {
                Undo.DestroyObjectImmediate(found.gameObject);
                return;
            }
        }
        // 레거시: 레이어 부모 없이 직접 붙어있던 콜라이더 처리
        Transform legacy = map.transform.Find(name);
        if (legacy != null)
            Undo.DestroyObjectImmediate(legacy.gameObject);
    }

    private void RebuildColliders(TileMapData map)
    {
        for (int i = map.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(map.transform.GetChild(i).gameObject);

        foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
            GetOrCreateLayerParent(map, type, useUndo: false);

        foreach (var tile in map.Tiles)
        {
            Transform layerParent = map.transform.Find(tile.type.ToString());
            var go = new GameObject(ColliderName(tile.gridPos));
            go.transform.SetParent(layerParent);
            go.transform.position = map.GridToWorld(tile.gridPos);

            var box = go.AddComponent<BoxCollider2D>();
            if (tile.type == TileType.Ground)
                box.compositeOperation = Collider2D.CompositeOperation.Merge;

            int layer = LayerMask.NameToLayer(TileMapData.LayerNames[tile.type]);
            if (layer >= 0) go.layer = layer;
        }
        EditorUtility.SetDirty(map);
    }

    private static string ColliderName(Vector2Int pos) => $"Tile_{pos.x}_{pos.y}";
}
