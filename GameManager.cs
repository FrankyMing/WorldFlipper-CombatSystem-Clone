using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Prepare, Playing, Stop, GameOver,GameWin }
public class GameManager : MonoBehaviour
{
    // 單例模式
    public static GameManager Instance { get; private set; }
    //定義廣播事件
    public static event System.Action<GameState> OnGameStateChangedEvent;

    private GameState _currentState;
    public GameState CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState == value) return;
            _currentState = value;
            //發送廣播
            OnGameStateChangedEvent?.Invoke(_currentState);
            OnGameStateChanged(_currentState);
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 初始狀態設為準備
        CurrentState = GameState.Prepare;
    }
    public void StartGame()
    {
        if (CurrentState == GameState.Prepare)
        {
            CurrentState = GameState.Playing;
        }
    }
    public void TogglePause()
    {
        if (CurrentState == GameState.Playing)
        {
            CurrentState = GameState.Stop;
        }
        else if (CurrentState == GameState.Stop)
        {
            CurrentState = GameState.Playing;
        }
    }
    public void RetryGame()
    {
        CurrentState = GameState.Prepare;
    }
    public void SetGameOver()
    {
        CurrentState = GameState.GameOver;
    }
    public void SetGameWin()
    {
        if (CurrentState == GameState.Playing)
        {
            CurrentState = GameState.GameWin;
        }
    }
    //全域邏輯行為中心
    private void OnGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Prepare:
                
                break;

            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Stop:
                Time.timeScale = 0f;
                break;

            case GameState.GameOver:
                break;
            case GameState.GameWin:

                break;
        }
    }
}
