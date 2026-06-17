using UnityEngine;

/// <summary>
/// 닿으면 즉시 플레이어를 사망시키는 트리거. (가시/스파이크, 위장-치명 블록 등)
/// 플레이어는 Kinematic Rigidbody2D 라서 트리거 콜백이 정상 발생한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KillZone : MonoBehaviour
{
    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
    private void OnTriggerStay2D(Collider2D other) => TryKill(other);

    private void TryKill(Collider2D other)
    {
        PlayerFSM player = other.GetComponentInParent<PlayerFSM>();
        if (player != null)
            player.Kill();
    }
}
