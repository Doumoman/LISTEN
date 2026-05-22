using UnityEngine;

public class UI_Popup_Credit : UI_Popup
{
    enum GameObjects
    {
        Panel
    }

    enum Buttons
    {
        CloseButton
    }

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));
        Bind<UnityEngine.UI.Button>(typeof(Buttons));

        GetButton((int)Buttons.CloseButton).onClick.AddListener(() =>
        {
            ClosePopupUI();
        });
    }

    public override void OnCancel()
    {
        ClosePopupUI();
    }
}