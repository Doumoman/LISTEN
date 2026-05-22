using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Scene_MainLobby : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;

    private void Start()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnClickNewGame);

        if (loadGameButton != null)
            loadGameButton.onClick.AddListener(OnClickContinue);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnClickExit);
    }

    private void OnClickNewGame()
    {
        Debug.Log("[MainLobby] 새 게임 시작! 데이터 초기화 및 챕터 0 로드");

        // TODO: 세이브 데이터 초기화 로직
        SingletonManagers.Data.CreateNewGameData();
        SceneManager.LoadScene("Scene_Chapter0");
    }

    private void OnClickContinue()
    {
        Debug.Log("[MainLobby] 이어하기 - 세이브 데이터를 불러옵니다.");
        SingletonManagers.Data.LoadGame();
        // TODO: 세이브된 데이터 불러와서 마지막 세이브 포인트 위치로 
    }

    private void OnClickExit()
    {
        Debug.Log("[MainLobby] 게임 종료");
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveAllListeners();
        if (loadGameButton != null) loadGameButton.onClick.RemoveAllListeners();
        if (exitButton != null) exitButton.onClick.RemoveAllListeners();
    }
}
