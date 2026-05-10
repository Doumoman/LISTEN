using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataManager : IManager
{
    private bool _init = false;
    private string _savePath;

    // 인게임에서 실시간으로 읽고 쓸 데이터 원본
    public GameData CurrentData { get; private set; }

    public void Init()
    {
        if (_init) return;
        _init = true;

        // 세이브 파일이 저장될 절대 경로 설정
        _savePath = Path.Combine(Application.persistentDataPath, "SaveData.json");
        LoadGame();
    }

    // 데이터 저장
    public void SaveGame()
    {
        // GameData 객체를 JSON 문자열로 변환
        string json = JsonUtility.ToJson(CurrentData, true);

        // 지정된 경로에 파일 쓰기
        // 현재는 로컬 저장 방식, 추후에 변경 가능
        File.WriteAllText(_savePath, json);
        Debug.Log($"[DataManager] 데이터 저장 완료: {_savePath}");
    }

    // 데이터 불러오기
    public void LoadGame()
    {
        if (File.Exists(_savePath))
        {
            try
            {
                string json = File.ReadAllText(_savePath);
                CurrentData = JsonUtility.FromJson<GameData>(json);
                Debug.Log($"[DataManager] 데이터 로드 완료: {_savePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataManager] 세이브 파일 로드 실패. 새 데이터를 생성합니다. Error: {e.Message}");
                CurrentData = new GameData();
            }
        }
        else
        {
            CreateNewGameData();
            Debug.Log("[DataManager] 세이브 파일이 없습니다. 새 게임 데이터를 생성했습니다.");
        }
    }

    public void CreateNewGameData()
    {
        CurrentData = new GameData();
        SaveGame();

        Debug.Log("[DataManager] 새 게임 데이터 초기화 완료!");
    }

    public void Clear()
    {
        // 필요 시 메모리에 있는 데이터 초기화 로직 작성
    }

    public void OnDestroy()
    {
        _init = false;
    }
}