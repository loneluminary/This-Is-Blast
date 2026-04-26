using System;
using DG.Tweening;
using Lean.Pool;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using Utilities.Extensions;

public enum ShooterEntityState { InQueue, MovingToSlot, InSlot, Waiting, Exiting }

[RequireComponent(typeof(Collider))]
public class ShooterEntity : MonoBehaviour, ITappable
{
    [SerializeField] private Outline3D outline;
    [SerializeField] private Renderer blockRenderer;
    [SerializeField] private Transform feets;
    [SerializeField] private TextMeshPro countLabel;
    [SerializeField] private float moveSpeed = 15f;

    [Title("VFX")]
    [SerializeField, SceneObjectsOnly] private ParticleSystem shootVfx;

    [Title("SFX")]
    [SerializeField] AudioClip shootSfx;
    [SerializeField] AudioClip tapSfx;
    public AudioClip MergeSfx;

    private AudioSource _audioSource;
    private Collider _collider;
    private Coroutine _fireCoroutine;

    public ShooterEntityState State = ShooterEntityState.InQueue;

    public ColorType ColorType { get; private set; }
    public int RemainingCount { get; private set; }
    public int GridCol { get; private set; }
    public int GridRow { get; private set; }

    // Per-entity round-robin cursor independent from every other shooter
    private int _shotTargetIndex;

    public void Initialize(ShooterConfig config, Color visualColor, int col, int row)
    {
        ColorType = config.color;
        RemainingCount = config.shots;
        GridCol = col;
        GridRow = row;
        State = ShooterEntityState.InQueue;

        UpdateVisibility();

        _collider = GetComponent<Collider>();
        _audioSource = GetComponent<AudioSource>();

        blockRenderer.material.color = visualColor;
        RefreshLabel();
        SetInteractable(false);

        if (countLabel) countLabel.alpha = 0.5f; // visually distinguish queued shooters from those in slots
        feets?.gameObject.SetActive(false);
    }

    public void OnTapped()
    {
        if (State != ShooterEntityState.InQueue) return;

        if (ShooterManager.Instance.OnQueueShooterTapped(this))
        {
            PlaySfx(tapSfx);
        }
    }

    public void StartFiring(Projectile projectilePrefab, float shotDelay, float settleDelay, ShooterSlot slot)
    {
        GameManager.Instance.SetState(GameState.Shooting);

        _shotTargetIndex = 0;
        _fireCoroutine = StartCoroutine(FireRoutine(projectilePrefab, shotDelay, settleDelay, slot));
    }

    public void StopFiring()
    {
        if (_fireCoroutine == null) return;

        StopCoroutine(_fireCoroutine);
        _fireCoroutine = null;
    }

    private System.Collections.IEnumerator FireRoutine(Projectile projectilePrefab, float shotDelay, float settleDelay, ShooterSlot slot)
    {
        while (RemainingCount > 0)
        {
            GridBlock target = ShooterManager.Instance.GetNextFrontTarget(ColorType, ref _shotTargetIndex);

            if (target == null)
            {
                State = ShooterEntityState.Waiting;
                transform.DORotate(Vector3.zero, 0.1f).SetEase(Ease.OutSine);
                ShooterManager.Instance.OnShooterWaiting(this, slot);
                yield break;
            }

            FireOneShot(projectilePrefab, target, slot);

            yield return new WaitForSeconds(shotDelay);
        }

        yield return new WaitForSeconds(settleDelay);
        ShooterManager.Instance.OnShooterDepleted(this, slot);
    }

    private void FireOneShot(Projectile projectilePrefab, GridBlock target, ShooterSlot slot)
    {
        // Prevent multiple projectiles targeting the same block in the same firing sequence
        if (target.IsTargeted) return;
        target.MaskAsTargeted();

        DecrementCount();

        var origin = transform.position.WithAddY(0.25f);
        Projectile proj = LeanPool.Spawn(projectilePrefab, origin, Quaternion.identity);
        proj.Initialize(origin, target, ColorType, () => ShooterManager.Instance.OnProjectileHit(this, slot));

        transform.DOLookAt(target.transform.position, 0.1f).SetEase(Ease.OutSine);
        transform.DOShakePosition(0.1f, 0.1f).SetEase(Ease.OutSine);

        PlaySfx(shootSfx);
        if (shootVfx) shootVfx.Play();
    }

    public void MoveToSlot(ShooterSlot slot, Action onArrived)
    {
        State = ShooterEntityState.MovingToSlot;
        SetInteractable(false);

        MoveToPosition(slot.transform.position, onArrived: () =>
        {
            State = ShooterEntityState.InSlot;
            onArrived?.Invoke();
        });
    }

    /// Moves off-screen left or right (whichever edge is closer) then self-destructs.
    public void ExitOffScreen(Action onComplete = null)
    {
        State = ShooterEntityState.Exiting;
        SetInteractable(false);

        // Step forward slightly, then slide off the nearest screen edge
        MoveToPosition(transform.position + Vector3.forward * 1.2f, false, () =>
        {
            // Determine direction from screen-space X position
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            bool goLeft = screenPos.x <= Screen.width * 0.5f;

            float depth = Mathf.Abs(transform.position.z - Camera.main.transform.position.z);
            float offscreenNormalizedX = goLeft ? -0.3f : 1.3f;
            Vector3 viewportPoint = new(offscreenNormalizedX, 0.5f, depth);
            float targetX = Camera.main.ViewportToWorldPoint(viewportPoint).x;

            Vector3 target = new(targetX, transform.position.y, transform.position.z);

            MoveToPosition(target, false, () =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        });
    }

    public void MoveToPosition(Vector3 worldTarget, bool sitDownOnReach = true, Action onArrived = null)
    {
        feets?.gameObject.SetActive(true);

        transform.DOComplete();

        transform.DOMoveY(0.5f, 0.2f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            transform.DOMove(worldTarget.WithY(transform.position.y), moveSpeed).SetSpeedBased().SetEase(Ease.OutSine).OnComplete(() =>
            {
                if (sitDownOnReach)
                {
                    feets?.gameObject.SetActive(false);
                    transform.DOMoveY(0f, 0.2f).SetEase(Ease.OutExpo).OnComplete(() => onArrived?.Invoke());
                }
                else onArrived?.Invoke();
            });

            transform.DOLookAt(worldTarget.WithY(transform.position.y), 0.1f).SetEase(Ease.OutSine);
        });
    }

    public void AbsorbFrom(ShooterEntity donor)
    {
        RemainingCount += donor.RemainingCount;
        RefreshLabel();
    }

    public void DecrementCount()
    {
        RemainingCount = Mathf.Max(0, RemainingCount - 1);
        RefreshLabel();
    }

    public void SetInteractable(bool interactable)
    {
        if (_collider != null) _collider.enabled = interactable;
        if (outline) outline.enabled = interactable ? true : false;

        if (countLabel && interactable) countLabel.alpha = 1f;
    }

    public void SetGridRow(int newRow)
    {
        GridRow = newRow;

        UpdateVisibility();

        var shiftPos = ShooterManager.Instance.QueueGrid.GetWorldPosition(GridCol, GridRow);

        // Animate only if under visible rows for performence
        if (GridRow <= 2) MoveToPosition(shiftPos);
        else transform.position = shiftPos;
    }

    /// Simple row check to determine if block should be visible or not to save up performence 
    public void UpdateVisibility()
    {
        gameObject.SetActive(GridRow <= 2);
    }

    public void AddShots(int shots)
    {
        RemainingCount += shots;
        RefreshLabel();

        transform.DOPunchScale(Vector3.one * 0.3f, 0.2f);
    }

    private void RefreshLabel()
    {
        if (countLabel) countLabel.text = RemainingCount.ToString();
    }

    public void PlaySfx(AudioClip clip)
    {
        if (_audioSource && clip) _audioSource.PlayOneShot(clip);
    }
}

/// Implement on any world-space object that should respond to player tap/click via InputHandler.
public interface ITappable
{
    void OnTapped();
}