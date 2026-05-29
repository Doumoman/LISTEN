using UnityEngine;

public class LedgeDetection : MonoBehaviour
{
    [SerializeField] private float rayDistance = 0.15f;
    [SerializeField] private float downRayInset = 0.05f;   // 하향 ray 시작점의 벽 내부 진입 오프셋
    [SerializeField] private float downRayPadding = 0.2f;  // 하향 ray 길이 여유분

    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private bool isTouchingWall;
    [SerializeField] private bool isTouchingLedge;

    private PlayerFSM playerFSM;

    private Vector2 downRayStart;

    private void Awake()
    {
        playerFSM = GetComponent<PlayerFSM>();
    }

    private void Update()
    {
        CheckLedge();
    }

    private void CheckLedge()
    {
        if (playerFSM.PlayerData.isHanging) return;

        Vector2 rayDirection = new Vector2(Mathf.Sign(playerFSM.lastDir.x), 0f);

        // wallHit의 hitPoint의 x좌표는 모서리의 x좌표로 확정지을 수 있음
        // 모서리의 y좌표는 확정된 x좌표를 가지고 위에서 아래로 ray를 쏘면 구할 수 있음
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, rayDirection, rayDistance, groundLayer);
        RaycastHit2D ledgeHit = Physics2D.Raycast(ledgeCheck.position, rayDirection, rayDistance, groundLayer);

        isTouchingWall = wallHit.collider != null ? true : false;
        isTouchingLedge = ledgeHit.collider != null ? true : false;

        if (isTouchingWall && !isTouchingLedge && playerFSM.PlayerData.isFalling)
        {
            Debug.Log("Ledge 존재!");

            downRayStart = new Vector2(wallHit.point.x + (rayDirection.x * downRayInset), ledgeCheck.position.y); // 벽 표면에서 살짝 들어간 부분부터 시작
            float downRayLength = ledgeCheck.position.y - wallCheck.position.y + downRayPadding; // 머리 -> 가슴 길이보다 조금 더 길게

            RaycastHit2D cornerHit = Physics2D.Raycast(downRayStart, Vector2.down, downRayLength, groundLayer);

            if(cornerHit.collider != null)
            {
                Vector2 cornerPosition = new Vector2(wallHit.point.x, cornerHit.point.y);
                GrapLedge(cornerPosition);
            }
        }
    }

    private void GrapLedge(Vector2 cornerPos)
    {
        playerFSM.PlayerData.ledgeCornerPos = cornerPos;
        playerFSM.PlayerData.ledgeGrabDir = Mathf.Sign(playerFSM.lastDir.x);
        playerFSM.PlayerData.isLedgeGrabbed = true;

        playerFSM.SetMoveVelocity(0f, 0f);
    }

    private void OnDrawGizmos()
    {
        if (wallCheck == null || ledgeCheck == null || playerFSM == null) return;

        Vector2 dir = new Vector2(Mathf.Sign(playerFSM.lastDir.x), 0f);
        Gizmos.color = isTouchingWall ? Color.red : Color.green;
        Gizmos.DrawLine(wallCheck.position, (Vector2)wallCheck.position + dir * rayDistance);

        Gizmos.color = isTouchingLedge ? Color.red : Color.cyan;
        Gizmos.DrawLine(ledgeCheck.position, (Vector2)ledgeCheck.position + dir * rayDistance);
    }
}
