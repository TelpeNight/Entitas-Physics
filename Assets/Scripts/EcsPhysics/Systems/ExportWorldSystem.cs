using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace EcsPhysics.Systems
{
    class ExportWorldSystem : Entitas.IExecuteSystem
    {
        private readonly PhysicsContext m_context;

        public ExportWorldSystem(PhysicsContext context)
        {
            m_context = context;
        }

        public void Execute()
        {
            if (!m_context.hasPhysicsGeneration)
            {
                return;
            }
            
            if (!m_context.physicsSimulation.FinishHandle.IsCompleted)
            {
                Debug.LogWarning($"Physics {m_context.physicsSimulation.FinishHandle} wasn't completed during frame. May cause glitches");
            }
            m_context.physicsSimulation.FinishHandle.Complete();
            
            IReadOnlyList<GameEntity> entities = m_context.physicsGeneration.Value;
            ref PhysicsWorld world = ref m_context.physicsWorld.Value;
            int numDynamicBodies = world.NumDynamicBodies;
            Debug.Assert(entities.Count() >= numDynamicBodies);
            for (int i = 0; i < numDynamicBodies; ++i)
            {
                GameEntity e = entities[i];
                if (e.isEnabled)
                {
                    MotionData motionData = world.MotionDatas[i];
                    MotionVelocity motionVelocity = world.MotionVelocities[i];
                    RigidTransform worldFromBody =
                        math.mul(motionData.WorldFromMotion, math.inverse(motionData.BodyFromMotion));
                    e.ReplacePosition(worldFromBody.pos);
                    e.ReplaceRotation(worldFromBody.rot);
                    e.ReplacePhysicsVelocity(motionVelocity.LinearVelocity, motionVelocity.AngularVelocity);
                }
            }
        }
    }
}