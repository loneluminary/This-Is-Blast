using UnityEngine;
using System.Linq;
using Utilities.Extensions;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;

public class ShooterManager : MonoSingleton<ShooterManager>
{
    [SerializeField] private ShooterSlot[] slots;
    public ShooterQueueGrid QueueGrid;

    [Title("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float delayBetweenShots = 0.07f;  // feel: rapid-fire stagger
    [SerializeField] private float postFireSettleDelay = 0.35f; // pause after last shot before state change

    private void Start()
    {
        GridManager.Instance.OnGridShifted += CheckWaitingShooters;
    }

    public void LoadLevel(LevelData data)
    {
        FreeAllSlots();
        QueueGrid.BuildGrid(data);
    }

    private void FreeAllSlots()
    {
        foreach (ShooterSlot slot in slots) slot.Free();
    }

    public bool OnQueueShooterTapped(ShooterEntity entity)
    {
        GameState state = GameManager.Instance.CurrentState;
        if (state == GameState.Win || state == GameState.Lose) return false;

        ShooterSlot targetSlot = slots.FirstOrDefault(s => s.IsEmpty);
        if (targetSlot == null) return false;

        QueueGrid.NotifyEntityTakenFromFrontRow(entity.GridCol);
        targetSlot.Occupy(entity);

        entity.MoveToSlot(targetSlot, () =>
        {
            if (!CheckAndMergeSlots())
            {
                entity.StartFiring(projectilePrefab, delayBetweenShots, postFireSettleDelay, targetSlot);
            }
        });

        return true;
    }

    /// Called on every projectile landing. Checks for win mid-sequence.
    public void OnProjectileHit(ShooterEntity entity, ShooterSlot slot)
    {
        UIManager.Instance.UpdateProgressBar(GridManager.Instance.GetClearProgress());

        if (GridManager.Instance.AllBlocksCleared())
        {
            StopAllShooters();
            GameManager.Instance.SetState(GameState.Win);
        }
    }

    /// Called when an entity's count reaches zero.
    public void OnShooterDepleted(ShooterEntity entity, ShooterSlot slot)
    {
        UIManager.Instance.UpdateProgressBar(GridManager.Instance.GetClearProgress());

        slot.Free();
        entity.ExitOffScreen();

        // if blocks remain but queue is empty and no shooters on slots, it's a loss
        if (IsQueueEmpty() && !HasOccupiedSlots())
        {
            GameManager.Instance.SetState(GameState.Lose);
            return;
        }
    }

    /// Called when an entity finds no matching front block and must wait.
    public void OnShooterWaiting(ShooterEntity entity, ShooterSlot slot)
    {
        if (AllSlotsStuck())
        {
            // StopAllShooters();
            GameManager.Instance.SetState(GameState.Lose);

            return;
        }

        GameManager.Instance.SetState(GameState.Idle);
    }

    /// Scans for waiting shooters whose color now has a front block after a grid shift.
    /// Each one resumes independently multiple can fire concurrently.
    public void CheckWaitingShooters()
    {
        GameState state = GameManager.Instance.CurrentState;
        if (state == GameState.Win || state == GameState.Lose) return;

        foreach (ShooterSlot slot in slots)
        {
            if (slot.IsEmpty) continue;

            ShooterEntity entity = slot.CurrentEntity;
            if (entity == null || entity.State != ShooterEntityState.Waiting) continue;

            if (!GridManager.Instance.GetFrontBlocksOfColor(entity.ColorType).IsNullOrEmpty())
            {
                entity.State = ShooterEntityState.InSlot;
                entity.StartFiring(projectilePrefab, delayBetweenShots, postFireSettleDelay, slot);
            }
        }
    }

    /// If min 3 occupied slot shares the same color, merges all counts into the
    private bool CheckAndMergeSlots()
    {
        // get all occupied slots that are ready (not moving)
        var readySlots = slots.Where(s => !s.IsEmpty && s.CurrentEntity.State != ShooterEntityState.MovingToSlot).ToList();
        if (readySlots.Count < 3) return false;

        // group by color and find the first group that has 3 or more
        var matchGroup = readySlots.GroupBy(s => s.CurrentEntity.ColorType).FirstOrDefault(g => g.Count() >= 3);
        if (matchGroup == null) return false;

        // we have a match, get the specific slots involved.
        List<ShooterSlot> matchedSlots = matchGroup.ToList();

        // pick a survivor (the middle one of the matched list looks best visually)
        int survivorIndex = matchedSlots.Count / 2;
        ShooterSlot survivorSlot = matchedSlots[survivorIndex];
        ShooterEntity survivorEntity = survivorSlot.CurrentEntity;

        // collect all entities in the match except the survivor
        List<ShooterEntity> allEntitiesToMerge = matchedSlots.Where(s => s != survivorSlot).Select(s => s.CurrentEntity).ToList();

        // stop firing and free the non-survivor slots
        foreach (var entity in allEntitiesToMerge) entity.StopFiring();
        survivorEntity.StopFiring();

        foreach (var slot in matchedSlots) if (slot != survivorSlot) slot.Free();

        // Play Animation Sequence
        const float riseHeight = 1.5f, riseTime = 0.25f, flyTime = 0.2f;

        survivorEntity.transform.DOKill();
        survivorEntity.transform.DORotate(Vector3.zero, 0.1f).SetEase(Ease.OutSine);
        survivorEntity.transform.DOMoveY(riseHeight, riseTime).SetEase(Ease.OutExpo);

        var seq = DOTween.Sequence().OnComplete(() =>
        {
            survivorEntity.PlaySfx(survivorEntity.MergeSfx);
            survivorEntity.transform.DOMoveY(0f, riseTime).SetEase(Ease.OutExpo);
            survivorEntity.transform.DOPunchScale(Vector3.one * 0.4f, 0.3f).OnComplete(() =>
            {
                survivorEntity.StartFiring(projectilePrefab, delayBetweenShots, postFireSettleDelay, survivorSlot);
            });
        });

        foreach (var entity in allEntitiesToMerge)
        {
            entity.transform.DOKill();
            entity.transform.DORotate(Vector3.zero, 0.1f).SetEase(Ease.OutSine);
            seq.Join(entity.transform.DOMoveY(riseHeight, riseTime).SetEase(Ease.OutExpo));
        }

        seq.AppendInterval(0f);

        foreach (var entity in allEntitiesToMerge)
        {
            entity.transform.DOComplete();
            seq.Join(entity.transform.DOMoveX(survivorEntity.transform.position.x, flyTime).SetEase(Ease.OutExpo).OnComplete(() =>
            {
                survivorEntity.AbsorbFrom(entity);
                Destroy(entity.gameObject);
            }));
        }

        seq.Play();

        return true;
    }

    /// Returns the next front-row block of the given color.
    /// <paramref name="index"/> is the caller's own round-robin cursor, passed by ref so each entity advances independently without sharing state.
    public GridBlock GetNextFrontTarget(ColorType color, ref int index)
    {
        List<GridBlock> front = GridManager.Instance.GetFrontBlocksOfColor(color, false);
        if (front.Count == 0) return null;

        index %= front.Count; // guard against list shrinking mid-fire
        GridBlock target = front[index];
        index = (index + 1) % front.Count;

        return target;
    }

    public ShooterSlot GetFirstOccupiedSlot()
    {
        foreach (ShooterSlot slot in slots) if (!slot.IsEmpty) return slot;
        return null;
    }

    public bool AddShotsToAllSlotShooters(int bonus)
    {
        bool anyShooter = false;
        foreach (ShooterSlot slot in slots)
        {
            if (slot.IsEmpty) continue;
            slot.CurrentEntity?.AddShots(bonus);
            anyShooter = true;
        }

        return anyShooter;
    }

    /// Every slot occupied and every occupant waiting truly unwinnable.
    public bool AllSlotsStuck()
    {
        foreach (ShooterSlot slot in slots)
        {
            if (slot.IsEmpty) return false;

            if (slot.CurrentEntity.State != ShooterEntityState.Waiting) return false;

            // If real (non-destroyed) front blocks of this color still exist,
            // if (GridManager.Instance.HasFrontBlocksOfColor(slot.CurrentEntity.ColorType)) return false;
        }

        return true;
    }

    public void StopAllShooters()
    {
        foreach (ShooterSlot s in slots)
        {
            if (s.IsEmpty) continue;
            s.CurrentEntity?.StopFiring();
            s.CurrentEntity?.ExitOffScreen();
            s.Free();
        }
    }

    public bool IsQueueEmpty() => QueueGrid == null || QueueGrid.IsEmpty;
    public bool HasOccupiedSlots() => slots.Any(s => !s.IsEmpty);

    private void OnDestroy()
    {
        GridManager.Instance.OnGridShifted -= CheckWaitingShooters;
    }
}