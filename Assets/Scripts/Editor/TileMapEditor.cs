using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TileMapData))]
public class TileMapEditor : Editor
{
    private enum EditMode
    {
        Tiles,
        CameraRooms
    }

    private EditMode _editMode = EditMode.Tiles;
    private TileType _selected = TileType.Ground;
    private Vector2 _colliderSize = Vector2.one;

    private bool _hasSelectedMovingPlatform;
    private Vector2Int _selectedMovingPlatformGrid;
    private bool _hasCameraRoomStart;
    private Vector2Int _cameraRoomStartGrid;

    private static readonly string[] Names =
    {
        "Ground",
        "Ladder",
        "Pushable",
        "Door",
        "LockedBlock",
        "Water",
        "Lava",
        "MovingPlatform",
        "FallingPlatform",
        "Spike",
        "Gas",
        "Disguised",
        "Fruit",
        "Portal"
    };

    private int _variant;

    private static readonly string[] DisguisedVariants = { "Solid(안전)", "Deadly(치명)", "Fake(허상)" };
    private static readonly string[] FruitVariants = { "None", "Antidote(해독)", "Vision(광명)", "Phase(위상)", "Heat(작열)" };
    private static readonly string[] PortalVariants = { "Decoy(가짜)", "Real(진짜)" };
    private static readonly string[] GasVariants = { "Static(상시)", "Pulsing(맥동)" };

    private static bool UsesVariant(TileType t)
        => t == TileType.Disguised || t == TileType.Fruit || t == TileType.Portal || t == TileType.Gas;

    public override void OnInspectorGUI()
    {
        TileMapData map = (TileMapData)target;

        _editMode = (EditMode)GUILayout.Toolbar((int)_editMode, new[] { "Tiles", "Camera Rooms" });
        EditorGUILayout.Space(8);

        if (_editMode == EditMode.CameraRooms)
        {
            DrawCameraRoomInspector(map);
            return;
        }

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
        EditorGUILayout.LabelField("Collider Size", EditorStyles.boldLabel);

        _colliderSize = EditorGUILayout.Vector2Field("W x H", _colliderSize);
        _colliderSize.x = Mathf.Max(0.1f, _colliderSize.x);
        _colliderSize.y = Mathf.Max(0.1f, _colliderSize.y);

        if (UsesVariant(_selected))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Variant (종류)", EditorStyles.boldLabel);

            string[] opts = _selected == TileType.Disguised ? DisguisedVariants
                          : _selected == TileType.Fruit ? FruitVariants
                          : _selected == TileType.Gas ? GasVariants
                          : PortalVariants;

            _variant = Mathf.Clamp(_variant, 0, opts.Length - 1);
            _variant = EditorGUILayout.Popup(_variant, opts);
        }

        EditorGUILayout.Space(4);

        if (_selected == TileType.MovingPlatform)
        {
            string selectedInfo = _hasSelectedMovingPlatform
                ? $"선택된 플랫폼: {_selectedMovingPlatformGrid}"
                : "선택된 플랫폼 없음";

            EditorGUILayout.HelpBox(
                $"선택: {_selected}  |  크기: {_colliderSize.x:F1} x {_colliderSize.y:F1}\n" +
                $"{selectedInfo}\n\n" +
                "LMB = MovingPlatform 생성 / 선택\n" +
                "RMB = 선택된 MovingPlatform의 목적지 지정\n" +
                "Shift + LMB = 지우기",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"선택: {_selected}  |  크기: {_colliderSize.x:F1} x {_colliderSize.y:F1}\n" +
                "LMB = 페인트   RMB / Shift+LMB = 지우기",
                MessageType.None);
        }

        EditorGUILayout.Space(4);

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 0.85f, 1f);
        if (GUILayout.Button("★ Rebuild All (콜라이더 + 기믹 일괄)", GUILayout.Height(28)))
            map.RebuildAll();
        GUI.backgroundColor = prevBg;

        EditorGUILayout.HelpBox(
            "Spike / Gas / Disguised / Fruit / Portal 같은 기믹은 페인트 후\n" +
            "위의 ★ Rebuild All 을 눌러야 실제 오브젝트로 생성됩니다.",
            MessageType.None);

        EditorGUILayout.Space(2);

        if (GUILayout.Button("Rebuild Colliders"))
            RebuildColliders(map);

        if (GUILayout.Button("Rebuild Ground SpriteShape"))
            map.RebuildGroundSpriteShapes();

        if (GUILayout.Button("Rebuild Fluids"))
            map.RebuildFluids();

        if (GUILayout.Button("Rebuild Moving Platforms"))
            RebuildMovingPlatforms(map);

        if (GUILayout.Button("Rebuild Falling Platforms"))
            RebuildFallingPlatforms(map);

        EditorGUILayout.Space(2);

        if (GUILayout.Button("Clear All Tiles") &&
            EditorUtility.DisplayDialog("Clear All Tiles", "모든 타일을 삭제합니다.", "삭제", "취소"))
        {
            Undo.RecordObject(target, "Clear All Tiles");

            for (int i = map.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(map.transform.GetChild(i).gameObject);

            map.ClearAll();
            _hasSelectedMovingPlatform = false;

            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"타일 수: {map.Tiles.Count}", EditorStyles.miniLabel);
    }

    private void DrawCameraRoomInspector(TileMapData map)
    {
        EditorGUILayout.LabelField("Camera Rooms", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scene 뷰에서 카메라가 비출 화면 단위 영역을 지정합니다.\n" +
            "런타임 카메라는 방 안에서 플레이어를 따라가되, 룸 밖을 비추지 않도록 제한됩니다.\n" +
            "다른 룸으로 넘어가면 잠깐 시간이 멈추고 카메라가 다음 룸으로 전환됩니다.\n\n" +
            "Camera Room은 카메라가 비출 수 있는 전체 영역입니다.\n" +
            "실제 화면 크기는 Main Camera의 CameraController > Visible Area에서 조절하세요.\n" +
            "2560x1440 기준이면 Reference Resolution을 2560 x 1440으로 두고 Visible Area World Height를 조절하면 됩니다.\n\n" +
            "LMB = 시작 지점 지정\n" +
            "RMB = 끝 지점 지정 후 영역 생성\n" +
            "Shift + LMB = 커서 위치의 영역 삭제",
            MessageType.Info);

        string startText = _hasCameraRoomStart
            ? $"시작 지점: {_cameraRoomStartGrid}"
            : "시작 지점 없음";

        EditorGUILayout.LabelField(startText, EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(!_hasCameraRoomStart))
        {
            if (GUILayout.Button("Cancel Start Point"))
                _hasCameraRoomStart = false;
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Clear Camera Rooms") &&
            EditorUtility.DisplayDialog("Clear Camera Rooms", "모든 카메라 영역을 삭제합니다.", "삭제", "취소"))
        {
            Undo.RecordObject(target, "Clear Camera Rooms");
            map.ClearCameraRooms();
            _hasCameraRoomStart = false;
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"카메라 영역 수: {map.CameraRooms.Count}", EditorStyles.miniLabel);

        for (int i = 0; i < map.CameraRooms.Count; i++)
        {
            CameraRoomData room = map.CameraRooms[i];
            Rect rect = map.GetCameraRoomWorldRect(room);

            EditorGUILayout.LabelField(
                $"{i + 1}. {room.roomName}",
                $"{room.startGridPos} -> {room.endGridPos} / {rect.width:F0} x {rect.height:F0}");
        }
    }

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

        if (_editMode == EditMode.CameraRooms)
        {
            DrawCameraRooms(map);
            DrawCameraRoomCursor(map, gridPos);
            HandleCameraRoomInput(map, e, gridPos);
            SceneView.RepaintAll();
            return;
        }

        DrawCursor(map, gridPos);
        DrawSelectedMovingPlatformGuide(map, gridPos);

        if (_selected == TileType.MovingPlatform)
            HandleMovingPlatformInput(map, e, gridPos);
        else
            HandleDefaultTileInput(map, e, gridPos);

        SceneView.RepaintAll();
    }

    private void HandleCameraRoomInput(TileMapData map, Event e, Vector2Int gridPos)
    {
        if (e.type != EventType.MouseDown)
            return;

        if (e.shift && e.button == 0)
        {
            Undo.RecordObject(target, "Remove Camera Room");

            if (map.RemoveCameraRoomAt(gridPos))
            {
                EditorUtility.SetDirty(target);
                _hasCameraRoomStart = false;
            }

            e.Use();
            return;
        }

        if (e.button == 0)
        {
            _cameraRoomStartGrid = gridPos;
            _hasCameraRoomStart = true;
            e.Use();
            return;
        }

        if (e.button == 1)
        {
            if (!_hasCameraRoomStart)
            {
                Debug.LogWarning("카메라 영역의 시작 지점을 먼저 좌클릭으로 지정하세요.");
                e.Use();
                return;
            }

            Undo.RecordObject(target, "Add Camera Room");
            map.AddCameraRoom(_cameraRoomStartGrid, gridPos);
            _hasCameraRoomStart = false;
            EditorUtility.SetDirty(target);
            e.Use();
        }
    }

    private void DrawCameraRooms(TileMapData map)
    {
        for (int i = 0; i < map.CameraRooms.Count; i++)
        {
            CameraRoomData room = map.CameraRooms[i];
            Rect rect = map.GetCameraRoomWorldRect(room);

            Color fill = new Color(0.1f, 0.65f, 1f, 0.12f);
            Color outline = new Color(0.1f, 0.75f, 1f, 0.95f);

            Handles.DrawSolidRectangleWithOutline(rect, fill, outline);
            Handles.Label(
                new Vector3(rect.xMin + 0.25f, rect.yMax - 0.75f, 0f),
                $"{i + 1}. {room.roomName} ({rect.width:F0}x{rect.height:F0})");
        }
    }

    private void DrawCameraRoomCursor(TileMapData map, Vector2Int gridPos)
    {
        Rect cursorRect = GetGridRect(map, gridPos, gridPos);

        Handles.DrawSolidRectangleWithOutline(
            cursorRect,
            new Color(0.1f, 0.8f, 1f, 0.12f),
            new Color(0.1f, 0.9f, 1f, 0.95f));

        if (!_hasCameraRoomStart)
            return;

        Rect preview = GetGridRect(map, _cameraRoomStartGrid, gridPos);

        Handles.DrawSolidRectangleWithOutline(
            preview,
            new Color(0.1f, 0.9f, 1f, 0.18f),
            new Color(1f, 0.9f, 0.1f, 0.95f));

        Vector3 startWorld = new Vector3(preview.xMin, preview.yMin, 0f);
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(map.transform.position + new Vector3(_cameraRoomStartGrid.x, _cameraRoomStartGrid.y, 0f), Vector3.forward, 0.18f);
        Handles.Label(startWorld + Vector3.up * 0.25f, "Start");
        Handles.Label(new Vector3(preview.xMax, preview.yMax, 0f) + Vector3.up * 0.25f, "RMB to Create");
    }

    private Rect GetGridRect(TileMapData map, Vector2Int a, Vector2Int b)
    {
        Vector2Int min = Vector2Int.Min(a, b);
        Vector2Int max = Vector2Int.Max(a, b);
        Vector3 origin = map.transform.position;

        return new Rect(
            origin.x + min.x,
            origin.y + min.y,
            max.x - min.x + 1f,
            max.y - min.y + 1f);
    }

    private void HandleMovingPlatformInput(TileMapData map, Event e, Vector2Int gridPos)
    {
        bool shiftErase = e.shift && e.button == 0;

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && shiftErase)
        {
            Undo.RecordObject(target, "Erase Moving Platform");

            TileData prevTile = map.HasTile(gridPos) ? map.GetTile(gridPos) : null;

            if (map.RemoveTile(gridPos))
            {
                if (prevTile != null && prevTile.type == TileType.MovingPlatform)
                {
                    map.RemoveMovingPlatformObjects(gridPos);

                    if (_hasSelectedMovingPlatform && _selectedMovingPlatformGrid == gridPos)
                        _hasSelectedMovingPlatform = false;
                }
                else if (prevTile != null && prevTile.type == TileType.FallingPlatform)
                {
                    map.RemoveFallingPlatformObjects(gridPos);
                }
                else
                {
                    RemoveCollider(map, gridPos);
                }
            }

            EditorUtility.SetDirty(target);
            e.Use();
            return;
        }

        if (e.type != EventType.MouseDown)
            return;

        if (e.button == 0)
        {
            Undo.RecordObject(target, "Create Or Select Moving Platform");

            bool replacing = map.HasTile(gridPos);
            TileType prevType = replacing ? map.GetTile(gridPos).type : default;

            if (replacing && prevType == TileType.MovingPlatform)
            {
                _selectedMovingPlatformGrid = gridPos;
                _hasSelectedMovingPlatform = true;

                if (!map.HasMovingPlatformObject(gridPos))
                    map.CreateMovingPlatform(gridPos, map.GetTile(gridPos).colliderSize);
            }
            else
            {
                if (replacing)
                    RemoveCollider(map, gridPos);

                map.AddOrReplace(gridPos, TileType.MovingPlatform, _colliderSize);
                map.CreateMovingPlatform(gridPos, _colliderSize);

                _selectedMovingPlatformGrid = gridPos;
                _hasSelectedMovingPlatform = true;
            }

            EditorUtility.SetDirty(target);
            e.Use();
            return;
        }

        if (e.button == 1)
        {
            if (!_hasSelectedMovingPlatform)
            {
                Debug.LogWarning("목적지를 지정할 MovingPlatform을 먼저 좌클릭으로 선택하거나 생성하세요.");
                e.Use();
                return;
            }

            Undo.RecordObject(target, "Set Moving Platform Destination");

            bool success = map.TrySetMovingPlatformDestination(
                _selectedMovingPlatformGrid,
                gridPos
            );

            if (!success)
                Debug.LogWarning("MovingPlatform 목적지 지정에 실패했습니다.");

            EditorUtility.SetDirty(target);
            e.Use();
        }
    }

    private void HandleDefaultTileInput(TileMapData map, Event e, Vector2Int gridPos)
    {
        bool isErase = e.shift || e.button == 1;

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            && (e.button == 0 || e.button == 1))
        {
            Undo.RecordObject(target, isErase ? "Erase Tile" : "Paint Tile");

            if (isErase)
            {
                TileData prevTile = map.HasTile(gridPos) ? map.GetTile(gridPos) : null;

                if (map.RemoveTile(gridPos))
                {
                    if (prevTile != null && prevTile.type == TileType.MovingPlatform)
                        map.RemoveMovingPlatformObjects(gridPos);
                    else if (prevTile != null && prevTile.type == TileType.FallingPlatform)
                        map.RemoveFallingPlatformObjects(gridPos);
                    else
                        RemoveCollider(map, gridPos);
                }
            }
            else
            {
                bool replacing = map.HasTile(gridPos);
                TileType prevType = replacing ? map.GetTile(gridPos).type : default;

                if (replacing)
                    RemoveCollider(map, gridPos);

                map.AddOrReplace(gridPos, _selected, _colliderSize);

                if (UsesVariant(_selected))
                {
                    TileData painted = map.GetTile(gridPos);
                    if (painted != null) painted.variant = _variant;
                }

                UpdateCollider(map, gridPos, _selected, _colliderSize);
            }

            EditorUtility.SetDirty(target);
            e.Use();
        }
    }

    private void DrawCursor(TileMapData map, Vector2Int gridPos)
    {
        Color c = TileMapData.Colors[_selected];
        Vector3 center = map.GridToWorld(gridPos);
        float rx = center.x - _colliderSize.x * 0.5f;
        float ry = center.y - _colliderSize.y * 0.5f;

        Handles.DrawSolidRectangleWithOutline(
            new Rect(rx, ry, _colliderSize.x, _colliderSize.y),
            new Color(c.r, c.g, c.b, 0.2f),
            c);
    }

    private void DrawSelectedMovingPlatformGuide(TileMapData map, Vector2Int hoverGridPos)
    {
        if (_selected != TileType.MovingPlatform)
            return;

        if (!_hasSelectedMovingPlatform)
            return;

        Transform pointA = map.GetMovingPlatformPointATransform(_selectedMovingPlatformGrid);
        Transform pointB = map.GetMovingPlatformPointBTransform(_selectedMovingPlatformGrid);

        if (pointA == null || pointB == null)
            return;

        Handles.color = Color.yellow;
        Handles.DrawLine(pointA.position, pointB.position);

        Handles.color = Color.green;
        Handles.DrawWireDisc(pointA.position, Vector3.forward, 0.25f);
        Handles.Label(pointA.position + Vector3.up * 0.25f, "Start A");

        Handles.color = Color.red;
        Handles.DrawWireDisc(pointB.position, Vector3.forward, 0.25f);
        Handles.Label(pointB.position + Vector3.up * 0.25f, "Destination B");

        Vector3 hoverWorld = map.GridToWorld(hoverGridPos);

        Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Handles.DrawDottedLine(pointA.position, hoverWorld, 4f);
        Handles.DrawWireDisc(hoverWorld, Vector3.forward, 0.2f);
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

    private Transform GetOrCreateLayerParent(TileMapData map, TileType type, bool useUndo = true)
    {
        string parentName = type.ToString();
        Transform existing = map.transform.Find(parentName);
        if (existing != null) return existing;

        GameObject parentGO = new GameObject(parentName);

        if (useUndo)
            Undo.RegisterCreatedObjectUndo(parentGO, "Create Layer Parent");

        parentGO.transform.SetParent(map.transform, false);

        int layer = LayerMask.NameToLayer(TileMapData.LayerNames[type]);

        if (layer >= 0)
            parentGO.layer = layer;

        if (type == TileType.Ground)
        {
            Rigidbody2D rb = parentGO.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            parentGO.AddComponent<CompositeCollider2D>();
        }

        return parentGO.transform;
    }

    private void UpdateCollider(TileMapData map, Vector2Int pos, TileType type, Vector2 colliderSize)
    {
        RemoveCollider(map, pos);

        // 감각 기믹(Spike/Gas/Disguised/Fruit/Portal)은 데이터만 저장하고
        // 실제 오브젝트는 ★ Rebuild All(map.RebuildAll)에서 일괄 생성한다.
        if (type == TileType.Spike || type == TileType.Gas || type == TileType.Disguised
            || type == TileType.Fruit || type == TileType.Portal)
            return;

        if (type == TileType.MovingPlatform)
        {
            map.CreateMovingPlatform(pos, colliderSize);
            return;
        }

        if (type == TileType.FallingPlatform)
        {
            map.CreateFallingPlatform(pos, colliderSize);
            return;
        }

        Transform layerParent = GetOrCreateLayerParent(map, type);

        GameObject go = new GameObject(ColliderName(pos));
        Undo.RegisterCreatedObjectUndo(go, "Create Tile Collider");

        go.transform.SetParent(layerParent);
        go.transform.position = map.GridToWorld(pos) - new Vector3(0.5f, 0.5f, 0f);

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.offset = new Vector2(0.5f, 0.5f);
        box.size = colliderSize;

        if (type == TileType.Water || type == TileType.Lava || type == TileType.Ladder)
        {
            box.isTrigger = true;
        }
        else if (type == TileType.Ground)
        {
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        if (TileMapData.LayerNames.TryGetValue(type, out string layerName))
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
                go.layer = layer;
        }
    }

    private void RemoveCollider(TileMapData map, Vector2Int pos)
    {
        map.RemoveMovingPlatformObjects(pos);
        map.RemoveFallingPlatformObjects(pos);

        string name = ColliderName(pos);

        foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
        {
            if (type == TileType.MovingPlatform || type == TileType.FallingPlatform)
                continue;

            Transform layerParent = map.transform.Find(type.ToString());
            if (layerParent == null) continue;

            Transform found = layerParent.Find(name);

            if (found != null)
            {
                Undo.DestroyObjectImmediate(found.gameObject);
                return;
            }
        }

        Transform legacy = map.transform.Find(name);

        if (legacy != null)
            Undo.DestroyObjectImmediate(legacy.gameObject);
    }

    private void RebuildColliders(TileMapData map)
    {
        ClearTileColliderParents(map);

        foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
        {
            if (type == TileType.MovingPlatform || type == TileType.FallingPlatform)
                continue;

            GetOrCreateLayerParent(map, type, useUndo: false);
        }

        foreach (TileData tile in map.Tiles)
        {
            if (tile.type == TileType.MovingPlatform || tile.type == TileType.FallingPlatform)
                continue;

            // Ladder는 BuildMergedLadders() 에서 연결 그룹 단위로 일괄 처리
            if (tile.type == TileType.Ladder)
                continue;

            Transform layerParent = map.transform.Find(tile.type.ToString());

            GameObject go = new GameObject(ColliderName(tile.gridPos));
            go.transform.SetParent(layerParent);
            go.transform.position = map.GridToWorld(tile.gridPos) - new Vector3(0.5f, 0.5f, 0f);

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(0.5f, 0.5f);
            box.size = tile.colliderSize;

            if (tile.type == TileType.Water || tile.type == TileType.Lava)
            {
                box.isTrigger = true;
            }
            else if (tile.type == TileType.Ground)
            {
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            int layer = LayerMask.NameToLayer(TileMapData.LayerNames[tile.type]);

            if (layer >= 0)
                go.layer = layer;
        }

        map.BuildMergedLadders();

        EditorUtility.SetDirty(map);
    }

    private void RebuildMovingPlatforms(TileMapData map)
    {
        foreach (TileData tile in map.Tiles)
        {
            if (tile.type != TileType.MovingPlatform)
                continue;

            map.CreateMovingPlatform(tile.gridPos, tile.colliderSize);
        }

        EditorUtility.SetDirty(map);
    }

    private void RebuildFallingPlatforms(TileMapData map)
    {
        foreach (TileData tile in map.Tiles)
        {
            if (tile.type != TileType.FallingPlatform)
                continue;

            map.CreateFallingPlatform(tile.gridPos, tile.colliderSize);
        }

        EditorUtility.SetDirty(map);
    }

    private void ClearTileColliderParents(TileMapData map)
    {
        foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
        {
            if (type == TileType.MovingPlatform || type == TileType.FallingPlatform)
                continue;

            Transform parent = map.transform.Find(type.ToString());

            if (parent != null)
                DestroyImmediate(parent.gameObject);
        }
    }

    private static string ColliderName(Vector2Int pos)
        => $"Tile_{pos.x}_{pos.y}";
}
