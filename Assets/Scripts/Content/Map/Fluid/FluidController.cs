using System.Collections.Generic;
using UnityEngine;

public class FluidController : MonoBehaviour
{
    private const string ChunkPrefabPath = "Prefabs/Map/Fluid/FluidChunk";
    private const string FluidParticleLayerName = "FluidParticle";

    private const int CellsPerTile = 4;
    private const int MaxShaderParticles = 256;

    private static readonly List<FluidParticle> AllParticles = new List<FluidParticle>();
    private static readonly Dictionary<TileType, GlobalMetaballVisual> GlobalVisuals =
        new Dictionary<TileType, GlobalMetaballVisual>();

    private static int lastForceFrame = -1;

    [Header("Init Data")]
    [SerializeField] private TileType fluidType;
    [SerializeField] private Vector2Int gridPos;
    [SerializeField] private bool hasInitData;

    [Header("Chunk Visual")]
    [SerializeField] private SpriteRenderer chunkPrefab;

    [Tooltip("보이는 액체 방울의 월드 반지름입니다. Collider보다 커야 액체처럼 겹쳐 보입니다.")]
    [SerializeField] private float visualRadius = 0.28f;

    [Tooltip("실제 물리 충돌용 콜라이더 월드 반지름입니다. visualRadius보다 작아야 합니다.")]
    [SerializeField] private float colliderRadius = 0.09f;

    [Tooltip("생성 위치를 살짝 흔들어 격자 느낌을 줄입니다.")]
    [SerializeField] private float spawnJitter = 0.02f;

    [Header("Physics")]
    [SerializeField] private float gravityScale = 1.2f;
    [SerializeField] private float mass = 0.08f;
    [SerializeField] private float linearDrag = 2.0f;
    [SerializeField] private float angularDrag = 2.5f;

    [Header("Fluid Cohesion")]
    [SerializeField] private float restDistance = 0.22f;
    [SerializeField] private float interactionRadius = 0.6f;
    [SerializeField] private float pressureForce = 7.0f;
    [SerializeField] private float cohesionForce = 4.5f;
    [SerializeField] private float viscosity = 0.5f;

    [Header("Bounce / Jelly")]
    [SerializeField] private float bounce = 0.03f;

    [Header("Idle Wobble")]
    [SerializeField] private float idleWobbleForce = 0.02f;
    [SerializeField] private float idleWobbleSpeed = 2.4f;

    [Header("Individual Visual Deformation")]
    [SerializeField] private bool deformByVelocity = true;
    [SerializeField] private float stretchBySpeed = 0.11f;
    [SerializeField] private float maxStretch = 1.45f;
    [SerializeField] private float visualReturnSpeed = 12f;

    [Header("Global Metaball Visual")]
    [SerializeField] private bool useMetaballVisual = true;

    [Tooltip("Custom/Fluid/GlobalMetaball2D Shader를 사용하는 Material을 넣으세요.")]
    [SerializeField] private Material metaballMaterial;

    [SerializeField] private float metaballQuadPadding = 1.0f;
    [SerializeField] private string metaballSortingLayerName = "Default";
    [SerializeField] private int metaballSortingOrder = 20;

    [Header("Color")]
    [SerializeField] private Color waterColor = new Color(0.1f, 0.45f, 1f, 0.72f);
    [SerializeField] private Color lavaColor = new Color(1f, 0.25f, 0f, 0.9f);

    private Transform dynamicFluidParent;
    private readonly List<FluidParticle> myParticles = new List<FluidParticle>();

    private bool initialized;

    private class FluidParticle
    {
        public TileType type;

        public Rigidbody2D rb;
        public CircleCollider2D collider;

        public Transform visualTransform;
        public SpriteRenderer spriteRenderer;

        public float visualRadius;
        public float colliderRadius;
        public float phase;

        public Vector3 baseVisualScale;
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
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
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
            quadObject.transform.position = Vector3.zero;
            quadObject.transform.rotation = Quaternion.identity;
            quadObject.transform.localScale = Vector3.one;

            meshFilter = quadObject.AddComponent<MeshFilter>();
            meshRenderer = quadObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = CreateUnitQuadMesh();

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

            for (int i = 0; i < AllParticles.Count; i++)
            {
                FluidParticle p = AllParticles[i];

                if (p == null || p.rb == null)
                    continue;

                if (p.type != type)
                    continue;

                if (count >= MaxShaderParticles)
                    break;

                Vector2 pos = p.rb.position;
                float radius = p.visualRadius;

                shaderParticles[count] = new Vector4(pos.x, pos.y, radius, 0f);

                min = Vector2.Min(min, pos);
                max = Vector2.Max(max, pos);

                count++;
            }

            if (count <= 0)
            {
                quadObject.SetActive(false);
                return;
            }

            quadObject.SetActive(true);

            for (int i = count; i < shaderParticles.Length; i++)
            {
                shaderParticles[i] = Vector4.zero;
            }

            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;

            float width = Mathf.Max(1f, size.x + padding * 2f);
            float height = Mathf.Max(1f, size.y + padding * 2f);

            quadObject.transform.position = new Vector3(center.x, center.y, 0f);
            quadObject.transform.rotation = Quaternion.identity;
            quadObject.transform.localScale = new Vector3(width, height, 1f);

            runtimeMaterial.SetFloat("_ParticleCount", count);
            runtimeMaterial.SetVectorArray("_Particles", shaderParticles);
            runtimeMaterial.SetColor("_FluidColor", color);
        }

        public void Destroy()
        {
            if (quadObject != null)
                DestroyObjectSafeStatic(quadObject);

            if (runtimeMaterial != null)
                DestroyObjectSafeStatic(runtimeMaterial);

            quadObject = null;
            meshRenderer = null;
            meshFilter = null;
            runtimeMaterial = null;
        }

        private static Mesh CreateUnitQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Global Fluid Metaball Quad";

            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };

            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            mesh.triangles = new int[]
            {
                0, 2, 1,
                2, 3, 1
            };

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
        if (chunkPrefab == null)
            chunkPrefab = Resources.Load<SpriteRenderer>(ChunkPrefabPath);
    }

    private void Start()
    {
        if (initialized)
            return;

        if (!hasInitData)
            return;

        RebuildRuntimeFluid();
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        if (useMetaballVisual)
        {
            HideIndividualParticleVisuals();
            EnsureGlobalMetaballVisual();
            UpdateAllGlobalMetaballVisuals();
        }
        else
        {
            ShowIndividualParticleVisuals();
            UpdateVisualDeformation();
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
        {
            ApplySettingsToExistingParticles();
        }
    }

    private void RebuildRuntimeFluid()
    {
        ClearRuntimeObjects();

        ClampValues();

        dynamicFluidParent = GetOrCreateDynamicFluidParent();

        BuildParticles();

        if (useMetaballVisual)
        {
            HideIndividualParticleVisuals();
            EnsureGlobalMetaballVisual();
            UpdateAllGlobalMetaballVisuals();
        }

        initialized = true;
    }

    private void ClearRuntimeObjects()
    {
        initialized = false;

        for (int i = myParticles.Count - 1; i >= 0; i--)
        {
            FluidParticle p = myParticles[i];

            if (p != null)
                AllParticles.Remove(p);

            if (p != null && p.rb != null)
                DestroyObjectSafe(p.rb.gameObject);
        }

        myParticles.Clear();
    }

    private void ClampValues()
    {
        visualRadius = Mathf.Max(0.01f, visualRadius);
        colliderRadius = Mathf.Max(0.001f, colliderRadius);

        if (colliderRadius > visualRadius)
            colliderRadius = visualRadius * 0.5f;

        spawnJitter = Mathf.Max(0f, spawnJitter);

        gravityScale = Mathf.Max(0f, gravityScale);
        mass = Mathf.Max(0.001f, mass);
        linearDrag = Mathf.Max(0f, linearDrag);
        angularDrag = Mathf.Max(0f, angularDrag);

        restDistance = Mathf.Max(0.01f, restDistance);
        interactionRadius = Mathf.Max(restDistance + 0.01f, interactionRadius);

        pressureForce = Mathf.Max(0f, pressureForce);
        cohesionForce = Mathf.Max(0f, cohesionForce);
        viscosity = Mathf.Max(0f, viscosity);

        bounce = Mathf.Clamp01(bounce);

        idleWobbleForce = Mathf.Max(0f, idleWobbleForce);
        idleWobbleSpeed = Mathf.Max(0f, idleWobbleSpeed);

        stretchBySpeed = Mathf.Max(0f, stretchBySpeed);
        maxStretch = Mathf.Max(1f, maxStretch);
        visualReturnSpeed = Mathf.Max(0.01f, visualReturnSpeed);

        metaballQuadPadding = Mathf.Max(0.01f, metaballQuadPadding);
    }

    private void BuildParticles()
    {
        if (chunkPrefab == null)
        {
            chunkPrefab = Resources.Load<SpriteRenderer>(ChunkPrefabPath);

            if (chunkPrefab == null)
            {
                Debug.LogWarning(
                    $"FluidChunk prefab을 찾을 수 없습니다.\n" +
                    $"필요 경로: Assets/Resources/{ChunkPrefabPath}.prefab"
                );
                return;
            }
        }

        float cellSize = 1f / CellsPerTile;
        Vector3 tileBottomLeftWorld = transform.position + new Vector3(-0.5f, -0.5f, 0f);

        int fluidLayer = LayerMask.NameToLayer(FluidParticleLayerName);

        for (int y = 0; y < CellsPerTile; y++)
        {
            for (int x = 0; x < CellsPerTile; x++)
            {
                Vector3 worldPos = tileBottomLeftWorld + new Vector3(
                    (x + 0.5f) * cellSize,
                    (y + 0.5f) * cellSize,
                    0f
                );

                worldPos.x += Random.Range(-spawnJitter, spawnJitter);
                worldPos.y += Random.Range(-spawnJitter, spawnJitter);

                FluidParticle particle = CreateParticle(worldPos, x, y, fluidLayer);

                myParticles.Add(particle);
                AllParticles.Add(particle);
            }
        }
    }

    private FluidParticle CreateParticle(Vector3 worldPos, int x, int y, int fluidLayer)
    {
        GameObject bodyGO = new GameObject($"{fluidType}ParticleBody_{gridPos.x}_{gridPos.y}_{x}_{y}");
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

        CircleCollider2D circle = bodyGO.AddComponent<CircleCollider2D>();
        circle.isTrigger = false;
        circle.offset = Vector2.zero;
        circle.radius = colliderRadius;

        PhysicsMaterial2D mat = new PhysicsMaterial2D($"{fluidType}_FluidParticleMat");
        mat.friction = 0.02f;
        mat.bounciness = bounce;
        circle.sharedMaterial = mat;

        SpriteRenderer visual = Instantiate(chunkPrefab, bodyGO.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.color = GetFluidColor();

        if (fluidLayer >= 0)
            SetLayerRecursively(visual.gameObject, fluidLayer);

        StripPhysicsFromVisual(visual.gameObject);

        Vector3 visualScale = CalculateVisualScale(visual, visualRadius);
        visual.transform.localScale = visualScale;

        FluidParticle particle = new FluidParticle
        {
            type = fluidType,

            rb = rb,
            collider = circle,

            visualTransform = visual.transform,
            spriteRenderer = visual,

            visualRadius = visualRadius,
            colliderRadius = colliderRadius,

            phase = Random.Range(0f, 100f),
            baseVisualScale = visual.transform.localScale
        };

        return particle;
    }

    private Vector3 CalculateVisualScale(SpriteRenderer visual, float targetWorldRadius)
    {
        if (visual == null || visual.sprite == null)
            return Vector3.one * (targetWorldRadius * 2f);

        Vector2 spriteSize = visual.sprite.bounds.size;

        float safeWidth = Mathf.Max(0.0001f, spriteSize.x);
        float safeHeight = Mathf.Max(0.0001f, spriteSize.y);

        float targetDiameter = targetWorldRadius * 2f;

        return new Vector3(
            targetDiameter / safeWidth,
            targetDiameter / safeHeight,
            1f
        );
    }

    private void StripPhysicsFromVisual(GameObject visualGO)
    {
        if (visualGO == null)
            return;

        Rigidbody2D[] rigidbodies = visualGO.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            DestroyObjectSafe(rigidbodies[i]);
        }

        Collider2D[] colliders = visualGO.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            DestroyObjectSafe(colliders[i]);
        }
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;

        Transform t = target.transform;

        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    private Transform GetOrCreateDynamicFluidParent()
    {
        Transform tileMapRoot = transform.parent != null ? transform.parent.parent : null;

        if (tileMapRoot == null)
            tileMapRoot = transform.root;

        Transform found = tileMapRoot.Find("DynamicFluids");

        if (found != null)
            return found;

        GameObject go = new GameObject("DynamicFluids");
        go.transform.SetParent(tileMapRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        return go.transform;
    }

    private void ApplyGlobalFluidForcesOncePerFrame()
    {
        if (lastForceFrame == Time.frameCount)
            return;

        lastForceFrame = Time.frameCount;

        CleanupNullParticles();

        for (int i = 0; i < AllParticles.Count; i++)
        {
            FluidParticle a = AllParticles[i];

            if (a == null || a.rb == null)
                continue;

            for (int j = i + 1; j < AllParticles.Count; j++)
            {
                FluidParticle b = AllParticles[j];

                if (b == null || b.rb == null)
                    continue;

                if (a.type != b.type)
                    continue;

                Vector2 delta = b.rb.position - a.rb.position;
                float dist = delta.magnitude;

                if (dist <= 0.0001f)
                    continue;

                if (dist > interactionRadius)
                    continue;

                Vector2 dir = delta / dist;

                ApplyPairForces(a, b, dir, dist);
            }
        }
    }

    private void ApplyPairForces(FluidParticle a, FluidParticle b, Vector2 dir, float dist)
    {
        float targetDistance = restDistance;

        if (dist < targetDistance)
        {
            float t = 1f - dist / targetDistance;
            Vector2 force = -dir * pressureForce * t;

            a.rb.AddForce(force, ForceMode2D.Force);
            b.rb.AddForce(-force, ForceMode2D.Force);
        }
        else
        {
            float t = 1f - Mathf.InverseLerp(targetDistance, interactionRadius, dist);
            Vector2 force = dir * cohesionForce * t;

            a.rb.AddForce(force, ForceMode2D.Force);
            b.rb.AddForce(-force, ForceMode2D.Force);
        }

        Vector2 relativeVelocity = b.rb.linearVelocity - a.rb.linearVelocity;
        Vector2 viscousForce = relativeVelocity * viscosity;

        a.rb.AddForce(viscousForce, ForceMode2D.Force);
        b.rb.AddForce(-viscousForce, ForceMode2D.Force);
    }

    private void ApplyIdleWobble()
    {
        for (int i = 0; i < myParticles.Count; i++)
        {
            FluidParticle p = myParticles[i];

            if (p == null || p.rb == null)
                continue;

            float wave = Mathf.Sin(Time.time * idleWobbleSpeed + p.phase);
            Vector2 force = new Vector2(wave * idleWobbleForce, 0f);

            p.rb.AddForce(force, ForceMode2D.Force);
        }
    }

    private void UpdateVisualDeformation()
    {
        for (int i = 0; i < myParticles.Count; i++)
        {
            FluidParticle p = myParticles[i];

            if (p == null || p.rb == null || p.visualTransform == null)
                continue;

            if (!deformByVelocity)
            {
                p.visualTransform.localScale = Vector3.Lerp(
                    p.visualTransform.localScale,
                    p.baseVisualScale,
                    Time.deltaTime * visualReturnSpeed
                );

                p.visualTransform.localRotation = Quaternion.Lerp(
                    p.visualTransform.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * visualReturnSpeed
                );

                continue;
            }

            Vector2 velocity = p.rb.linearVelocity;
            float speed = velocity.magnitude;

            if (speed > 0.05f)
            {
                float stretch = Mathf.Clamp(1f + speed * stretchBySpeed, 1f, maxStretch);
                float squash = 1f / Mathf.Sqrt(stretch);

                Vector3 targetScale = new Vector3(
                    p.baseVisualScale.x * stretch,
                    p.baseVisualScale.y * squash,
                    p.baseVisualScale.z
                );

                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

                p.visualTransform.localRotation = Quaternion.Lerp(
                    p.visualTransform.localRotation,
                    Quaternion.Euler(0f, 0f, angle),
                    Time.deltaTime * visualReturnSpeed
                );

                p.visualTransform.localScale = Vector3.Lerp(
                    p.visualTransform.localScale,
                    targetScale,
                    Time.deltaTime * visualReturnSpeed
                );
            }
            else
            {
                p.visualTransform.localRotation = Quaternion.Lerp(
                    p.visualTransform.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * visualReturnSpeed
                );

                p.visualTransform.localScale = Vector3.Lerp(
                    p.visualTransform.localScale,
                    p.baseVisualScale,
                    Time.deltaTime * visualReturnSpeed
                );
            }
        }
    }

    private void HideIndividualParticleVisuals()
    {
        for (int i = 0; i < myParticles.Count; i++)
        {
            FluidParticle p = myParticles[i];

            if (p == null || p.spriteRenderer == null)
                continue;

            p.spriteRenderer.enabled = false;
        }
    }

    private void ShowIndividualParticleVisuals()
    {
        for (int i = 0; i < myParticles.Count; i++)
        {
            FluidParticle p = myParticles[i];

            if (p == null || p.spriteRenderer == null)
                continue;

            p.spriteRenderer.enabled = true;
        }
    }

    private void EnsureGlobalMetaballVisual()
    {
        if (!useMetaballVisual)
            return;

        if (metaballMaterial == null)
            return;

        if (GlobalVisuals.ContainsKey(fluidType))
            return;

        GlobalMetaballVisual visual = new GlobalMetaballVisual(
            fluidType,
            metaballMaterial,
            GetFluidColor(),
            metaballQuadPadding,
            metaballSortingLayerName,
            metaballSortingOrder
        );

        GlobalVisuals.Add(fluidType, visual);
    }

    private static void UpdateAllGlobalMetaballVisuals()
    {
        foreach (KeyValuePair<TileType, GlobalMetaballVisual> pair in GlobalVisuals)
        {
            pair.Value.UpdateVisual();
        }
    }

    private static void CleanupUnusedGlobalVisuals()
    {
        bool hasWater = false;
        bool hasLava = false;

        for (int i = 0; i < AllParticles.Count; i++)
        {
            FluidParticle p = AllParticles[i];

            if (p == null || p.rb == null)
                continue;

            if (p.type == TileType.Water)
                hasWater = true;
            else if (p.type == TileType.Lava)
                hasLava = true;
        }

        CleanupGlobalVisualIfUnused(TileType.Water, hasWater);
        CleanupGlobalVisualIfUnused(TileType.Lava, hasLava);
    }

    private static void CleanupGlobalVisualIfUnused(TileType type, bool hasAny)
    {
        if (hasAny)
            return;

        if (!GlobalVisuals.TryGetValue(type, out GlobalMetaballVisual visual))
            return;

        visual.Destroy();
        GlobalVisuals.Remove(type);
    }

    private void CleanupNullParticles()
    {
        for (int i = AllParticles.Count - 1; i >= 0; i--)
        {
            if (AllParticles[i] == null || AllParticles[i].rb == null)
                AllParticles.RemoveAt(i);
        }
    }

    private Color GetFluidColor()
    {
        return fluidType == TileType.Lava ? lavaColor : waterColor;
    }

    [ContextMenu("Apply Settings To Existing Particles")]
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

                if (p.collider.sharedMaterial != null)
                {
                    p.collider.sharedMaterial.friction = 0.02f;
                    p.collider.sharedMaterial.bounciness = bounce;
                }
            }

            if (p.spriteRenderer != null && p.visualTransform != null)
            {
                p.spriteRenderer.color = GetFluidColor();

                Vector3 newScale = CalculateVisualScale(p.spriteRenderer, visualRadius);

                p.visualTransform.localScale = newScale;
                p.baseVisualScale = newScale;
            }

            p.visualRadius = visualRadius;
            p.colliderRadius = colliderRadius;
        }
    }

    private void DestroyObjectSafe(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(target);
        else
            Destroy(target);
#else
        Destroy(target);
#endif
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