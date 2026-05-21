using UnityEngine;

public class LedgeDetection : MonoBehaviour
{
    [SerializeField] private float _rayDistance = 0.15f;
    // 0 = top과 동일, 1 = collider center와 동일 (기본 0.5 = top~center 중간)
    [SerializeField] private float _midRayRatio = 0.5f;
    [SerializeField] private LayerMask _groundLayer;

    private PlayerFSM _player;

    private Vector2 _topOrigin;
    private Vector2 _midOrigin;
    private Vector2 _ledgePosition;
    public Vector2 LedgePosition => _ledgePosition;

    private int _nearLedgeFrameCount;
    [SerializeField] private int _nearLedgeWindowFrames = 3;

    private void Awake()
    {
        _player = GetComponent<PlayerFSM>();
    }

    private void Update()
    {
        BoxCollider2D bc = _player.Bc;
        Vector2 center = (Vector2)_player.transform.position + bc.offset;
        float halfW = bc.size.x * 0.5f;
        float halfH = bc.size.y * 0.5f;
        float faceDir = Mathf.Sign(_player.lastDir.x);
        float originX = center.x + faceDir * halfW;

        // 최상단 ray: collider top
        _topOrigin.x = originX;
        _topOrigin.y = center.y + halfH;

        // 중단 ray: top~center 사이 (_midRayRatio 비율)
        _midOrigin.x = originX;
        _midOrigin.y = center.y + halfH * (1f - _midRayRatio);

        Vector2 rayDir = Vector2.right * faceDir;
        RaycastHit2D topHit = Physics2D.Raycast(_topOrigin, rayDir, _rayDistance, _groundLayer);
        RaycastHit2D midHit = Physics2D.Raycast(_midOrigin, rayDir, _rayDistance, _groundLayer);

        // topRay는 감지 x, midRay는 감지 o, Ledge가 있다고 판단
        bool nearLedge = topHit.collider == null && midHit.collider != null;
        _player.ledgeDetected = nearLedge;

        if (nearLedge)
            _nearLedgeFrameCount = _nearLedgeWindowFrames;
        else if (_nearLedgeFrameCount > 0)
            _nearLedgeFrameCount--;

        // Grab 타이밍: nearLedge 윈도우 내에 topRay가 감지되면 grab
        if (_nearLedgeFrameCount > 0 && topHit.collider != null)
        {
            _player.ledgeGrabReady = true;
            // 하향 ray로 실제 레지 윗면 Y 계산 (topHit.point.y는 플레이어 top Y라 얇은 레지에서 오차 발생)
            Vector2 downOrigin = new Vector2(topHit.point.x - faceDir * 0.02f, _topOrigin.y + 0.2f);
            RaycastHit2D surfaceHit = Physics2D.Raycast(downOrigin, Vector2.down, halfH * 2f + 0.3f, _groundLayer);
            _ledgePosition.x = topHit.point.x;
            _ledgePosition.y = surfaceHit.collider != null ? surfaceHit.point.y : topHit.point.y;
        }
        else
        {
            _player.ledgeGrabReady = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (_player == null || _player.Bc == null) return;

        BoxCollider2D bc = _player.Bc;
        Vector2 center = (Vector2)_player.transform.position + bc.offset;
        float halfW = bc.size.x * 0.5f;
        float halfH = bc.size.y * 0.5f;
        float faceDir = Mathf.Sign(_player.lastDir.x);
        float originX = center.x + faceDir * halfW;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector2(originX, center.y + halfH),
                        new Vector2(originX + faceDir * _rayDistance, center.y + halfH));

        Gizmos.color = Color.green;
        float midY = center.y + halfH * (1f - _midRayRatio);
        Gizmos.DrawLine(new Vector2(originX, midY),
                        new Vector2(originX + faceDir * _rayDistance, midY));

        if (_player.ledgeGrabReady)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_ledgePosition, 0.1f);
        }
    }
}
