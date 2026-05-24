using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UI_FocusMenuItem : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _label;

    [Header("Color")]
    [SerializeField] private Color _normalColor = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.25f);

    [Header("Scale")]
    [SerializeField] private float _normalScale = 1f;
    [SerializeField] private float _selectedScale = 1.08f;

    private RectTransform _rect;
    private Vector2 _originAnchoredPos;
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect != null)
            _originAnchoredPos = _rect.anchoredPosition;
    }

    public void SetText(string text)
    {
        if (_label != null)
            _label.text = text;
    }

    public void SetSelected(bool selected)
    {
        Color color = selected ? _selectedColor : _normalColor;
        float scale = selected ? _selectedScale : _normalScale;

        if (_icon != null)
            //_icon.color = color;

        if (_label != null)
            _label.color = color;

        transform.localScale = Vector3.one * scale;
    }

    public IEnumerator PlayShake(float duration = 0.25f, float power = 5f)
    {
        if (_rect == null)
            yield break;

        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float x = Random.Range(-power, power);
            float y = Random.Range(-power, power);

            _rect.anchoredPosition = _originAnchoredPos + new Vector2(x, y);

            yield return null;
        }

        _rect.anchoredPosition = _originAnchoredPos;
    }
}