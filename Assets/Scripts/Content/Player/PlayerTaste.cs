using UnityEngine;

/// <summary>
/// 미각(열매) 효과 종류.
/// None  : 효과 없음
/// Antidote(해독) : 가스 면역
/// Vision(광명)   : 시야 반경 증가 + 위장 블록의 정체 표시
/// Phase(위상)    : 위상 게이트 통과
/// Heat(작열)     : 용암/열 면역
/// </summary>
public enum TasteKind { None, Antidote, Vision, Phase, Heat }

/// <summary>
/// 플레이어가 먹은 열매 효과를 관리한다. 일정 시간 후 효과가 사라진다.
/// 다른 감각 기믹(GasZone, DisguisedBlock, FruitGate 등)이 이 컴포넌트를 조회한다.
/// </summary>
public class PlayerTaste : MonoBehaviour
{
    [SerializeField] private TasteKind _current = TasteKind.None;
    [SerializeField] private float _remaining;
    [SerializeField] private float _visionBonusRadius = 4f;

    /// <summary>현재 유효한 효과(시간이 남아있을 때만).</summary>
    public TasteKind Current => _remaining > 0f ? _current : TasteKind.None;
    public float Remaining => Mathf.Max(0f, _remaining);

    public bool Has(TasteKind kind) => kind != TasteKind.None && Current == kind;

    public bool HasGasImmunity => Has(TasteKind.Antidote);
    public bool HasVision => Has(TasteKind.Vision);
    public bool HasPhase => Has(TasteKind.Phase);
    public bool HasHeat => Has(TasteKind.Heat);

    public float VisionBonus => HasVision ? _visionBonusRadius : 0f;

    /// <summary>열매를 먹는다. 같은/다른 열매를 먹으면 효과가 갱신된다.</summary>
    public void Eat(TasteKind kind, float duration)
    {
        _current = kind;
        _remaining = Mathf.Max(0f, duration);
    }

    public void ClearTaste()
    {
        _current = TasteKind.None;
        _remaining = 0f;
    }

    private void Update()
    {
        if (_remaining > 0f)
            _remaining -= Time.deltaTime;
    }
}
