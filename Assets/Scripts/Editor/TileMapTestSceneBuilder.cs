using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5감각(촉/시/청/후/미) 데모 씬을 코드로 생성하는 에디터 도구.
/// 모든 기믹(스파이크/가스/위장/열매/포탈)은 TileMapData 의 타일로 배치되고,
/// 시각은 PlayerVision(플레이어 중심 원형 시야 + 화면 어둠)으로 처리한다.
/// 메뉴: Tools/TileMapTest/...
/// </summary>
public static class TileMapTestSceneBuilder
{
    private const string SceneFolder = "Assets/Scenes/TileMapTest";
    private const string PlayerPrefabPath = "Assets/Resources/Prefabs/Player.prefab";

    private const int RW = 28; // 룸 가로
    private const int RH = 15; // 룸 세로

    // variant 약칭
    private const int DSolid = 0, DDeadly = 1, DFake = 2;             // Disguised
    private const int GStatic = 0, GPulse = 1;                        // Gas
    private const int PDecoy = 0, PReal = 1;                          // Portal
    private const int FAntidote = 1, FVision = 2, FPhase = 3;         // Fruit(TasteKind)

    [MenuItem("Tools/TileMapTest/Build All Demo Scenes")]
    public static void BuildAll()
    {
        EnsureSprites();
        EnsureSceneFolder();

        List<string> built = new List<string>
        {
            BuildScene1_TouchSight(),
            BuildScene2_SmellTaste(),
            BuildScene3_HearingSight(),
            BuildScene4_TouchSmellTaste(),
            BuildScene5_AllSenses()
        };

        AddToBuildSettings(built);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TileMapTest] 데모 씬 {built.Count}개 생성 완료:\n - " + string.Join("\n - ", built));
    }

    [MenuItem("Tools/TileMapTest/1. Touch + Sight")]
    public static void Menu1() { EnsureSprites(); EnsureSceneFolder(); BuildScene1_TouchSight(); }
    [MenuItem("Tools/TileMapTest/2. Smell + Taste")]
    public static void Menu2() { EnsureSprites(); EnsureSceneFolder(); BuildScene2_SmellTaste(); }
    [MenuItem("Tools/TileMapTest/3. Hearing + Sight")]
    public static void Menu3() { EnsureSprites(); EnsureSceneFolder(); BuildScene3_HearingSight(); }
    [MenuItem("Tools/TileMapTest/4. Touch + Smell + Taste")]
    public static void Menu4() { EnsureSprites(); EnsureSceneFolder(); BuildScene4_TouchSmellTaste(); }
    [MenuItem("Tools/TileMapTest/5. All Senses")]
    public static void Menu5() { EnsureSprites(); EnsureSceneFolder(); BuildScene5_AllSenses(); }

    // ─────────────────────────────────────────────────────────────
    // Scene 1 — 촉각 + 시각 : 어둠 속 위장 다리 / 보이지 않는 디딤돌
    // ─────────────────────────────────────────────────────────────
    private static string BuildScene1_TouchSight()
    {
        Ctx c = NewScene("1_Touch_Sight");
        c.Vision(4.2f); // 화면 시야 축소

        // Room A (촉각): 위장 블록 다리 — 닿아야 안전/치명/허상을 안다
        int aL = 0;
        c.Room(aL, aL + 2, 3);
        c.FloorSpan(aL, 7, 0); c.FloorSpan(aL, 7, 1);
        c.FloorSpan(20, aL + RW - 1, 0); c.FloorSpan(20, aL + RW - 1, 1);
        c.Wall(aL, 2, RH);
        int[] kindsA = { DSolid, DSolid, DDeadly, DSolid, DFake, DSolid, DSolid, DDeadly, DSolid, DFake, DSolid, DSolid, DSolid };
        for (int i = 0; i < kindsA.Length; i++) c.Disguised(8 + i, 1, kindsA[i]);
        c.Fruit(4, 4, FVision); // 광명: 정체를 색으로 + 시야 확대

        // Room B (시각): 어둠 속 디딤돌(보통 발판) + 숨은 가시
        int bL = RW;
        c.Room(bL, bL + 2, 3);
        c.FloorSpan(bL, bL + 5, 0); c.FloorSpan(bL, bL + 5, 1);
        c.FloorSpan(bL + 22, bL + RW - 1, 0); c.FloorSpan(bL + 22, bL + RW - 1, 1);
        c.Wall(bL + RW - 1, 2, RH);
        // 디딤돌(바닥 높이 y=1) — 어둠이라 가까이 가야 보임
        c.GroundRect(bL + 8, 1, bL + 9, 1);
        c.GroundRect(bL + 12, 1, bL + 13, 1);
        c.GroundRect(bL + 16, 1, bL + 17, 1);
        c.GroundRect(bL + 20, 1, bL + 21, 1);
        // 숨은 가시
        c.Spike(bL + 3, 2);
        c.Spike(bL + 24, 2);

        c.Goal(bL + RW - 2, 3);
        return Finish(c);
    }

    // ─────────────────────────────────────────────────────────────
    // Scene 2 — 후각 + 미각 : 독가스 회랑과 해독 열매
    // ─────────────────────────────────────────────────────────────
    private static string BuildScene2_SmellTaste()
    {
        Ctx c = NewScene("2_Smell_Taste");

        // Room A: 가스 장벽 — 해독 열매로 통과
        int aL = 0;
        c.Room(aL, aL + 2, 3);
        c.FloorSpan(aL, aL + RW - 1, 0); c.FloorSpan(aL, aL + RW - 1, 1);
        c.Wall(aL, 2, RH);
        c.Fruit(5, 3, FAntidote);
        for (int x = 12; x <= 15; x++)
            for (int y = 2; y <= 7; y++)
                c.Gas(x, y, GStatic);

        // Room B: 맥동 가스 구덩이 — 타이밍 점프
        int bL = RW;
        c.Room(bL, bL + 2, 3);
        c.FloorSpan(bL, bL + 4, 0); c.FloorSpan(bL, bL + 4, 1);
        c.FloorSpan(bL + 22, bL + RW - 1, 0); c.FloorSpan(bL + 22, bL + RW - 1, 1);
        c.Wall(bL + RW - 1, 2, RH);
        c.GroundRect(bL + 9, 1, bL + 10, 1);
        c.GroundRect(bL + 15, 1, bL + 16, 1);
        // 디딤돌 사이를 채우는 맥동 가스
        for (int y = 2; y <= 5; y++) { c.Gas(bL + 6, y, GPulse); c.Gas(bL + 7, y, GPulse); }
        for (int y = 2; y <= 5; y++) { c.Gas(bL + 12, y, GPulse); c.Gas(bL + 13, y, GPulse); }
        for (int y = 2; y <= 5; y++) { c.Gas(bL + 18, y, GPulse); c.Gas(bL + 19, y, GPulse); }
        c.Fruit(bL + 2, 3, FAntidote);

        c.Goal(bL + RW - 2, 3);
        return Finish(c);
    }

    // ─────────────────────────────────────────────────────────────
    // Scene 3 — 청각 + 시각 : 소리로 찾는 포탈
    // ─────────────────────────────────────────────────────────────
    private static string BuildScene3_HearingSight()
    {
        Ctx c = NewScene("3_Hearing_Sight");
        c.Vision(3.4f); // 어두워서 멀리선 안 보이고, 진짜 포탈만 소리가 난다

        // Room A: 가짜 포탈 사이 진짜 1개(소리). 양옆 벽으로 막힌 막다른 방 — 진짜 포탈로만 탈출
        int aL = 0;
        c.Room(aL, aL + 2, 3);
        c.FloorSpan(aL, aL + RW - 1, 0); c.FloorSpan(aL, aL + RW - 1, 1);
        c.Wall(aL, 2, RH); c.Wall(aL + RW - 1, 2, RH);
        c.Portal(6, 3, PDecoy);
        c.Portal(12, 5, PDecoy);
        c.Portal(18, 3, PReal);   // 진짜 → 다음 룸으로 워프 + 소리 비콘
        c.Portal(23, 6, PDecoy);

        // Room B: 도착지 → 목표
        int bL = RW;
        c.Room(bL, bL + 2, 3);
        c.FloorSpan(bL, bL + RW - 1, 0); c.FloorSpan(bL, bL + RW - 1, 1);
        c.Wall(bL + RW - 1, 2, RH);
        c.Goal(bL + RW - 2, 3);
        return Finish(c);
    }

    // ─────────────────────────────────────────────────────────────
    // Scene 4 — 촉각 + 후각 + 미각 : 가스 위 위장 다리 + 광명/해독 열매
    // ─────────────────────────────────────────────────────────────
    private static string BuildScene4_TouchSmellTaste()
    {
        Ctx c = NewScene("4_Touch_Smell_Taste");

        // Room A: 구덩이 바닥은 가스(추락사). 위장 다리를 건너되 광명 열매로 안전블록 확인
        int aL = 0;
        c.Room(aL, aL + 2, 3);
        c.FloorSpan(aL, 7, 0); c.FloorSpan(aL, 7, 1);
        c.FloorSpan(21, aL + RW - 1, 0); c.FloorSpan(21, aL + RW - 1, 1);
        c.Wall(aL, 2, RH);
        for (int x = 8; x <= 20; x++) c.Gas(x, 0, GStatic); // 구덩이 바닥 가스
        int[] kindsA = { DSolid, DDeadly, DSolid, DSolid, DFake, DSolid, DDeadly, DSolid, DSolid, DFake, DSolid, DSolid, DSolid };
        for (int i = 0; i < kindsA.Length; i++) c.Disguised(8 + i, 2, kindsA[i]);
        c.Fruit(4, 4, FVision);

        // Room B: 위장 다리 + 스치는 맥동 가스 + 광명/해독
        int bL = RW;
        c.Room(bL, bL + 2, 3);
        c.FloorSpan(bL, bL + 5, 0); c.FloorSpan(bL, bL + 5, 1);
        c.FloorSpan(bL + 21, bL + RW - 1, 0); c.FloorSpan(bL + 21, bL + RW - 1, 1);
        c.Wall(bL + RW - 1, 2, RH);
        int[] kindsB = { DSolid, DFake, DSolid, DDeadly, DSolid, DSolid, DFake, DSolid, DDeadly, DSolid, DSolid };
        for (int i = 0; i < kindsB.Length; i++) c.Disguised(bL + 7 + i, 2, kindsB[i]);
        for (int x = bL + 8; x <= bL + 16; x++) c.Gas(x, 3, GPulse); // 다리 위 스치는 가스
        c.Fruit(bL + 2, 3, FVision);
        c.Fruit(bL + 4, 3, FAntidote);

        c.Goal(bL + RW - 2, 3);
        return Finish(c);
    }

    // ─────────────────────────────────────────────────────────────
    // Scene 5 — 5감각 종합 : 청각 → 시각+촉각 → 후각+미각
    // ─────────────────────────────────────────────────────────────
    private static string BuildScene5_AllSenses()
    {
        Ctx c = NewScene("5_All_Senses");
        c.Vision(3.8f);

        int aL = 0, bL = RW, cL = RW * 2;

        // Room A (청각): 진짜 포탈 찾기 → Room B
        c.Room(aL, aL + 2, 3);
        c.FloorSpan(aL, aL + RW - 1, 0); c.FloorSpan(aL, aL + RW - 1, 1);
        c.Wall(aL, 2, RH); c.Wall(aL + RW - 1, 2, RH);
        c.Portal(7, 4, PDecoy);
        c.Portal(14, 3, PReal);
        c.Portal(21, 5, PDecoy);

        // Room B (시각 + 촉각): 어둠 속 위장 다리 + 숨은 가시
        c.Room(bL, bL + 2, 3);
        c.FloorSpan(bL, bL + 6, 0); c.FloorSpan(bL, bL + 6, 1);
        c.FloorSpan(bL + 20, bL + RW - 1, 0); c.FloorSpan(bL + 20, bL + RW - 1, 1);
        int[] kinds = { DSolid, DDeadly, DSolid, DFake, DSolid, DSolid, DDeadly, DSolid, DSolid };
        for (int i = 0; i < kinds.Length; i++) c.Disguised(bL + 8 + i, 1, kinds[i]);
        c.Spike(bL + 4, 2);
        c.Fruit(bL + 3, 3, FVision);

        // Room C (후각 + 미각): 가스 + 해독 → 최종 목표
        c.Room(cL, cL + 2, 3);
        c.FloorSpan(cL, cL + RW - 1, 0); c.FloorSpan(cL, cL + RW - 1, 1);
        c.Wall(cL + RW - 1, 2, RH);
        c.Fruit(cL + 2, 3, FAntidote);
        for (int x = cL + 8; x <= cL + 12; x++)
            for (int y = 2; y <= 6; y++)
                c.Gas(x, y, GStatic);
        for (int y = 2; y <= 5; y++) { c.Gas(cL + 17, y, GPulse); c.Gas(cL + 18, y, GPulse); }

        c.Goal(cL + RW - 2, 3);
        return Finish(c);
    }

    // ═════════════════════════════════════════════════════════════
    // 빌드 컨텍스트 (전부 타일 기반)
    // ═════════════════════════════════════════════════════════════
    private class Ctx
    {
        public string path;
        public TileMapData map;
        public bool useVision;
        public float visionRadius = 4f;

        public Vector3 World(int gx, int gy) => map.GridToWorld(new Vector2Int(gx, gy));

        public void Vision(float radius) { useVision = true; visionRadius = radius; }

        private void Set(int x, int y, TileType type, int variant = 0)
        {
            Vector2Int p = new Vector2Int(x, y);
            map.AddOrReplace(p, type, Vector2.one);
            if (variant != 0)
            {
                TileData t = map.GetTile(p);
                if (t != null) t.variant = variant;
            }
        }

        public void Ground(int x, int y) => Set(x, y, TileType.Ground);

        public void GroundRect(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    Ground(x, y);
        }

        public void FloorSpan(int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x++) Ground(x, y);
        }

        public void Wall(int x, int y0, int y1)
        {
            for (int y = y0; y <= y1; y++) Ground(x, y);
        }

        public void Spike(int x, int y) => Set(x, y, TileType.Spike);
        public void Gas(int x, int y, int variant) => Set(x, y, TileType.Gas, variant);
        public void Disguised(int x, int y, int variant) => Set(x, y, TileType.Disguised, variant);
        public void Fruit(int x, int y, int kind) => Set(x, y, TileType.Fruit, kind);
        public void Portal(int x, int y, int variant) => Set(x, y, TileType.Portal, variant);
        public void Goal(int x, int y) => Set(x, y, TileType.Portal, PReal); // 진짜 포탈 = 다음 룸(마지막이면 첫 룸 루프)

        public void Room(int left, int spawnX, int spawnY)
        {
            map.AddCameraRoom(new Vector2Int(left, 0), new Vector2Int(left + RW - 1, RH));
            CameraRoomData room = map.CameraRooms[map.CameraRooms.Count - 1];
            room.roomName = $"Room {map.CameraRooms.Count}";
            room.hasSpawn = true;
            room.spawnGridPos = new Vector2Int(spawnX, spawnY);
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 씬 생성 / 마감
    // ═════════════════════════════════════════════════════════════
    private static Ctx NewScene(string name)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Ctx c = new Ctx { path = $"{SceneFolder}/{name}.unity" };

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(8f, 4f, -10f);
        EnsureUrpCameraData(camGo);
        CameraController camCtrl = camGo.AddComponent<CameraController>();

        GameObject mapGo = new GameObject("TileMap");
        mapGo.transform.position = Vector3.zero;
        c.map = mapGo.AddComponent<TileMapData>();

        SetSerializedReference(camCtrl, "_cameraRoomSource", c.map);
        return c;
    }

    private static string Finish(Ctx c)
    {
        c.map.RebuildAll();

        Vector3 spawn = c.map.CameraRooms.Count > 0
            ? c.map.GridToWorld(c.map.CameraRooms[0].spawnGridPos)
            : new Vector3(2.5f, 3.5f, 0f);

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject player = null;
        if (playerPrefab != null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = spawn;
        }
        else
        {
            Debug.LogWarning($"[TileMapTest] Player 프리팹을 찾을 수 없습니다: {PlayerPrefabPath}");
        }

        if (player != null && player.GetComponent<PlayerTaste>() == null)
            player.AddComponent<PlayerTaste>();

        CameraController camCtrl = Object.FindFirstObjectByType<CameraController>();
        PlayerFSM playerFsm = player != null ? player.GetComponent<PlayerFSM>() : null;
        if (camCtrl != null && playerFsm != null)
            SetSerializedReference(camCtrl, "_player", playerFsm);

        GameObject sys = new GameObject("@Systems");
        RoomRespawnController respawn = sys.AddComponent<RoomRespawnController>();
        SetSerializedReference(respawn, "_map", c.map);
        if (playerFsm != null)
            SetSerializedReference(respawn, "_player", playerFsm);

        if (c.useVision)
        {
            PlayerVision vision = sys.AddComponent<PlayerVision>();
            vision.BaseRadius = c.visionRadius;
        }

        EditorSceneManager.MarkAllScenesDirty();
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), c.path))
            Debug.LogError($"[TileMapTest] 씬 저장 실패: {c.path}");

        return c.path;
    }

    // ═════════════════════════════════════════════════════════════
    // 유틸
    // ═════════════════════════════════════════════════════════════
    private static void EnsureUrpCameraData(GameObject camGo)
    {
        var type = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (type != null && camGo.GetComponent(type) == null)
            camGo.AddComponent(type);
    }

    private static void SetSerializedReference(Object component, string fieldName, Object value)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureSceneFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets/Scenes", "TileMapTest");
    }

    private static void EnsureSprites()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Sprites");
        EnsureSquareSprite();
        EnsureVisionMaskSprite();
        EnsureVisionMaterial();
    }

    // PlayerVision 이 런타임에 로드하는 어둠 머티리얼(빌드 포함 보장).
    private static void EnsureVisionMaterial()
    {
        const string path = "Assets/Resources/Materials/VisionDarkness.mat";
        if (File.Exists(path)) return;

        Shader shader = Shader.Find("Custom/VisionDarkness");
        if (shader == null)
        {
            Debug.LogWarning("[TileMapTest] 'Custom/VisionDarkness' 셰이더를 찾을 수 없어 머티리얼 생성을 건너뜁니다.");
            return;
        }

        EnsureFolder("Assets/Resources/Materials");
        Material mat = new Material(shader) { name = "VisionDarkness" };
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void EnsureSquareSprite()
    {
        const string path = "Assets/Resources/Sprites/Square.png";
        if (!File.Exists(path))
        {
            Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        ConfigureSpriteImporter(path, 8);
    }

    // 방사형 그라디언트(중심 alpha=1 → 가장자리 0) — PlayerVision 의 SpriteMask 용
    private static void EnsureVisionMaskSprite()
    {
        const string path = "Assets/Resources/Sprites/VisionMask.png";
        if (!File.Exists(path))
        {
            int sz = 128;
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[sz * sz];
            float half = sz * 0.5f;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.SmoothStep(0.80f, 1.0f, d);
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        ConfigureSpriteImporter(path, 128);
    }

    private static void ConfigureSpriteImporter(string path, int pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spritePixelsPerUnit != pixelsPerUnit) { importer.spritePixelsPerUnit = pixelsPerUnit; dirty = true; }
        if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (dirty) importer.SaveAndReimport();
    }

    private static void AddToBuildSettings(List<string> scenePaths)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string path in scenePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            if (scenes.Exists(s => s.path == path)) continue;
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
