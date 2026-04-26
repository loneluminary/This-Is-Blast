using System.Collections.Generic;
using UnityEngine;
using Utilities.Extensions;

/// Owns all power-up execution logic and per-level charge tracking.
public class PowerUpManager : MonoSingleton<PowerUpManager>
{
    [SerializeField] private PowerUpDefinition[] definitions;

    // remaining charges this level, keyed by PowerUpType
    private readonly Dictionary<PowerUpType, int> _charges = new();

    public void Initialize(int level)
    {
        _charges.Clear();

        foreach (PowerUpDefinition def in definitions)
            _charges[def.Type] = def.UnlockAtLevel <= level ? def.ChargesPerLevel : 0;
    }

    public bool CanUse(PowerUpType type) => _charges.TryGetValue(type, out int c) && c > 0;
    public int GetRemainingCharges(PowerUpType type) => _charges.TryGetValue(type, out int c) ? c : 0;

    public PowerUpDefinition GetDefinition(PowerUpType type)
    {
        foreach (PowerUpDefinition def in definitions) if (def.Type == type) return def;
        return null;
    }

    /// Returns true if the power-up is unlocked for the given level number.
    public bool IsUnlockedForLevel(PowerUpType type, int levelNumber)
    {
        PowerUpDefinition def = GetDefinition(type);
        return def != null && levelNumber >= def.UnlockAtLevel;
    }

    public void Activate(PowerUpType type)
    {
        if (!CanUse(type))
        {
            UIManager.Instance.ShowToastMessage("Power-up not available!");
            return;
        }

        GameState state = GameManager.Instance.CurrentState;
        if (state == GameState.Win || state == GameState.Lose) return;

        _charges[type]--;

        var def = GetDefinition(type);

        switch (type)
        {
            case PowerUpType.Bomb: ActivateBomb(def); break;
            case PowerUpType.ColorShift: ActivateColorShift(def); break;
            case PowerUpType.ExtraShots: ActivateExtraShots(def); break;
        }

        UIManager.Instance.RefreshPowerUpButtons();
    }

    /// Destroys every block in the front row regardless of color.
    private void ActivateBomb(PowerUpDefinition definition)
    {
        var frontBlocks = GridManager.Instance.GetEntireFrontRow();
        if (frontBlocks.IsNullOrEmpty()) return;

        foreach (GridBlock block in frontBlocks)
        {
            if (!block.IsDestroyed)
                block.TriggerDestroy();
        }

        UIManager.Instance.UpdateProgressBar(GridManager.Instance.GetClearProgress());

        if (GridManager.Instance.AllBlocksCleared())
        {
            ShooterManager.Instance.StopAllShooters();
            GameManager.Instance.SetState(GameState.Win);
        }

        if (definition.AudioClip) AudioSource.PlayClipAtPoint(definition.AudioClip, Camera.main.transform.position);
    }

    /// Recolors the front-row to match the first active shooter's color.
    private void ActivateColorShift(PowerUpDefinition definition)
    {
        ShooterSlot activeSlot = ShooterManager.Instance.GetFirstOccupiedSlot();
        if (activeSlot == null)
        {
            UIManager.Instance.ShowToastMessage("No active shooter to match!\nplease doc a shooter first");
            _charges[PowerUpType.ColorShift]++; // refund
            return;
        }

        ColorType targetColor = activeSlot.CurrentEntity.ColorType;
        foreach (var block in GridManager.Instance.GetEntireFrontRow())
        {
            block.Recolor(targetColor);
        }

        if (definition.AudioClip) AudioSource.PlayClipAtPoint(definition.AudioClip, Camera.main.transform.position);

        ShooterManager.Instance.CheckWaitingShooters();
    }

    /// Adds bonus shots to every shooter currently occupying a slot.
    private void ActivateExtraShots(PowerUpDefinition definition)
    {
        int bonus = definition ? definition.BonusShotsAmount : 10;

        if (ShooterManager.Instance.AddShotsToAllSlotShooters(bonus))
        {
            UIManager.Instance.ShowToastMessage($"+{bonus} shots added!");
            if (definition.AudioClip) AudioSource.PlayClipAtPoint(definition.AudioClip, Camera.main.transform.position);
        }
    }
}