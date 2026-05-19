using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class FallingPlatformController : MonoBehaviour
{
    private enum State
    {
        Idle,
        Arming,
        Falling,
        Breaking
    }

    [Header("Layer")]
    [SerializeField] private string sensorLayerName = "Sensor";

    [Header("Detection")]
    [SerializeField] private LayerMask activationLayers;
    [SerializeField] private float activationHoldTime = 0.1f;
    [SerializeField] private float topSensorHeight = 0.07f;
    [SerializeField] private float topSensorInset = 0.03f;
    [SerializeField] private float topCheckTolerance = 0.04f;
    [SerializeField] private float maxUpwardVelocity = 0.1f;

    [Header("Fall")]
    [SerializeField] private float armDelay = 0.25f;
    [SerializeField] private float gravityScale = 2.5f;

    [Header("Break")]
    [SerializeField] private LayerMask breakLayers;
    [SerializeField] private float bottomSensorHeight = 0.07f;
    [SerializeField] private float bottomSensorInset = 0.03f;
    [SerializeField] private float breakAnimTime = 0.35f;
    [SerializeField] private float destroyDelay = 0.05f;
    [SerializeField] private GameObject breakEffectPrefab;

    [Header("Domino")]
    [SerializeField] private bool useDomino = true;
    [SerializeField] private float dominoMinFallVelocity = 0.2f;

    private Rigidbody2D _rb;
    private BoxCollider2D _bodyCollider;
    private BoxCollider2D _topSensor;
    private BoxCollider2D _bottomSensor;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;

    private State _state = State.Idle;

    private readonly HashSet<Collider2D> _currentTopObjects = new HashSet<Collider2D>();

    private float _holdTimer;
    private float _armTimer;
    private Vector2 _bodySize = Vector2.one;
    private Coroutine _breakRoutine;

    public bool IsFalling => _state == State.Falling;
    public bool IsBroken => _state == State.Breaking;

    private void Awake()
    {
        CacheComponents();

        if (_bodyCollider != null)
            _bodySize = _bodyCollider.size;

        SetupRigidbodyIdle();
        SetupTopSensor();
        SetupBottomSensor();

        SetTopSensorActive(true);
        SetBottomSensorActive(false);
    }

    private void Reset()
    {
        activationLayers = LayerMask.GetMask("Player", "Pushable", "Throwable", "Monster");
        breakLayers = LayerMask.GetMask("Ground", "LockedBlock");
        sensorLayerName = "Sensor";
    }

    public void Init(Vector2 colliderSize)
    {
        CacheComponents();

        _bodySize = colliderSize;

        _bodyCollider.isTrigger = false;
        _bodyCollider.size = colliderSize;
        _bodyCollider.offset = Vector2.zero;

        // 중요:
        // colliderSize는 BoxCollider2D.size에만 적용한다.
        // root scale까지 키우면 Sensor 위치가 과장된다.
        transform.localScale = Vector3.one;

        SetupRigidbodyIdle();
        SetupTopSensor();
        SetupBottomSensor();

        SetTopSensorActive(true);
        SetBottomSensorActive(false);

        _state = State.Idle;
        _holdTimer = 0f;
        _armTimer = 0f;
        _currentTopObjects.Clear();

        transform.localRotation = Quaternion.identity;
    }

    private void CacheComponents()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_bodyCollider == null)
            _bodyCollider = GetComponent<BoxCollider2D>();

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_animator == null)
            _animator = GetComponent<Animator>();
    }

    private void FixedUpdate()
    {
        switch (_state)
        {
            case State.Idle:
                TickIdle();
                break;

            case State.Arming:
                TickArming();
                break;
        }
    }

    private void TickIdle()
    {
        CleanupNullTopObjects();

        if (HasValidObjectOnTop())
        {
            _holdTimer += Time.fixedDeltaTime;

            if (_holdTimer >= activationHoldTime)
                BeginArming();
        }
        else
        {
            _holdTimer = 0f;
        }
    }

    private void TickArming()
    {
        _armTimer += Time.fixedDeltaTime;

        ShakeVisual();

        if (_armTimer >= armDelay)
            BeginFalling();
    }

    private void BeginArming()
    {
        if (_state != State.Idle)
            return;

        _state = State.Arming;
        _armTimer = 0f;
    }

    private void BeginFalling()
    {
        if (_state == State.Falling || _state == State.Breaking)
            return;

        _state = State.Falling;

        transform.localRotation = Quaternion.identity;

        SetTopSensorActive(false);
        SetBottomSensorActive(true);

        _currentTopObjects.Clear();

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = gravityScale;
        _rb.freezeRotation = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    public void ForceTriggerByDomino()
    {
        if (_state == State.Breaking || _state == State.Falling)
            return;

        BeginFalling();
    }

    public void NotifyBottomHit(Collider2D other)
    {
        if (_state != State.Falling)
            return;

        if (other == null)
            return;

        if (!IsBreakLayer(other.gameObject.layer))
            return;

        Break();
    }

    private void Break()
    {
        if (_state == State.Breaking)
            return;

        _state = State.Breaking;

        if (_breakRoutine != null)
            StopCoroutine(_breakRoutine);

        _breakRoutine = StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        if (breakEffectPrefab != null)
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);

        SetTopSensorActive(false);
        SetBottomSensorActive(false);

        _currentTopObjects.Clear();

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;

        if (_bodyCollider != null)
            _bodyCollider.enabled = true;

        if (_animator != null)
            _animator.SetTrigger("Break");

        float timer = 0f;

        while (timer < breakAnimTime)
        {
            timer += Time.deltaTime;

            if (_animator == null)
            {
                float shake = Mathf.Sin(Time.time * 90f) * 0.025f;
                transform.localRotation = Quaternion.Euler(0f, 0f, shake * 100f);
            }

            yield return null;
        }

        transform.localRotation = Quaternion.identity;

        if (_bodyCollider != null)
            _bodyCollider.enabled = false;

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = false;

        if (_rb != null)
            _rb.simulated = false;

        yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_state != State.Falling)
            return;

        FallingPlatformController otherFallingPlatform =
            collision.collider.GetComponentInParent<FallingPlatformController>();

        if (useDomino &&
            otherFallingPlatform != null &&
            otherFallingPlatform != this &&
            !otherFallingPlatform.IsBroken)
        {
            if (_rb.linearVelocity.y <= -dominoMinFallVelocity)
            {
                otherFallingPlatform.ForceTriggerByDomino();

                // 도미노 방식 B:
                // 떨어지는 발판이 다른 붕괴 발판을 치면,
                // 상대는 떨어지고 자신은 깨진다.
                Break();
                return;
            }
        }
    }

    public void AddTopObject(Collider2D other)
    {
        if (_state != State.Idle)
            return;

        if (other == null)
            return;

        if (!IsActivationLayer(other.gameObject.layer))
            return;

        _currentTopObjects.Add(other);
    }

    public void RemoveTopObject(Collider2D other)
    {
        if (other == null)
            return;

        _currentTopObjects.Remove(other);
    }

    private bool HasValidObjectOnTop()
    {
        foreach (Collider2D col in _currentTopObjects)
        {
            if (col == null)
                continue;

            if (!col.enabled)
                continue;

            if (!IsReallyAbovePlatform(col))
                continue;

            Rigidbody2D otherRb = col.attachedRigidbody;

            if (otherRb != null && otherRb.linearVelocity.y > maxUpwardVelocity)
                continue;

            return true;
        }

        return false;
    }

    private bool IsReallyAbovePlatform(Collider2D other)
    {
        if (_bodyCollider == null)
            return false;

        Bounds platformBounds = _bodyCollider.bounds;
        Bounds otherBounds = other.bounds;

        float platformTop = platformBounds.max.y;
        float otherBottom = otherBounds.min.y;

        if (otherBottom < platformTop - topCheckTolerance)
            return false;

        float platformLeft = platformBounds.min.x;
        float platformRight = platformBounds.max.x;
        float otherCenterX = otherBounds.center.x;

        return otherCenterX >= platformLeft && otherCenterX <= platformRight;
    }

    private void SetupRigidbodyIdle()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void SetupTopSensor()
    {
        Transform sensorTransform = transform.Find("TopSensor");

        if (sensorTransform == null)
        {
            GameObject sensorGO = new GameObject("TopSensor");
            sensorGO.transform.SetParent(transform, false);
            sensorTransform = sensorGO.transform;
        }

        sensorTransform.localPosition = Vector3.zero;
        sensorTransform.localRotation = Quaternion.identity;
        sensorTransform.localScale = Vector3.one;

        ApplySensorLayer(sensorTransform.gameObject);

        _topSensor = sensorTransform.GetComponent<BoxCollider2D>();

        if (_topSensor == null)
            _topSensor = sensorTransform.gameObject.AddComponent<BoxCollider2D>();

        FallingPlatformTopSensor forwarder =
            sensorTransform.GetComponent<FallingPlatformTopSensor>();

        if (forwarder == null)
            forwarder = sensorTransform.gameObject.AddComponent<FallingPlatformTopSensor>();

        forwarder.Init(this);

        _topSensor.isTrigger = true;

        Vector2 sensorSize = new Vector2(
            Mathf.Max(0.05f, _bodySize.x * 0.85f),
            topSensorHeight
        );

        float sensorY = _bodySize.y * 0.5f + topSensorHeight * 0.5f - topSensorInset;

        _topSensor.size = sensorSize;
        _topSensor.offset = new Vector2(0f, sensorY);
    }

    private void SetupBottomSensor()
    {
        Transform sensorTransform = transform.Find("BottomSensor");

        if (sensorTransform == null)
        {
            GameObject sensorGO = new GameObject("BottomSensor");
            sensorGO.transform.SetParent(transform, false);
            sensorTransform = sensorGO.transform;
        }

        sensorTransform.localPosition = Vector3.zero;
        sensorTransform.localRotation = Quaternion.identity;
        sensorTransform.localScale = Vector3.one;

        ApplySensorLayer(sensorTransform.gameObject);

        _bottomSensor = sensorTransform.GetComponent<BoxCollider2D>();

        if (_bottomSensor == null)
            _bottomSensor = sensorTransform.gameObject.AddComponent<BoxCollider2D>();

        FallingPlatformBottomSensor forwarder =
            sensorTransform.GetComponent<FallingPlatformBottomSensor>();

        if (forwarder == null)
            forwarder = sensorTransform.gameObject.AddComponent<FallingPlatformBottomSensor>();

        forwarder.Init(this);

        _bottomSensor.isTrigger = true;

        Vector2 sensorSize = new Vector2(
            Mathf.Max(0.05f, _bodySize.x * 0.85f),
            bottomSensorHeight
        );

        float sensorY = -_bodySize.y * 0.5f - bottomSensorHeight * 0.5f + bottomSensorInset;

        _bottomSensor.size = sensorSize;
        _bottomSensor.offset = new Vector2(0f, sensorY);
    }

    private void ApplySensorLayer(GameObject sensorGO)
    {
        int layer = LayerMask.NameToLayer(sensorLayerName);

        if (layer >= 0)
            sensorGO.layer = layer;
        else
            Debug.LogWarning($"Sensor Layer를 찾을 수 없습니다: {sensorLayerName}");
    }

    private void SetTopSensorActive(bool active)
    {
        if (_topSensor != null)
            _topSensor.enabled = active;
    }

    private void SetBottomSensorActive(bool active)
    {
        if (_bottomSensor != null)
            _bottomSensor.enabled = active;
    }

    private void ShakeVisual()
    {
        float x = Mathf.Sin(Time.time * 80f) * 0.015f;
        transform.localRotation = Quaternion.Euler(0f, 0f, x * 100f);
    }

    private void CleanupNullTopObjects()
    {
        _currentTopObjects.RemoveWhere(col => col == null || !col.enabled);
    }

    private bool IsActivationLayer(int layer)
    {
        return (activationLayers.value & (1 << layer)) != 0;
    }

    private bool IsBreakLayer(int layer)
    {
        return (breakLayers.value & (1 << layer)) != 0;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D body = GetComponent<BoxCollider2D>();

        if (body == null)
            return;

        Bounds bounds = body.bounds;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(
                bounds.center.x,
                bounds.max.y + topSensorHeight * 0.5f - topSensorInset,
                bounds.center.z
            ),
            new Vector3(bounds.size.x * 0.85f, topSensorHeight, 0.01f)
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(
                bounds.center.x,
                bounds.min.y - bottomSensorHeight * 0.5f + bottomSensorInset,
                bounds.center.z
            ),
            new Vector3(bounds.size.x * 0.85f, bottomSensorHeight, 0.01f)
        );
    }
}