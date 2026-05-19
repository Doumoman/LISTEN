using UnityEngine;

public class FallingPlatformTopSensor : MonoBehaviour
{
    private FallingPlatformController _owner;

    public void Init(FallingPlatformController owner)
    {
        _owner = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_owner == null)
            _owner = GetComponentInParent<FallingPlatformController>();

        if (_owner == null)
            return;

        _owner.AddTopObject(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_owner == null)
            _owner = GetComponentInParent<FallingPlatformController>();

        if (_owner == null)
            return;

        _owner.RemoveTopObject(other);
    }
}