using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Attach to each power-up button in the HUD.
/// Handles locked / available / depleted visual states automatically.
[RequireComponent(typeof(Button))]
public class PowerUpButton : MonoBehaviour
{
    [SerializeField] private PowerUpType type;

    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject lockOverlay;       // greyed-out panel shown when locked
    [SerializeField] private GameObject depletedOverlay;   // shown when charges = 0 but unlocked
    [SerializeField] private TextMeshProUGUI chargesLabel;
    [SerializeField] private TextMeshProUGUI unlockLevelLabel; // e.g. "Lv.3" shown on lock overlay

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClicked);
    }

    /// Called by UIManager on level start and after every activation.
    public void Refresh(int level)
    {
        PowerUpDefinition def = PowerUpManager.Instance.GetDefinition(type);
        if (def == null) return;

        bool unlocked = PowerUpManager.Instance.IsUnlockedForLevel(type, level);
        bool canUse = PowerUpManager.Instance.CanUse(type);

        if (iconImage && def.Icon) iconImage.sprite = def.Icon;

        if (lockOverlay) lockOverlay.SetActive(!unlocked);
        if (unlockLevelLabel) unlockLevelLabel.text = $"Lv. {def.UnlockAtLevel}";

        // Depleted overlay (unlocked but out of charges)
        if (depletedOverlay) depletedOverlay.SetActive(unlocked && !canUse);

        if (chargesLabel)
        {
            chargesLabel.gameObject.SetActive(unlocked);

            if (unlocked)
            {
                chargesLabel.text = canUse ? PowerUpManager.Instance.GetRemainingCharges(type).ToString() : "0";
            }
        }

        _button.interactable = unlocked && canUse;

        // Entrance animation on first unlock
        if (unlocked && !_hasPlayedUnlockAnim)
        {
            _hasPlayedUnlockAnim = true;
            PlayUnlockAnimation();
        }
    }

    private bool _hasPlayedUnlockAnim;

    private void OnClicked()
    {
        PowerUpManager.Instance.Activate(type);
        PlayUseAnimation();
    }

    private void PlayUnlockAnimation()
    {
        transform.localScale = Vector3.zero;
        transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
    }

    private void PlayUseAnimation()
    {
        transform.DOComplete();
        transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 5).SetEase(Ease.OutBack);
    }
}