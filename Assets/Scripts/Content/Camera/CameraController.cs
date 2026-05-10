using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject target;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float lerpSpeed = 10f;

    private Transform targetTransform;

    private void Start()
    {
        targetTransform = target.GetComponent<Transform>();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (SingletonManagers.UI.PopupCount > 0) return; // UI 떨림 방지

        Vector3 targetPos = targetTransform.position + offset;
        Vector3 lerpPos = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);
        transform.position = lerpPos;
    }
}
