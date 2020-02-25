using Entitas;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Physics;

namespace EcsPhysics.Systems
{
    class CollectCollisionsSystem : IExecuteSystem
    {
        private readonly PhysicsContext m_context;

        public CollectCollisionsSystem(PhysicsContext context)
        {
            m_context = context;
        }

        public void Execute()
        {
            m_context.physicsCollisionList.FinishHandle.Complete();
            
            m_context.physicsCollisionList.Collisions.Clear();
            m_context.physicsCollisionList.Triggers.Clear();

            JobHandle collisions = new CollectCollisionJob()
            {
                Collisions = m_context.physicsCollisionList.Collisions,
                World = m_context.physicsWorld.Value
            }.Schedule(m_context.physicsSimulation.Simulation, ref m_context.physicsWorld.Value,
                m_context.physicsSimulation.FinishHandle);
            
            JobHandle triggers = new CollectTriggersJob()
            {
                Triggers = m_context.physicsCollisionList.Triggers
            }.Schedule(m_context.physicsSimulation.Simulation, ref m_context.physicsWorld.Value, 
                m_context.physicsSimulation.FinishHandle);
            
            
            m_context.physicsCollisionList.FinishHandle = JobHandle.CombineDependencies(collisions, triggers);
        }
        
        [BurstCompile]
        private struct CollectTriggersJob : ITriggerEventsJob
        {
            public NativeList<BodyIndexPair> Triggers;
            public void Execute(TriggerEvent triggerEvent)
            {
                Triggers.Add(triggerEvent.BodyIndices);
            }
        }

        [BurstCompile]
        private struct CollectCollisionJob : ICollisionEventsJob
        {
            public NativeList<Physics.CollisionData> Collisions;
            [ReadOnly]
            public PhysicsWorld World;
            public void Execute(CollisionEvent collisionEvent)
            {
                
                Collisions.Add(new Physics.CollisionData()
                {
                    BodyIndexPair = collisionEvent.BodyIndices,
                    AverageContactPointPosition = collisionEvent.CalculateDetails(ref World).AverageContactPointPosition
                });
            }
        }
    }
}