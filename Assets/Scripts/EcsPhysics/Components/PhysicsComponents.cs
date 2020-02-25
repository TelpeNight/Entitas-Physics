using Entitas;
using Entitas.CodeGeneration.Attributes;
using Entitas.VisualDebugging.Unity;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;

// ReSharper disable once CheckNamespace
namespace Physics
{
    [Game]
    public class ColliderComponent : IComponent
    {
        private BlobAssetReference<Collider> m_value;    //XXX leaks

        public BlobAssetReference<Collider> Value
        {
            get => m_value;
            set => m_value = value;
        } // null is allowed
        public bool IsValid => Value.IsCreated;
        public unsafe Collider* ColliderPtr => (Collider*)Value.GetUnsafePtr();
        public MassProperties MassProperties => Value.IsCreated ? Value.Value.MassProperties : MassProperties.UnitSphere;
    }

    [Game]
    public class CustomTagsComponent : IComponent
    {
        public byte Value;
    }

    [Game]
    public class VelocityComponent : IComponent
    {
        public Vector3 Linear;
        public Vector3 Angular;
    }

    [Game][DontDrawComponent]
    public class MassComponent : IComponent
    {
        public PhysicsMass Value;
    }
    
    [Game]
    public class DampingComponent : IComponent
    {
        public float Linear;
        public float Angular;
    }
    
    [Game]
    public class GravityFactorComponent : IComponent
    {
        public float Value;
    }
}