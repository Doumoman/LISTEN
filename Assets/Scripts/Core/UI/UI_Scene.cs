using UnityEngine;

public class UI_Scene : UI_Base
{
    public virtual bool KeepUIInputMode => false;
    public override void Init()
    {
        SingletonManagers.UI.RegisterSceneUI(this);
        SingletonManagers.UI.SetCanvas(gameObject, false);
    }
}
