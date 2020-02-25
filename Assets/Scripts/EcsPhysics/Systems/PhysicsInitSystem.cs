using Entitas;
using Unity.Jobs;
using Unity.Physics;

namespace EcsPhysics.Systems
{
    class PhysicsInitSystem : IInitializeSystem
    {
        private readonly PhysicsContext m_context;

        public PhysicsInitSystem(PhysicsContext context)
        {
            m_context = context;
        }

        public void Initialize()
        {
            m_context.SetPhysicsWorld(new PhysicsWorld(0, 0, 0));
            m_context.SetPhysicsSimulation(new Simulation(), new JobHandle());
            m_context.SetPhysicsCollisionList(new JobHandle());
        }
    }
}