using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;
using Utilities.Extensions;

public class GridBlock : MonoBehaviour
{
    [SerializeField] Renderer blockRenderer;

    [Title("VFX")]
    [SerializeField, SceneObjectsOnly] private ParticleSystem hitVfx;

    [Title("SFX")]
    [SerializeField] private AudioClip hitSfx;
    private AudioSource _audioSource;

    public int GridCol { get; private set; }
    public int GridRow { get; private set; }
    public ColorType ColorType { get; private set; }
    public bool IsDestroyed { get; private set; }
    /// True once a projectile has been fired at this block.
    /// Prevents a second shot targeting the same block before it physically dies.
    public bool IsTargeted { get; private set; }

    private Tween _shiftTween;

    private void Awake()
    {
        if (!blockRenderer) blockRenderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
    }

    public void Initialize(int col, int row, ColorType colorType, Color color)
    {
        GridCol = col;
        GridRow = row;
        ColorType = colorType;
        IsDestroyed = false;

        UpdateVisibility();

        blockRenderer.material.color = color;
    }

    public void SetGridRow(int newRow)
    {
        GridRow = newRow;

        UpdateVisibility();

        var shiftPos = GridManager.Instance.GetCellWorldPosition(GridCol, GridRow);
        // Animate only if under visible rows for performence
        if (GridRow <= 5)
        {
            /// Animates the block sliding to its new world position after the column shifts forward.
            this.DelayedExecution(0.5f, () =>
            {
                _shiftTween?.Kill();
                _shiftTween = transform.DOMove(shiftPos, 10f).SetSpeedBased().SetEase(Ease.InOutExpo);
            });
        }
        else transform.position = shiftPos;
    }

    /// Changes this block's color type and visual. Used by the ColorShift power-up.
    public void Recolor(ColorType newColor)
    {
        ColorType = newColor;
        IsTargeted = false; // reset so it can be targeted by shooters of the new color

        blockRenderer.material.DOColor(GameManager.Instance.GetColor(newColor), 0.3f).SetEase(Ease.OutSine);
        transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 5);
    }

    /// Called at fire time. Reserves this block so no second projectile targets it.
    public void MaskAsTargeted() => IsTargeted = true;

    /// Simple row check to determine if block should be visible or not to save up performence 
    public void UpdateVisibility()
    {
        gameObject.SetActive(GridRow <= 5);
    }

    public void TriggerDestroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        GridManager.Instance.NotifyBlockDestroyed(this);

        transform.DOPunchScale(Vector3.one * 0.2f, 0.1f).OnComplete(() =>
        {
            transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
            {
                Destroy(gameObject);
            });
        });

        if (_audioSource && hitSfx) _audioSource.PlayOneShot(hitSfx);
        if (hitVfx) hitVfx.Play();
    }
}