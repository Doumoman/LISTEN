using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatformController : MonoBehaviour
{
    [Header("Move Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Move Settings")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float waitTime = 0.15f;
    [SerializeField] private bool startToB = true;

    private Rigidbody2D _rb;
    private Transform _target;
    private bool _movingToB;
    private float _waitTimer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        RefreshTarget();
    }

    public void Init(Transform a, Transform b)
    {
        pointA = a;
        pointB = b;

        _movingToB = startToB;
        _target = _movingToB ? pointB : pointA;

        if (pointA != null)
            transform.position = pointA.position;
    }

    private void FixedUpdate()
    {
        if (pointA == null || pointB == null)
            return;

        if (_target == null)
            RefreshTarget();

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 current = _rb.position;
        Vector2 target = _target.position;

        Vector2 next = Vector2.MoveTowards(
            current,
            target,
            speed * Time.fixedDeltaTime
        );

        _rb.MovePosition(next);

        if (Vector2.Distance(next, target) <= 0.01f)
        {
            _movingToB = !_movingToB;
            _target = _movingToB ? pointB : pointA;
            _waitTimer = waitTime;
        }
    }

    private void RefreshTarget()
    {
        _movingToB = startToB;
        _target = _movingToB ? pointB : pointA;
    }

    private void OnDrawGizmos()
    {
        if (pointA == null || pointB == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pointA.position, pointB.position);

        Gizmos.DrawWireSphere(pointA.position, 0.15f);
        Gizmos.DrawWireSphere(pointB.position, 0.15f);
    }
}