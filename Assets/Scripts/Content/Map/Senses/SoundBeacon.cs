using UnityEngine;

/// <summary>
/// 청각 기믹 — 플레이어가 가까울수록 소리가 커지는 비콘.
/// 진짜 포탈에 붙여두면, 플레이어는 '소리가 가장 크게 들리는 곳'을 향해 진짜 포탈을 찾는다.
/// AudioClip이 없으면 절차적으로 사인파 톤을 생성한다(별도 에셋 불필요).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundBeacon : MonoBehaviour
{
    [SerializeField] private float _maxDistance = 12f;
    [SerializeField] private float _maxVolume = 1f;
    [SerializeField] private float _frequency = 440f;
    [SerializeField] private bool _generateTone = true;
    [SerializeField] private float _minPitch = 0.7f;
    [SerializeField] private float _maxPitch = 1.5f;

    private AudioSource _source;
    private Transform _player;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.loop = true;
        _source.spatialBlend = 0f;     // 2D — 거리 감쇠는 우리가 직접 계산
        _source.playOnAwake = false;
        _source.volume = 0f;

        if (_generateTone && _source.clip == null)
            _source.clip = CreateTone(_frequency);
    }

    private void Start()
    {
        PlayerFSM player = FindFirstObjectByType<PlayerFSM>();
        if (player != null) _player = player.transform;

        if (_source.clip != null && Application.isPlaying)
            _source.Play();
    }

    private void Update()
    {
        if (_player == null) return;

        float distance = Vector2.Distance(_player.position, transform.position);
        float t = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, _maxDistance));

        _source.volume = t * t * _maxVolume; // 가까울수록 가파르게 커짐
        _source.pitch = Mathf.Lerp(_minPitch, _maxPitch, t);
    }

    private AudioClip CreateTone(float frequency)
    {
        int sampleRate = 44100;
        int length = sampleRate;          // 1초 루프
        float[] data = new float[length];

        for (int i = 0; i < length; i++)
        {
            float phase = 2f * Mathf.PI * frequency * i / sampleRate;
            // 약간의 배음을 섞어 단조롭지 않게
            data[i] = (Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2f) * 0.15f) * 0.6f;
        }

        AudioClip clip = AudioClip.Create("BeaconTone", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _maxDistance);
    }
}
