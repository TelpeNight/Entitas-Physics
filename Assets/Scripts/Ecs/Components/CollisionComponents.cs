using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

public class CollisionData
{
    public GameEntity Collider;
    public Vector3 AverageContactPointPosition;
}

[Game][Cleanup(CleanupMode.RemoveComponent)]
public class CollisionComponent : IComponent
{
    private List<CollisionData> m_colliders;

    public List<CollisionData> Colliders
    {
        set => m_colliders = value;
        get { return m_colliders?.FindAll(c => c.Collider.isEnabled); }
    }
}

[Game][Cleanup(CleanupMode.RemoveComponent)]
public class TriggerContactComponent : IComponent
{
    private List<GameEntity> m_colliders;

    public List<GameEntity> Colliders
    {
        set => m_colliders = value;
        get { return m_colliders?.FindAll(e => e.isEnabled); }
    }
}