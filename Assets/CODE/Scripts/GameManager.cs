using System;
using Sirenix.OdinInspector;
using UnityEngine;

public enum GameState { Idle, Shooting, Win, Lose }

public class GameManager : MonoSingleton<GameManager>
{
    public Color[] ColorPalette; // indexed by ColorType enum

    [SerializeField] private LevelData[] levels;

    [ShowInInspector] public GameState CurrentState { get; private set; } = GameState.Idle;

    public int CurrentLevelIndex { get; private set; }

    public event Action<GameState> OnStateChanged;

    public const string LEVELS_PREFS = "UnlockedLevel";

    private void Start() => LoadLevel(PlayerPrefs.GetInt(LEVELS_PREFS, 0));

    public void LoadLevel(int index)
    {
        if (index < 0 || index >= levels.Length) { Debug.LogWarning($"Level index {index} out of range."); return; }

        CurrentLevelIndex = index;
        LevelData data = levels[index];

        GridManager.Instance.BuildGrid(data);
        ShooterManager.Instance.LoadLevel(data);
        PowerUpManager.Instance.Initialize(CurrentLevelIndex + 1);
        UIManager.Instance.Initialize(CurrentLevelIndex + 1);

        SetState(GameState.Idle);
    }

    public void RestartLevel() => LoadLevel(CurrentLevelIndex);

    public void GoToNextLevel()
    {
        int next = CurrentLevelIndex + 1;
        if (next < levels.Length)
        {
            PlayerPrefs.SetInt(LEVELS_PREFS, next); // Save the highest unlocked level index
            LoadLevel(next);
        }
        else LoadLevel(0); // wrap around to first level if we exceed available levels
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        switch (newState)
        {
            case GameState.Win:
                UIManager.Instance.ToggleWinScreen(true);
                UIManager.Instance.CoinAddingAnimation(new(Screen.width / 2f, Screen.height / 2f), () => CoinsManager.Instance.AddCoins(100));
                break;
            case GameState.Lose:
                UIManager.Instance.ToggleLoseScreen(true);
                break;
        }
    }

    public Color GetColor(ColorType colorType)
    {
        int index = (int)colorType;
        if (ColorPalette != null && index < ColorPalette.Length)
            return ColorPalette[index];

        // Fallback defaults
        return colorType switch
        {
            ColorType.Red => new Color(0.93f, 0.27f, 0.27f),
            ColorType.Blue => new Color(0.27f, 0.53f, 0.93f),
            ColorType.Yellow => new Color(0.98f, 0.82f, 0.15f),
            ColorType.Green => new Color(0.29f, 0.78f, 0.35f),
            ColorType.Purple => new Color(0.65f, 0.27f, 0.93f),
            _ => Color.white
        };
    }
}