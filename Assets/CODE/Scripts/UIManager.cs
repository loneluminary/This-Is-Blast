using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Utilities.Extensions;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;
using SceneLoading;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField, HideLabel] UIType Type;

    [Title("HUD")]
    [SerializeField, ShowIf("Type", UIType.Game)] private TextMeshProUGUI levelLabel;
    [SerializeField] private TextMeshProUGUI coinsLabel;
    [SerializeField, ShowIf("Type", UIType.Game)] private Slider progressBar;
    [SerializeField, ShowIf("Type", UIType.Game)] private Transform coinIcon;

    [Title("Win Screen")]
    [SerializeField, ShowIf("Type", UIType.Game)] private GameObject winScreen;
    [SerializeField, ShowIf("Type", UIType.Game)] private GameObject loseScreen;
    [SerializeField, ShowIf("Type", UIType.Game)] private GameObject pauseScreen;

    [Title("Power Ups")]
    [SerializeField, ShowIf("Type", UIType.Game)] private PowerUpButton[] powerUpButtons;

    [Title("SFX")]
    [SerializeField, ShowIf("Type", UIType.Game)] AudioClip winSFX;
    [SerializeField, ShowIf("Type", UIType.Game)] AudioClip loseSFX;
    [SerializeField, ShowIf("Type", UIType.MainMenu)] AudioSource musicSource;

    [Title("Toasts")]
    [SerializeField] private RectTransform toastPopupContainer;
    [SerializeField] private CanvasGroup toastPopupTemplate;
    private readonly List<string> _currentToasts = new();

    private void Awake()
    {
        if (toastPopupTemplate)
        {
            toastPopupTemplate.alpha = 0f;
            toastPopupTemplate.gameObject.SetActive(false);
        }

        // Destroy duplicated music when going back to main menu from game scene
        if (musicSource)
        {
            DontDestroyOnLoad(musicSource.gameObject);
            foreach (var audio in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (audio.clip == musicSource.clip && audio != musicSource) Destroy(musicSource.gameObject);
            }
        }
    }

    public void Initialize(int level)
    {
        if (winScreen) winScreen.SetActive(false);
        if (loseScreen) loseScreen.SetActive(false);
        if (pauseScreen) pauseScreen.SetActive(false);

        UpdateLevelLabel(level);
        UpdateProgressBar(0f);
        UpdateCoinsText();

        RefreshPowerUpButtons();
    }

    public void RefreshPowerUpButtons()
    {
        if (powerUpButtons == null) return;

        foreach (PowerUpButton btn in powerUpButtons)
            btn.Refresh(GameManager.Instance.CurrentLevelIndex + 1);
    }

    public void UpdateCoinsText()
    {
        if (!coinsLabel) return;

        int cash = CoinsManager.Instance.CurrentCoins;

        coinsLabel.DOComplete();
        if (int.TryParse(coinsLabel.text, out int prev) && cash > prev) coinsLabel.DOColor(Color.green, 0.2f).SetLoops(2, LoopType.Yoyo).SetUpdate(true);
        else coinsLabel.DOColor(Color.red, 0.2f).SetLoops(2, LoopType.Yoyo).SetUpdate(true);

        coinsLabel.text = $"{cash:N0}";
    }

    public void UpdateLevelLabel(int levelNumber)
    {
        if (levelLabel != null) levelLabel.text = $"Level: {levelNumber}";
    }

    public void UpdateProgressBar(float normalizedValue)
    {
        if (progressBar == null) return;

        progressBar.DOKill();
        progressBar.DOValue(normalizedValue, 0.3f).SetEase(Ease.OutSine);
    }

    public void ToggleWinScreen(bool toggle)
    {
        if (!winScreen) return;

        winScreen.SetActive(toggle);

        if (toggle)
        {
            var popup = winScreen.transform.GetChild(0);
            popup.localScale = Vector3.zero;
            popup.DOScale(0.8f, 0.5f).SetEase(Ease.OutBack);

            if (winSFX) AudioSource.PlayClipAtPoint(winSFX, Camera.main.transform.position);
        }
    }

    public void ToggleLoseScreen(bool toggle)
    {
        if (!loseScreen) return;

        loseScreen.SetActive(toggle);

        if (toggle)
        {
            var popup = loseScreen.transform.GetChild(0);
            popup.localScale = Vector3.zero;
            popup.DOScale(0.8f, 0.5f).SetEase(Ease.OutBack);

            if (loseSFX) AudioSource.PlayClipAtPoint(loseSFX, Camera.main.transform.position);
        }
    }

    public void TogglePauseScreen(bool toggle)
    {
        if (!pauseScreen) return;

        pauseScreen.SetActive(toggle);
        Time.timeScale = toggle ? 0f : 1f;

        if (toggle)
        {
            var popup = pauseScreen.transform.GetChild(0);
            popup.localScale = Vector3.zero;
            popup.DOScale(0.8f, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    public void ShowToastMessage(string text, float duration = 3f)
    {
        if (!toastPopupContainer || !toastPopupTemplate || _currentToasts.Contains(text)) return;

        var popup = Instantiate(toastPopupTemplate, toastPopupContainer);
        popup.GetComponentInChildren<TMP_Text>().text = text;
        _currentToasts.Add(text);

        popup.gameObject.SetActive(true);
        popup.DOFade(1f, 0.3f).OnComplete(() =>
        {
            popup.DOFade(0f, 0.3f).SetDelay(duration).OnComplete(() =>
            {
                _currentToasts.Remove(text);
                Destroy(popup.gameObject);
            });
        });
    }

    public void CoinAddingAnimation(Vector3 startPos, System.Action onComplete = null)
    {
        var masterSeq = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < 7; i++)
        {
            var coin = Instantiate(coinIcon, startPos, Quaternion.identity, coinIcon.root);
            coin.gameObject.SetActive(true);
            coin.localScale = Vector3.zero;

            var random = new Vector3(Random.Range(-50f, 50f), Random.Range(-100f, 100f));

            var seq = DOTween.Sequence()
                .Append(coin.DOScale(1f, 0.5f).SetEase(Ease.OutQuad))
                .Join(coin.DOMove(startPos + random, 0.5f).SetEase(Ease.InSine))
                .Append(coin.DOMove(coinsLabel.transform.position.WithZ(0f), 0.5f).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(coin.gameObject));

            masterSeq.Join(seq);
        }

        masterSeq.OnComplete(() => onComplete?.Invoke());
    }

    public void Play() => LoadingScreen.HasInstance?.Load(1);
    public void MainMenu() => LoadingScreen.HasInstance?.Load(0);
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private enum UIType
    {
        Game,
        MainMenu
    }
}