using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "PowerUpDefinition", menuName = "ThisIsBlast/Power Up Definition")]
public class PowerUpDefinition : ScriptableObject
{
    [Title("Identity")]
    public PowerUpType Type;
    public string DisplayName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Title("Progression")]
    [Tooltip("Player must reach this level before the button appears.")]
    public int UnlockAtLevel = 3;

    [Title("Balance")]
    [Tooltip("How many times this power-up can be used per level.")]
    public int ChargesPerLevel = 1;

    [Title("ExtraShots Settings")]
    [ShowIf("Type", PowerUpType.ExtraShots)]
    public int BonusShotsAmount = 10;

    [Title("VFX/SFX")]
    public AudioClip AudioClip;
}

public enum PowerUpType
{
    Bomb,        // Destroys entire front row regardless of color — unlocks level 3
    ColorShift,  // Recolors one front-row column to match the active shooter — unlocks level 6
    ExtraShots   // Adds bonus shots to every shooter currently in a slot — unlocks level 10
}