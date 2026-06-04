using UnityEngine;

public interface IHoldable
{
    void OnPickedUp(Transform holdPoint);
    void OnThrown(Vector2 velocity, float gravity, LayerMask collisionLayer);
    void OnDropped();
    Transform Transform { get; }
}
