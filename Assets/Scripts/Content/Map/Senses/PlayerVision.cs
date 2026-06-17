using UnityEngine;

/// <summary>
/// 시각 기믹 — 플레이어 주변만 보이고 나머지 화면은 어둡게 만든다.
/// 카메라 앞에 화면을 덮는 쿼드를 두고, 'Custom/VisionDarkness' 셰이더가
/// 월드 좌표 거리(플레이어 기준)로 시야 원 밖을 어둡게 칠한다. (파이프라인 무관)
/// 광명(Vision) 열매를 먹으면 시야 반경이 커진다.
/// </summary>
public class PlayerVision : MonoBehaviour
{
    [Header("시야")]
    [SerializeField] private float _baseRadius = 4f;
    [SerializeField] private float _softness = 1.6f;
    [SerializeField] private bool _visionEnabled = true;

    [Header("렌더")]
    [SerializeField] private Color _darkColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private string _sortingLayerName = "Default";
    [SerializeField] private int _sortingOrder = 900;
    [SerializeField] private float _coverMargin = 1.6f;

    private const string MaterialPath = "Materials/VisionDarkness";
    private const string ShaderName = "Custom/VisionDarkness";

    private static readonly int CenterID = Shader.PropertyToID("_Center");
    private static readonly int RadiusID = Shader.PropertyToID("_Radius");
    private static readonly int SoftID = Shader.PropertyToID("_Soft");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private Camera _cam;
    private Transform _player;
    private PlayerTaste _taste;
    private MeshRenderer _renderer;
    private Material _material;

    public float BaseRadius { get => _baseRadius; set => _baseRadius = value; }
    public bool VisionEnabled { get => _visionEnabled; set => _visionEnabled = value; }

    private void Start()
    {
        _cam = Camera.main;

        PlayerFSM player = FindFirstObjectByType<PlayerFSM>();
        if (player != null)
        {
            _player = player.transform;
            _taste = player.GetComponent<PlayerTaste>();
        }

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        Material baseMat = Resources.Load<Material>(MaterialPath);
        if (baseMat == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader != null) baseMat = new Material(shader);
        }

        if (baseMat == null)
        {
            Debug.LogWarning($"[PlayerVision] '{ShaderName}' 셰이더/머티리얼을 찾을 수 없습니다.");
            return;
        }

        _material = new Material(baseMat); // 인스턴스(공유 머티리얼 오염 방지)

        GameObject go = new GameObject("VisionDark");
        go.transform.SetParent(transform, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateQuad();

        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.sortingLayerName = _sortingLayerName;
        _renderer.sortingOrder = _sortingOrder;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
    }

    private void LateUpdate()
    {
        if (_renderer == null) return;

        bool show = _visionEnabled && _cam != null && _player != null;
        _renderer.enabled = show;
        if (!show) return;

        float viewH = _cam.orthographic ? _cam.orthographicSize * 2f : 20f;
        float viewW = viewH * _cam.aspect;
        Vector3 camPos = _cam.transform.position;

        _renderer.transform.position = new Vector3(camPos.x, camPos.y, 0f);
        _renderer.transform.localScale = new Vector3(viewW * _coverMargin, viewH * _coverMargin, 1f);

        float radius = _baseRadius + (_taste != null ? _taste.VisionBonus : 0f);
        _material.SetVector(CenterID, new Vector4(_player.position.x, _player.position.y, 0f, 0f));
        _material.SetFloat(RadiusID, radius);
        _material.SetFloat(SoftID, _softness);
        _material.SetColor(ColorID, _darkColor);
    }

    private static Mesh CreateQuad()
    {
        Mesh mesh = new Mesh { name = "VisionQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
