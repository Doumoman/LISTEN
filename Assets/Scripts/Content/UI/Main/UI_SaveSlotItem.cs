using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_SaveSlotItem : MonoBehaviour
{
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _infoText;

    [Header("Focus")]
    [SerializeField] private Image _focusFrame;
    [SerializeField] private Color _normalColor = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.25f);
    [SerializeField] private float _normalScale = 1f;
    [SerializeField] private float _selectedScale = 1.03f;

    public void SetEmpty(int slotIndex)
    {
        if (_nameText != null)
            _nameText.text = $"빈 파일 {slotIndex + 1}";

        if (_infoText != null)
            _infoText.text = "새로운 이야기를 시작합니다.";

        if (_portraitImage != null)
            _portraitImage.enabled = false;
    }

    public void SetSelected(bool selected)
    {
        Color color = selected ? _selectedColor : _normalColor;

        if (_focusFrame != null)
            _focusFrame.color = color;

        if (_nameText != null)
            _nameText.color = color;

        transform.localScale = Vector3.one * (selected ? _selectedScale : _normalScale);
    }
}