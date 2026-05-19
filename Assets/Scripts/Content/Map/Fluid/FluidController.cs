using System.Collections.Generic;
using UnityEngine;

public class FluidController : MonoBehaviour
{
    private const string ChunkPrefabPath = "Prefabs/Map/Fluid/FluidChunk";
    private const string FluidParticleLayerName = "FluidParticle";

    private const int CellsPerTile = 3;
    private const int MaxShaderParticles = 256;

    private static readonly List<FluidParticle> AllParticles = new List<FluidParticle>();
    private static readonly Dictionary<TileType, GlobalMetaballVisual> GlobalVisuals = new();

    [Header("Init Data")]
    [SerializeField] private TileType fluidType;
    [SerializeField] private Vector2Int gridPos;
    [SerializeField] private bool hasInitData;

    [Header("Particle Visual")]
    [SerializeField] private SpriteRenderer chunkPrefab;
    [SerializeField] private Sprite fallbackChunkSprite;
    [SerializeField] private Material fallbackChunkMaterial;

    [Header("Particle Shape")]
    [SerializeField] private float visualRadius = 0.28f;
    [SerializeField] private float colliderRadius = 0.09f;
    [SerializeField] private float spawnJitter = 0.02f;

    [Header("Physics")]
    [SerializeField] private float gravityScale = 1.2f;
    [SerializeField] private float mass = 0.08f;
    [SerializeField] private float linearDrag = 2.0f;
    [SerializeField] private float angularDrag = 2.5f;
    [SerializeField] private float bounce = 0.0f;

    [Header("Metaball Visual")]
    [SerializeField] private bool useMetaballVisual = true;

    [Tooltip("Custom/Fluid/GlobalMetaball2D Shader를 사용하는 Material")]
    [SerializeField] private Material metaballMaterial;

    [SerializeField] private float metaballQuadPadding = 1.0f;
    [SerializeField] private string metaballSortingLayerName = "Default";
    [SerializeField] private int metaballSortingOrder = 20;

    [Header("Color")]
    [SerializeField] private Color waterColor = new Color(0.1f, 0.45f, 1f, 0.72f);
    [SerializeField] private Color lavaColor = new Color(1f, 0.25f, 0f, 0.9f);

    private readonly List<FluidParticle> myParticles = new();
    private Transform dynamicFluidParent;
    private bool initialized;

    private class FluidParticle
    {
        public TileType type;
        public Rigidbody2D rb;
        public CircleCollider2D collider;
        public SpriteRenderer spriteRenderer;
        public Transform visualTransform;
        public float visualRadius;
    }

    private class GlobalMetaballVisual
    {
        private readonly TileType type;
        private readonly Material sourceMaterial;
        private readonly Color color;
        private readonly float padding;
        private readonly string sortingLayerName;
        private readonly int sortingOrder;

        private GameObject quadObject;
        private Material runtimeMaterial;
        private readonly Vector4[] shaderParticles = new Vector4[MaxShaderParticles];

        public GlobalMetaballVisual(
            TileType type,
            Material sourceMaterial,
            Color color,
            float padding,
            string sortingLayerName,
            int sortingOrder)
        {
            this.type = type;
            this.sourceMaterial = sourceMaterial;
            this.color = color;
            this.padding = padding;
            this.sortingLayerName = sortingLayerName;
            this.sortingOrder = sortingOrder;

            Create();
        }

        private void Create()
        {
            if (sourceMaterial == null)
                return;

            quadObject = new GameObject($"{type}_GlobalMetaballVisual");

            MeshFilter meshFilter = quadObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = quadObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = CreateQuadMesh();

            runtimeMaterial = new Material(sourceMaterial);
            runtimeMaterial.name = $"{sourceMaterial.name}_{type}_Runtime";

            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        public void UpdateVisual()
        {
            if (quadObject == null || runtimeMaterial == null)
                return;

            int count = 0;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < AllParticles.Count && count < MaxShaderParticles; i++)
            {
                FluidParticle p = AllParticles[i];

                if (p == null || p.rb == null || p.type != type)
                    continue;

                Vector2 pos = p.rb.position;
                shaderParticles[count] = new Vector4(pos.x, pos.y, p.visualRadius, 0f);

                min = Vector2.Min(min, pos);
                max = Vector2.Max(max, pos);

                count++;
            }

            if (count == 0)
            {
                quadObject.SetActive(false);
                return;
            }

            for (int i = count; i < shaderParticles.Length; i++)
                shaderParticles[i] = Vector4.zero;

            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;

            quadObject.SetActive(true);
            quadObject.transform.position = new Vector3(center.x, center.y, 0f);
            quadObject.transform.rotation = Quaternion.identity;
            quadObject.transform.localScale = new Vector3(
                Mathf.Max(1f, size.x + padding * 2f),
                Mathf.Max(1f, size.y + padding * 2f),
                1f
            );

            runtimeMaterial.SetFloat("_ParticleCount", count);
            runtimeMaterial.SetVectorArray("_Particles", shaderParticles);
            runtimeMaterial.SetColor("_FluidColor", color);
        }

        public void Destroy()
        {
            DestroyObjectSafeStatic(quadObject);
            DestroyObjectSafeStatic(runtimeMaterial);

            quadObject = null;
            runtimeMaterial = null;
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Global Fluid Metaball Quad";

            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };

            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            return mesh;
        }
    }

    public void Init(TileType type, Vector2Int pos)
    {
        fluidType = type;
        gridPos = pos;
        hasInitData = true;

        RebuildRuntimeFluid();
    }

    private void Awake()
    {
        ResolveVisualReferences();
    }

    private void Start()
    {
        if (!initialized && hasInitData)
            RebuildRuntimeFluid();
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        EnsureAllParticleVisuals();

        if (useMetaballVisual)
        {
            SetIndividualVisuals(false);
            EnsureGlobalMetaballVisual();
            UpdateAllGlobalMetaballVisuals();
        }
        else
        {
            SetIndividualVisuals(true);
        }
    }

    private void OnDestroy()
    {
        ClearRuntimeObjects();
        CleanupUnusedGlobalVisuals();
    }

    private void OnValidate()
    {
        ClampValues();

        if (Application.isPlaying && initialized)
            ApplySettingsToExistingParticles();
    }

    private void RebuildRuntimeFluid()
    {
        ClearRuntimeObjects();
        ResolveVisualReferences();
        ClampValues();

        dynamicFluidParent = GetOrCreateDynamicFluidParent();

        BuildParticles();
        EnsureAllParticleVisuals();

        if (useMetaballVisual)
        {
            SetIndividualVisuals(false);
            EnsureGlobalMetaballVisual();
            UpdateAllGlobalMetaballVisuals();
        }

        initialized = true;
    }

    private void BuildParticles()
    {
        if (chunkPrefab == null && fallbackChunkSprite == null)
        {
            Debug.LogWarning("Fluid visual에 사용할 Sprite가 없습니다. chunkPrefab 또는 fallbackChunkSprite를 넣어주세요.");
            return;
        }

        float cellSize = 1f / CellsPerTile;
        Vector3 tileBottomLeft = transform.position + new Vector3(-0.5f, -0.5f, 0f);
        int fluidLayer = LayerMask.NameToLayer(FluidParticleLayerName);

        for (int y = 0; y < CellsPerTile; y++)
        {
            for (int x = 0; x < CellsPerTile; x++)
            {
                Vector3 pos = tileBottomLeft + new Vector3(
                    (x + 0.5f) * cellSize + Random.Range(-spawnJitter, spawnJitter),
                    (y + 0.5f) * cellSize + Random.Range(-spawnJitter, spawnJitter),
                    0f
                );

                FluidParticle p = CreateParticle(pos, x, y, fluidLayer);
                myParticles.Add(p);
                AllParticles.Add(p);
            }
        }
    }

    private FluidParticle CreateParticle(Vector3 worldPos, int x, int y, int fluidLayer)
    {
        GameObject bodyGO = new GameObject($"{fluidType}Particle_{gridPos.x}_{gridPos.y}_{x}_{y}");
        bodyGO.transform.SetParent(dynamicFluidParent, true);
        bodyGO.transform.position = worldPos;
        bodyGO.transform.rotation = Quaternion.identity;
        bodyGO.transform.localScale = Vector3.one;

        if (fluidLayer >= 0)
            bodyGO.layer = fluidLayer;

        Rigidbody2D rb = bodyGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravityScale;
        rb.mass = mass;
        rb.linearDamping = linearDrag;
        rb.angularDamping = angularDrag;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = bodyGO.AddComponent<CircleCollider2D>();
        col.radius = colliderRadius;
        col.offset = Vector2.zero;
        col.sharedMaterial = CreatePhysicsMaterial();

        SpriteRenderer visual = CreateVisualRenderer(bodyGO.transform, fluidLayer);

        return new FluidParticle
        {
            type = fluidType,
            rb = rb,
            collider = col,
            spriteRenderer = visual,
            visualTransform = visual.transform,
            visualRadius = visualRadius
        };
    }

    private SpriteRenderer CreateVisualRenderer(Transform parent, int layer)
    {
        SpriteRenderer visual;

        if (chunkPrefab != null)
        {
            visual = Instantiate(chunkPrefab, parent);
        }
        else
        {
            GameObject visualGO = new GameObject("Visual");
            visualGO.transform.SetParent(parent, false);
            visual = visualGO.AddComponent<SpriteRenderer>();
        }

        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        if (layer >= 0)
            SetLayerRecursively(visual.gameObject, layer);

        StripPhysicsFromVisual(visual.gameObject);
        EnsureVisualRenderer(visual);

        visual.transform.localScale = CalculateVisualScale(visual, visualRadius);

        return visual;
    }

    private PhysicsMaterial2D CreatePhysicsMaterial()
    {
        PhysicsMaterial2D mat = new PhysicsMaterial2D($"{fluidType}_FluidParticleMat");
        mat.friction = 0.02f;
        mat.bounciness = bounce;
        return mat;
    }

    private void ResolveVisualReferences()
    {
        if (chunkPrefab == null)
            chunkPrefab = Resources.Load<SpriteRenderer>(ChunkPrefabPath);

        if (chunkPrefab == null)
            return;

        if (fallbackChunkSprite == null)
            fallbackChunkSprite = chunkPrefab.sprite;

        if (fallbackChunkMaterial == null)
            fallbackChunkMaterial = chunkPrefab.sharedMaterial;
    }

    private void EnsureVisualRenderer(SpriteRenderer visual)
    {
        if (visual == null)
            return;

        if (visual.sprite == null)
            visual.sprite = fallbackChunkSprite;

        if (visual.sharedMaterial == null)
            visual.sharedMaterial = fallbackChunkMaterial;

        visual.color = GetFluidColor();
        visual.enabled = !useMetaballVisual;

        if (visual.sprite == null)
            Debug.LogWarning($"{name}: Fluid Visual Sprite가 없습니다. fallbackChunkSprite를 확인하세요.");
    }

    private void EnsureAllParticleVisuals()
    {
        ResolveVisualReferences();

        for (int i = 0; i < myParticles.Count; i++)
        {
            FluidParticle p = myParticles[i];

            if (p == null || p.spriteRenderer == null)
                continue;

            EnsureVisualRenderer(p.spriteRenderer);

            if (p.visualTransform != null)
                p.visualTransform.localScale = CalculateVisualScale(p.spriteRenderer, visualRadius);

            p.visualRadius = visualRadius;
        }
    }

    private Vector3 CalculateVisualScale(SpriteRenderer visual, float targetRadius)
    {
        if (visual == null || visual.sprite == null)
            return Vector3.one * (targetRadius * 2f);

        Vector2 size = visual.sprite.bounds.size;
        float diameter = targetRadius * 2f;

        return new Vector3(
            diameter / Mathf.Max(0.0001f, size.x),
            diameter / Mathf.Max(0.0001f, size.y),
            1f
        );
    }

    private void SetIndividualVisuals(bool visible)
    {
        for (int i = 0; i < myParticles.Count; i++)
        {
            if (myParticles[i]?.spriteRenderer != null)
                myParticles[i].spriteRenderer.enabled = visible;
        }
    }

    private void ApplySettingsToExistingParticles()
    {
        ClampValues();

        for (int i = 0; i < myParticles.Count; i++)
        {
            FluidParticle p = myParticles[i];

            if (p == null)
                continue;

            if (p.rb != null)
            {
                p.rb.gravityScale = gravityScale;
                p.rb.mass = mass;
                p.rb.linearDamping = linearDrag;
                p.rb.angularDamping = angularDrag;
            }

            if (p.collider != null)
            {
                p.collider.radius = colliderRadius;
                p.collider.offset = Vector2.zero;
                p.collider.sharedMaterial = CreatePhysicsMaterial();
            }

            p.visualRadius = visualRadius;
        }

        EnsureAllParticleVisuals();
    }

    private void EnsureGlobalMetaballVisual()
    {
        if (!useMetaballVisual || metaballMaterial == null)
            return;

        if (GlobalVisuals.ContainsKey(fluidType))
            return;

        GlobalVisuals.Add(
            fluidType,
            new GlobalMetaballVisual(
                fluidType,
                metaballMaterial,
                GetFluidColor(),
                metaballQuadPadding,
                metaballSortingLayerName,
                metaballSortingOrder
            )
        );
    }

    private static void UpdateAllGlobalMetaballVisuals()
    {
        foreach (GlobalMetaballVisual visual in GlobalVisuals.Values)
            visual.UpdateVisual();
    }

    private static void CleanupUnusedGlobalVisuals()
    {
        CleanupGlobalVisualIfUnused(TileType.Water);
        CleanupGlobalVisualIfUnused(TileType.Lava);
    }

    private static void CleanupGlobalVisualIfUnused(TileType type)
    {
        bool hasAny = false;

        for (int i = 0; i < AllParticles.Count; i++)
        {
            FluidParticle p = AllParticles[i];

            if (p != null && p.rb != null && p.type == type)
            {
                hasAny = true;
                break;
            }
        }

        if (hasAny)
            return;

        if (!GlobalVisuals.TryGetValue(type, out GlobalMetaballVisual visual))
            return;

        visual.Destroy();
        GlobalVisuals.Remove(type);
    }

    private void ClearRuntimeObjects()
    {
        initialized = false;

        for (int i = myParticles.Count - 1; i >= 0; i--)
        {
            FluidParticle p = myParticles[i];

            if (p != null)
                AllParticles.Remove(p);

            if (p?.rb != null)
                DestroyObjectSafe(p.rb.gameObject);
        }

        myParticles.Clear();
    }

    private Transform GetOrCreateDynamicFluidParent()
    {
        Transform root = transform.parent != null ? transform.parent.parent : transform.root;
        Transform found = root != null ? root.Find("DynamicFluids") : null;

        if (found != null)
            return found;

        GameObject go = new GameObject("DynamicFluids");
        go.transform.SetParent(root, false);
        return go.transform;
    }

    private void StripPhysicsFromVisual(GameObject visualGO)
    {
        if (visualGO == null)
            return;

        Rigidbody2D[] rbs = visualGO.GetComponentsInChildren<Rigidbody2D>(true);
        Collider2D[] cols = visualGO.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < rbs.Length; i++)
            DestroyObjectSafe(rbs[i]);

        for (int i = 0; i < cols.Length; i++)
            DestroyObjectSafe(cols[i]);
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;

        for (int i = 0; i < target.transform.childCount; i++)
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
    }

    private void ClampValues()
    {
        visualRadius = Mathf.Max(0.01f, visualRadius);
        colliderRadius = Mathf.Clamp(colliderRadius, 0.001f, visualRadius * 0.5f);
        spawnJitter = Mathf.Max(0f, spawnJitter);

        gravityScale = Mathf.Max(0f, gravityScale);
        mass = Mathf.Max(0.001f, mass);
        linearDrag = Mathf.Max(0f, linearDrag);
        angularDrag = Mathf.Max(0f, angularDrag);
        bounce = Mathf.Clamp01(bounce);

        metaballQuadPadding = Mathf.Max(0.01f, metaballQuadPadding);
    }

    private Color GetFluidColor()
    {
        return fluidType == TileType.Lava ? lavaColor : waterColor;
    }

    private void DestroyObjectSafe(Object target)
    {
        DestroyObjectSafeStatic(target);
    }

    private static void DestroyObjectSafeStatic(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(target);
        else
            Object.Destroy(target);
#else
        Object.Destroy(target);
#endif
    }
}