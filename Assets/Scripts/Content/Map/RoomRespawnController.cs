using UnityEngine;

/// <summary>
/// 카메라 룸 기반 리스폰 컨트롤러. (셀레스트식 룸 단위 체크포인트)
/// 플레이어가 들어간 카메라 룸의 스폰 지점을 기억하고, 사망하면 그 지점에서 부활시킨다.
/// 룸을 넘어갈 때마다 체크포인트(스폰 지점)가 갱신된다.
/// </summary>
public class RoomRespawnController : MonoBehaviour
{
    [SerializeField] private TileMapData _map;
    [SerializeField] private PlayerFSM _player;
    [SerializeField] private float _respawnDelay = 0.45f;

    private CameraRoomData _currentRoom;
    private Vector3 _activeSpawn;
    private Vector3 _fallbackSpawn;
    private bool _respawning;
    private float _deadTimer;

    private void Awake()
    {
        if (_map == null) _map = FindFirstObjectByType<TileMapData>();
        if (_player == null) _player = FindFirstObjectByType<PlayerFSM>();
    }

    private void Start()
    {
        _fallbackSpawn = _player != null ? _player.transform.position : transform.position;
        _activeSpawn = _fallbackSpawn;
        UpdateRoomCheckpoint(force: true);
    }

    private void Update()
    {
        if (_player == null) return;

        if (!_player.IsDead)
        {
            _respawning = false;
            UpdateRoomCheckpoint(force: false);
            return;
        }

        // 사망 상태 — 지연 후 현재 체크포인트에서 부활
        if (!_respawning)
        {
            _respawning = true;
            _deadTimer = 0f;
        }

        _deadTimer += Time.unscaledDeltaTime;
        if (_deadTimer >= _respawnDelay)
        {
            _player.Respawn(_activeSpawn);
            _respawning = false;
        }
    }

    private void UpdateRoomCheckpoint(bool force)
    {
        if (_map == null || _player == null) return;
        if (!_map.TryGetCameraRoom(_player.transform.position, out CameraRoomData room, out _))
            return;

        bool changed = _currentRoom == null
            || room.startGridPos != _currentRoom.startGridPos
            || room.endGridPos != _currentRoom.endGridPos;

        if (!changed && !force) return;

        _currentRoom = room;
        if (room.hasSpawn)
            _activeSpawn = _map.GridToWorld(room.spawnGridPos);
    }

    public Vector3 ActiveSpawn => _activeSpawn;
}
