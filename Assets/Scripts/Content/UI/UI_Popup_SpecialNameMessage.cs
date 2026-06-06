using System.Collections;
using TMPro;
using UnityEngine;

public class UI_Popup_SpecialNameMessage : UI_Popup
{
    enum Texts
    {
        MessageText
    }

    private CanvasGroup _canvasGroup;
    private string _message = "";

    [SerializeField] private float _fadeInDuration = 0.35f;
    [SerializeField] private float _holdDuration = 1.5f;
    [SerializeField] private float _fadeOutDuration = 0.6f;

    public void Setup(string message)
    {
        _message = message;
        ApplyMessage();
    }

    public override void Init()
    {
        base.Init();

        Bind<TextMeshProUGUI>(typeof(Texts));

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ApplyMessage();
        StartCoroutine(MessageSequence());
    }

    public override void OnSubmit()
    {
    }

    public override void OnCancel()
    {
    }

    private void ApplyMessage()
    {
        TextMeshProUGUI messageText = GetText((int)Texts.MessageText);

        if (messageText != null)
            messageText.text = string.IsNullOrEmpty(_message) ? "..." : _message;
    }

    private IEnumerator MessageSequence()
    {
        yield return Fade(0f, 1f, _fadeInDuration);
        yield return new WaitForSecondsRealtime(_holdDuration);
        yield return Fade(1f, 0f, _fadeOutDuration);

        ClosePopupUI();
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (_canvasGroup == null)
            yield break;

        float timer = 0f;
        _canvasGroup.alpha = from;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            _canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }
}
