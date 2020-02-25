using Entitas;

namespace EcsPhysics.Systems
{
    public class CleanupGenerationSystem : IExecuteSystem
    {
        private readonly PhysicsContext m_context;

        public CleanupGenerationSystem(PhysicsContext context)
        {
            m_context = context;
        }

        public void Execute()
        {
            if (m_context.hasPhysicsGeneration)
            {
                m_context.physicsGeneration.Release();
                m_context.RemovePhysicsGeneration();
            }
        }
    }
}