using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerFSM _player;

    [Header("Follow Settings")]
    [SerializeField] private float _xLerpSpeed = 8f;
    [SerializeField] private float _yLerpSpeed = 5f;
    [SerializeField] private float _zOffset = -10f;
    [SerializeField] private float _yOffset = 0.5f; // 플레이어 위쪽을 더 보이게

    [Header("Look Ahead")]
    [SerializeField] private float _lookAheadDistance = 2.5f;
    [SerializeField] private float _lookAheadLerpSpeed = 4f;

    [Header("Vertical Dead Zone")]
    [SerializeField] private float _yDeadZone = 0.8f; // 이 범위 안 수직 이동은 카메라가 무시

    [Header("Bounds")]
    [SerializeField] private bool _useBounds = false;
    [SerializeField] private float _minX = -100f;
    [SerializeField] private float _maxX = 100f;
    [SerializeField] private float _minY = -100f;
    [SerializeField] private float _maxY = 100f;

    private float _currentLookAheadX;
    private float _targetLookAheadX;
    private float _anchorY; // 카메라가 추적하는 Y 기준점

    private void Start()
    {
        if (_player == null) return;

        Vector3 startPos = _player.transform.position;
        transform.position = new Vector3(startPos.x, startPos.y + _yOffset, _zOffset);
        _anchorY = startPos.y;
    }

    private void LateUpdate()
    {
        if (_player == null) return;
        if (SingletonManagers.UI.PopupCount > 0) return;

        Vector3 playerPos = _player.transform.position;

        // 수평 예측: 플레이어 이동 방향으로 카메라가 선행
        _targetLookAheadX = _player.lastDir.x * _lookAheadDistance;
        _currentLookAheadX = Mathf.Lerp(_currentLookAheadX, _targetLookAheadX, _lookAheadLerpSpeed * Time.deltaTime);

        float targetX = playerPos.x + _currentLookAheadX;
        float newX = Mathf.Lerp(transform.position.x, targetX, _xLerpSpeed * Time.deltaTime);

        // 수직 데드존: _anchorY에서 _yDeadZone 이상 벗어나야 추적
        float yDelta = playerPos.y - _anchorY;
        if (Mathf.Abs(yDelta) > _yDeadZone)
            _anchorY = playerPos.y - Mathf.Sign(yDelta) * _yDeadZone;

        float newY = Mathf.Lerp(transform.position.y, _anchorY + _yOffset, _yLerpSpeed * Time.deltaTime);

        if (_useBounds)
        {
            newX = Mathf.Clamp(newX, _minX, _maxX);
            newY = Mathf.Clamp(newY, _minY, _maxY);
        }

        transform.position = new Vector3(newX, newY, _zOffset);
    }
}