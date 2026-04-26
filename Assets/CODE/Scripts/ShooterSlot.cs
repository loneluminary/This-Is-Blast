using Sirenix.OdinInspector;
using UnityEngine;

/// A fixed world-space position where a ShooterEntity can reside and fire from.
public class ShooterSlot : MonoBehaviour
{
    [ShowInInspector, ReadOnly, HideLabel, HorizontalGroup] public ShooterEntity CurrentEntity { get; private set; }
    [ShowInInspector, ReadOnly, HideLabel, HorizontalGroup(Width = 20f)] public bool IsEmpty => CurrentEntity == null;

    /// Marks the slot as occupied by the given entity.
    public void Occupy(ShooterEntity entity)
    {
        CurrentEntity = entity;
    }

    /// Marks the slot as available for a new entity.
    public void Free()
    {
        CurrentEntity = null;
    }
}