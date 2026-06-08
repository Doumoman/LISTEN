using UnityEngine;

[ExecuteAlways]
public class CameraController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Target")]
    [SerializeField] private PlayerFSM _player;

    [Header("Follow Settings")]
    [SerializeField] private float _xLerpSpeed = 8f;
    [SerializeField] private float _yLerpSpeed = 5f;
    [SerializeField] private float _zOffset = -10f;
    [SerializeField] private float _yOffset = 0.5f;

    [Header("Look Ahead")]
    [SerializeField] private float _lookAheadDistance = 2.5f;
    [SerializeField] private float _lookAheadLerpSpeed = 4f;

    [Header("Vertical Dead Zone")]
    [SerializeField] private float _yDeadZone = 0.8f;

    [Header("Bounds")]
    [SerializeField] private bool _useBounds = false;
    [SerializeField] private float _minX = -100f;
    [SerializeField] private float _maxX = 100f;
    [SerializeField] private float _minY = -100f;
    [SerializeField] private float _maxY = 100f;

    [Header("Camera Rooms")]
    [SerializeField] private bool _useCameraRooms = true;
    [SerializeField] private TileMapData _cameraRoomSource;
    [SerializeField] private float _roomTransitionDuration = 0.35f;
    [SerializeField] private bool _freezeTimeDuringRoomTransition = true;
    [SerializeField] private AnimationCurve _roomTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visible Area")]
    [SerializeField] private bool _useFixedVisibleArea = true;
    [SerializeField] private Vector2Int _referenceResolution = new Vector2Int(2560, 1440);
    [SerializeField] private float _visibleAreaWorldHeight = 9f;
    [SerializeField] private bool _forceReferenceAspect = true;
    [SerializeField] private bool _shrinkVisibleAreaWhenRoomIsSmaller = true;
    [SerializeField] private bool _usePlayerPlaneForPerspectiveBounds = true;
    [SerializeField] private float _perspectiveBoundsPlaneZ = 0f;

    #endregion

    #region Runtime State

    private float _currentLookAheadX;
    private float _targetLookAheadX;
    private float _anchorY;

    private Camera _camera;
    private UIManager _uiManager;
    private float _defaultOrthographicSize;
    private CameraRoomData _currentCameraRoom;
    private Rect _currentCameraRoomRect;
    private bool _hasCurrentCameraRoom;

    private bool _isRoomTransitioning;
    private Vector3 _roomTransitionStartPosition;
    private Vector3 _roomTransitionTargetPosition;
    private float _roomTransitionStartSize;
    private float _roomTransitionTargetSize;
    private float _roomTransitionElapsed;
    private float _timeScaleBeforeRoomTransition = 1f;

    #endregion

    #region Unity Entry Points

    private void Start()
    {
        _camera = GetComponent<Camera>();

        if (Application.isPlaying)
            _uiManager = SingletonManagers.UI;

        EnsureCamera();
        _defaultOrthographicSize = _camera != null ? _camera.orthographicSize : 5f;
        ApplyBaseVisibleArea();

        if (!Application.isPlaying)
        {
            UpdateEditorRoomPreview();
            return;
        }

        if (_player == null)
            _player = FindFirstObjectByType<PlayerFSM>();

        if (_cameraRoomSource == null)
            _cameraRoomSource = FindFirstObjectByType<TileMapData>();

        if (_player == null) return;

        Vector3 startPos = _player.transform.position;
        _anchorY = startPos.y;

        if (TryFindCameraRoom(startPos, out CameraRoomData room, out Rect roomRect))
        {
            SnapToCameraRoom(room, roomRect, startPos);
            return;
        }

        transform.position = new Vector3(startPos.x, startPos.y + _yOffset, _zOffset);
    }

    private void OnValidate()
    {
        _camera = GetComponent<Camera>();

        EnsureCamera();
        ApplyBaseVisibleArea();

        if (!Application.isPlaying)
            UpdateEditorRoomPreview();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            UpdateEditorRoomPreview();
            return;
        }

        if (_player == null) return;

        if (_isRoomTransitioning)
        {
            UpdateRoomTransition();
            return;
        }

        if (IsPopupBlocking()) return;

        Vector3 playerPos = _player.transform.position;

        if (HasCameraRooms())
        {
            UpdateCameraRoom(playerPos);
            return;
        }

        FollowPlayer(playerPos);
    }

    #endregion

    #region Camera Room Flow

    private void UpdateCameraRoom(Vector3 playerPos)
    {
        if (!TryFindCameraRoom(playerPos, out CameraRoomData room, out Rect roomRect))
        {
            if (_hasCurrentCameraRoom)
                FollowPlayerInCameraRoom(playerPos, _currentCameraRoomRect);
            else
                FollowPlayer(playerPos);

            return;
        }

        if (!_hasCurrentCameraRoom)
        {
            SnapToCameraRoom(room, roomRect, playerPos);
            return;
        }

        if (IsSameCameraRoom(_currentCameraRoom, room))
        {
            FollowPlayerInCameraRoom(playerPos, roomRect);
            return;
        }

        BeginRoomTransition(room, roomRect, playerPos);
    }

    #endregion

    #region Follow Logic

    private void FollowPlayer(Vector3 playerPos)
    {
        ApplyBaseVisibleArea();

        Vector3 followPosition = CalculateFollowPosition(playerPos, Time.deltaTime);
        float newX = followPosition.x;
        float newY = followPosition.y;

        if (_useBounds)
        {
            newX = Mathf.Clamp(newX, _minX, _maxX);
            newY = Mathf.Clamp(newY, _minY, _maxY);
        }

        transform.position = new Vector3(newX, newY, _zOffset);
    }

    private void FollowPlayerInCameraRoom(Vector3 playerPos, Rect roomRect)
    {
        EnsureCamera();
        ApplyRoomCameraSize(roomRect);

        Vector3 followPosition = CalculateFollowPosition(playerPos, Time.deltaTime);
        Vector2 clamped = ClampCameraCenterToRoom(followPosition, roomRect);
        transform.position = new Vector3(clamped.x, clamped.y, _zOffset);
    }

    private Vector3 CalculateFollowPosition(Vector3 playerPos, float deltaTime)
    {
        _targetLookAheadX = _player.lastDir.x * _lookAheadDistance;
        _currentLookAheadX = Mathf.Lerp(_currentLookAheadX, _targetLookAheadX, _lookAheadLerpSpeed * deltaTime);

        float targetX = playerPos.x + _currentLookAheadX;
        float newX = Mathf.Lerp(transform.position.x, targetX, _xLerpSpeed * deltaTime);

        float yDelta = playerPos.y - _anchorY;
        if (Mathf.Abs(yDelta) > _yDeadZone)
            _anchorY = playerPos.y - Mathf.Sign(yDelta) * _yDeadZone;

        float newY = Mathf.Lerp(transform.position.y, _anchorY + _yOffset, _yLerpSpeed * deltaTime);

        return new Vector3(newX, newY, _zOffset);
    }

    #endregion

    #region Camera Room Lookup And Editor Preview

    private bool HasCameraRooms()
    {
        return _useCameraRooms
            && _cameraRoomSource != null
            && _cameraRoomSource.CameraRooms.Count > 0;
    }

    private bool TryFindCameraRoom(Vector3 playerPos, out CameraRoomData room, out Rect roomRect)
    {
        if (!HasCameraRooms())
        {
            room = null;
            roomRect = default;
            return false;
        }

        return _cameraRoomSource.TryGetCameraRoom(playerPos, out room, out roomRect);
    }

    private void UpdateEditorRoomPreview()
    {
        _camera = GetComponent<Camera>();

        if (_cameraRoomSource == null)
            _cameraRoomSource = FindFirstObjectByType<TileMapData>();

        if (_player == null)
            _player = FindFirstObjectByType<PlayerFSM>();

        EnsureCamera();

        if (_player == null || !TryFindCameraRoom(_player.transform.position, out CameraRoomData room, out Rect roomRect))
        {
            ApplyBaseVisibleArea();
            return;
        }

        _currentCameraRoom = room;
        _currentCameraRoomRect = roomRect;
        _hasCurrentCameraRoom = true;
        _anchorY = _player.transform.position.y;

        ApplyRoomCameraSize(roomRect);

        Vector2 target = new Vector2(_player.transform.position.x, _player.transform.position.y + _yOffset);
        Vector2 clamped = ClampCameraCenterToRoom(target, roomRect);
        transform.position = new Vector3(clamped.x, clamped.y, _zOffset);
    }

    #endregion

    #region Camera Room Transitions

    private void SnapToCameraRoom(CameraRoomData room, Rect roomRect, Vector3 playerPos)
    {
        _currentCameraRoom = room;
        _currentCameraRoomRect = roomRect;
        _hasCurrentCameraRoom = true;
        _anchorY = playerPos.y;

        EnsureCamera();
        ApplyRoomCameraSize(roomRect);

        Vector2 clamped = ClampCameraCenterToRoom(new Vector2(playerPos.x, playerPos.y + _yOffset), roomRect);
        transform.position = new Vector3(clamped.x, clamped.y, _zOffset);
    }

    private void BeginRoomTransition(CameraRoomData nextRoom, Rect nextRoomRect, Vector3 playerPos)
    {
        _currentCameraRoom = nextRoom;
        _currentCameraRoomRect = nextRoomRect;
        _hasCurrentCameraRoom = true;
        _anchorY = playerPos.y;

        EnsureCamera();
        _roomTransitionStartPosition = transform.position;
        _roomTransitionStartSize = _camera != null ? _camera.orthographicSize : 0f;
        _roomTransitionTargetSize = GetRoomOrthographicSize(nextRoomRect);
        _roomTransitionTargetPosition = GetRoomTransitionTargetPosition(nextRoomRect, playerPos, _roomTransitionTargetSize);
        _roomTransitionElapsed = 0f;
        _isRoomTransitioning = true;

        if (_freezeTimeDuringRoomTransition)
        {
            _timeScaleBeforeRoomTransition = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void UpdateRoomTransition()
    {
        float duration = Mathf.Max(0.01f, _roomTransitionDuration);
        _roomTransitionElapsed += Time.unscaledDeltaTime;

        float normalized = Mathf.Clamp01(_roomTransitionElapsed / duration);
        float eased = _roomTransitionCurve != null
            ? _roomTransitionCurve.Evaluate(normalized)
            : normalized;

        transform.position = Vector3.Lerp(_roomTransitionStartPosition, _roomTransitionTargetPosition, eased);

        if (_camera != null && _camera.orthographic)
            _camera.orthographicSize = Mathf.Lerp(_roomTransitionStartSize, _roomTransitionTargetSize, eased);

        if (normalized < 1f)
            return;

        transform.position = _roomTransitionTargetPosition;

        if (_camera != null && _camera.orthographic)
            _camera.orthographicSize = _roomTransitionTargetSize;

        _isRoomTransitioning = false;
        _anchorY = _player != null ? _player.transform.position.y : _anchorY;

        if (_freezeTimeDuringRoomTransition)
            Time.timeScale = _timeScaleBeforeRoomTransition;
    }

    private Vector3 GetRoomTransitionTargetPosition(Rect roomRect, Vector3 playerPos, float orthographicSize)
    {
        Vector2 target = new Vector2(playerPos.x + _player.lastDir.x * _lookAheadDistance, playerPos.y + _yOffset);
        Vector2 clamped = ClampCameraCenterToRoom(target, roomRect, orthographicSize);
        return new Vector3(clamped.x, clamped.y, _zOffset);
    }

    #endregion

    #region Visible Area And Room Bounds

    private float GetRoomOrthographicSize(Rect roomRect)
    {
        if (_camera == null || !_camera.orthographic)
            return _defaultOrthographicSize;

        ApplyReferenceAspect();

        float desiredSize = GetConfiguredOrthographicSize();
        float maxSizeInsideRoom = GetMaxOrthographicSizeInsideRoom(roomRect);

        if (_shrinkVisibleAreaWhenRoomIsSmaller)
            return Mathf.Min(desiredSize, maxSizeInsideRoom);

        return desiredSize;
    }

    private void ApplyRoomCameraSize(Rect roomRect)
    {
        EnsureCamera();

        if (_camera == null)
            return;

        ApplyReferenceAspect();

        if (_camera.orthographic)
        {
            _camera.orthographicSize = GetRoomOrthographicSize(roomRect);
            return;
        }

        ApplyConfiguredPerspectiveFov();
    }

    private void ApplyBaseVisibleArea()
    {
        EnsureCamera();

        if (_camera == null)
            return;

        ApplyReferenceAspect();

        if (!_useFixedVisibleArea)
            return;

        if (_camera.orthographic)
        {
            _camera.orthographicSize = GetConfiguredOrthographicSize();
            return;
        }

        ApplyConfiguredPerspectiveFov();
    }

    private void EnsureCamera()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();
    }

    private void ApplyReferenceAspect()
    {
        if (_camera == null || !_forceReferenceAspect)
            return;

        _camera.aspect = GetReferenceAspect();
    }

    private void ApplyConfiguredPerspectiveFov()
    {
        if (_camera == null || _camera.orthographic || !_useFixedVisibleArea)
            return;

        float distance = GetPerspectiveBoundsPlaneDistance(transform.position);
        if (distance <= 0.01f)
            return;

        float halfHeight = Mathf.Max(0.01f, _visibleAreaWorldHeight * 0.5f);
        float fov = Mathf.Atan(halfHeight / distance) * 2f * Mathf.Rad2Deg;
        _camera.fieldOfView = Mathf.Clamp(fov, 1f, 179f);
    }

    private float GetConfiguredOrthographicSize()
    {
        if (!_useFixedVisibleArea)
            return _defaultOrthographicSize;

        return Mathf.Max(0.01f, _visibleAreaWorldHeight * 0.5f);
    }

    private float GetMaxOrthographicSizeInsideRoom(Rect roomRect)
    {
        float aspect = GetCameraAspect();
        float halfHeightLimit = Mathf.Max(0.1f, roomRect.height * 0.5f);
        float halfWidthLimit = Mathf.Max(0.1f, roomRect.width / (2f * aspect));
        float maxSizeInsideRoom = Mathf.Min(halfHeightLimit, halfWidthLimit);

        return Mathf.Max(0.01f, maxSizeInsideRoom);
    }

    private Vector2 ClampCameraCenterToRoom(Vector2 cameraCenter, Rect roomRect)
    {
        return ClampCameraCenterToRoom(cameraCenter, roomRect, -1f);
    }

    private Vector2 ClampCameraCenterToRoom(Vector2 cameraCenter, Rect roomRect, float orthographicSize)
    {
        EnsureCamera();

        if (_camera == null)
            return cameraCenter;

        Vector3 candidateCameraPosition = new Vector3(cameraCenter.x, cameraCenter.y, _zOffset);
        Vector2 visibleSize = GetVisibleAreaWorldSize(candidateCameraPosition, orthographicSize);
        Vector2 visibleCenter = GetVisibleAreaWorldCenter(candidateCameraPosition);
        Vector2 cameraToVisibleCenterOffset = visibleCenter - cameraCenter;
        Vector2 halfVisibleSize = visibleSize * 0.5f;

        float minX = roomRect.xMin + halfVisibleSize.x;
        float maxX = roomRect.xMax - halfVisibleSize.x;
        float minY = roomRect.yMin + halfVisibleSize.y;
        float maxY = roomRect.yMax - halfVisibleSize.y;

        float x = minX > maxX
            ? roomRect.center.x
            : Mathf.Clamp(visibleCenter.x, minX, maxX);

        float y = minY > maxY
            ? roomRect.center.y
            : Mathf.Clamp(visibleCenter.y, minY, maxY);

        return new Vector2(x, y) - cameraToVisibleCenterOffset;
    }

    private float GetCameraAspect()
    {
        if (_forceReferenceAspect)
            return GetReferenceAspect();

        return _camera != null
            ? Mathf.Max(0.01f, _camera.aspect)
            : GetReferenceAspect();
    }

    private float GetReferenceAspect()
    {
        int width = Mathf.Max(1, _referenceResolution.x);
        int height = Mathf.Max(1, _referenceResolution.y);
        return width / (float)height;
    }

    private Vector2 GetCurrentVisibleAreaWorldSize()
    {
        return GetVisibleAreaWorldSize(transform.position, -1f);
    }

    private Vector3 GetCurrentVisibleAreaWorldCenter()
    {
        EnsureCamera();

        if (_camera == null)
            return transform.position;

        Vector2 center = GetVisibleAreaWorldCenter(transform.position);
        return new Vector3(center.x, center.y, GetPerspectiveBoundsPlaneZ());
    }

    private Vector2 GetVisibleAreaWorldSize(Vector3 cameraPosition, float orthographicSize)
    {
        EnsureCamera();

        if (_camera == null)
            return GetConfiguredVisibleAreaWorldSize();

        float halfHeight;
        if (_camera.orthographic)
        {
            halfHeight = orthographicSize > 0f ? orthographicSize : _camera.orthographicSize;
        }
        else
        {
            float distance = GetPerspectiveBoundsPlaneDistance(cameraPosition);
            if (distance <= 0.01f)
                return GetConfiguredVisibleAreaWorldSize();

            halfHeight = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
        }

        return new Vector2(halfHeight * 2f * GetCameraAspect(), halfHeight * 2f);
    }

    private Vector2 GetConfiguredVisibleAreaWorldSize()
    {
        float height = Mathf.Max(0.01f, _visibleAreaWorldHeight);
        return new Vector2(height * GetCameraAspect(), height);
    }

    private Vector2 GetVisibleAreaWorldCenter(Vector3 cameraPosition)
    {
        if (_camera == null || _camera.orthographic)
            return new Vector2(cameraPosition.x, cameraPosition.y);

        float distance = GetPerspectiveBoundsPlaneDistance(cameraPosition);
        if (distance <= 0.01f)
            return new Vector2(cameraPosition.x, cameraPosition.y);

        Vector3 center = cameraPosition + _camera.transform.forward * distance;
        return new Vector2(center.x, center.y);
    }

    private float GetPerspectiveBoundsPlaneDistance(Vector3 cameraPosition)
    {
        if (_camera == null)
            return 0f;

        float forwardZ = _camera.transform.forward.z;
        if (Mathf.Abs(forwardZ) < 0.001f)
            return Mathf.Abs(GetPerspectiveBoundsPlaneZ() - cameraPosition.z);

        return (GetPerspectiveBoundsPlaneZ() - cameraPosition.z) / forwardZ;
    }

    private float GetPerspectiveBoundsPlaneZ()
    {
        if (_usePlayerPlaneForPerspectiveBounds && _player != null)
            return _player.transform.position.z;

        return _perspectiveBoundsPlaneZ;
    }

    #endregion

    #region Utilities And Cleanup

    private bool IsPopupBlocking()
    {
        return _uiManager != null && _uiManager.PopupCount > 0;
    }

    private bool IsSameCameraRoom(CameraRoomData a, CameraRoomData b)
    {
        if (a == null || b == null)
            return false;

        return a.startGridPos == b.startGridPos && a.endGridPos == b.endGridPos;
    }

    private void OnDisable()
    {
        RestoreTimeScaleIfTransitioning();
    }

    private void OnDestroy()
    {
        RestoreTimeScaleIfTransitioning();
    }

    private void RestoreTimeScaleIfTransitioning()
    {
        if (!_isRoomTransitioning || !_freezeTimeDuringRoomTransition)
            return;

        Time.timeScale = _timeScaleBeforeRoomTransition;
        _isRoomTransitioning = false;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();

        if (_hasCurrentCameraRoom)
        {
            Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireCube(
                _currentCameraRoomRect.center,
                new Vector3(_currentCameraRoomRect.width, _currentCameraRoomRect.height, 0.01f));
        }

        Vector2 visibleSize = GetCurrentVisibleAreaWorldSize();
        Vector3 visibleCenter = GetCurrentVisibleAreaWorldCenter();
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(
            visibleCenter,
            new Vector3(visibleSize.x, visibleSize.y, 0.01f));
    }

    #endregion
}
