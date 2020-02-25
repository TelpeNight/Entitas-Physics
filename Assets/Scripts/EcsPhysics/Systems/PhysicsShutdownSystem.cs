namespace EcsPhysics.Systems
{
    class PhysicsShutdownSystem : Entitas.ITearDownSystem
    {
        private readonly PhysicsContext m_context;

        public PhysicsShutdownSystem(PhysicsContext context)
        {
            m_context = context;
        }
        
        public void TearDown()
        {
            if (m_context.hasPhysicsSimulation)
            {
                m_context.physicsSimulation.FinishHandle.Complete();
                m_context.physicsSimulation.Simulation.FinalJobHandle.Complete();
            }
            if (m_context.hasPhysicsCollisionList)
            {
                m_context.physicsCollisionList.FinishHandle.Complete();
            }

            if (m_context.hasPhysicsSimulation)
            {
                m_context.physicsSimulation.Simulation.Dispose();
            }
            if (m_context.hasPhysicsWorld)
            {
                m_context.physicsWorld.Value.Dispose();
            }
            if (m_context.hasPhysicsCollisionList)
            {
                m_context.physicsCollisionList.Collisions.Dispose();
                m_context.physicsCollisionList.Triggers.Dispose();
            }
            if (m_context.hasPhysicsGeneration)
            {
                m_context.physicsGeneration.Release();
            }
            m_context.Reset();
        }
    }
}