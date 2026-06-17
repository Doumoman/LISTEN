using UnityEngine;

/// <summary>
/// 포탈 — 플레이어가 들어가면 목표 지점으로 이동(클리어/다음 룸 진입).
/// 청각 기믹에서는 여러 개의 가짜 포탈 사이에 진짜 포탈 하나를 둔다.
/// 진짜(_isReal)는 목표로 워프, 가짜는 아무 일도 없거나(_killOnDecoy=true면) 함정.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Portal : MonoBehaviour
{
    [SerializeField] private bool _isReal = true;
    [SerializeField] private Transform _target;        // 도착 위치(진짜일 때)
    [SerializeField] private bool _killOnDecoy = false; // 가짜를 함정으로 만들지
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField] private float _spinSpeed = 90f;

    public void Configure(bool isReal, Transform target, bool killOnDecoy)
    {
        _isReal = isReal;
        _target = target;
        _killOnDecoy = killOnDecoy;
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (_visual != null)
            _visual.transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerFSM player = other.GetComponentInParent<PlayerFSM>();
        if (player == null) return;

        if (_isReal)
        {
            if (_target != null)
                player.Teleport(_target.position);
        }
        else if (_killOnDecoy)
        {
            player.Kill();
        }
    }
}
