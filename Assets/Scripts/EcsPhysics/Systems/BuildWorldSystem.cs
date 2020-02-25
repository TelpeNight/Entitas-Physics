using System.Collections.Generic;
using Entitas;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace EcsPhysics.Systems
{
    class BuildWorldSystem : IExecuteSystem
    {
        private readonly PhysicsContext m_physics;
        private readonly GameContext m_game;
        private ref PhysicsWorld PhysicsWorld => ref m_physics.physicsWorld.Value;

        private readonly List<GameEntity> m_entityBuffer = new List<GameEntity>();
        private readonly List<GameEntity> m_generation = new List<GameEntity>();
        private readonly IGroup<GameEntity> m_staticGroup;
        private readonly IGroup<GameEntity> m_dynamicGroup;

        public BuildWorldSystem(PhysicsContext physics, GameContext game)
        {
            m_physics = physics;
            m_game = game;

            m_staticGroup = game.GetGroup(GameMatcher
                .AllOf(GameMatcher.Position, GameMatcher.Rotation, GameMatcher.PhysicsCollider)
                .NoneOf(GameMatcher.PhysicsVelocity));
            m_dynamicGroup = game.GetGroup(GameMatcher
                .AllOf(GameMatcher.Position, GameMatcher.Rotation, GameMatcher.PhysicsVelocity));
        }

        //TODO check for changed static bodies
        public void Execute()
        {
            int numStaticBodies = m_staticGroup.count;
            int numDynamicBodies = m_dynamicGroup.count;

            StaticLayerChangeInfo staticLayerChangeInfo = new StaticLayerChangeInfo();
            staticLayerChangeInfo.Init(Allocator.TempJob);
            staticLayerChangeInfo.NumStaticBodies = numStaticBodies + 1;
            staticLayerChangeInfo.HaveStaticBodiesChanged = 0;

            if (numStaticBodies != (PhysicsWorld.StaticBodies.Length - 1)) //-1 for fake static body we add
            {
                // Quick test if number of bodies changed
                staticLayerChangeInfo.HaveStaticBodiesChanged = 1;
            }
            else
            {
                //TODO real check here. Static entities only
                staticLayerChangeInfo.NumStaticBodies = 0;
                staticLayerChangeInfo.HaveStaticBodiesChanged = 0;
            }

            PhysicsWorld.Reset(numStaticBodies + 1, numDynamicBodies, 0);

            //Create default static RB
            //TODO in parallel and not wait
            m_physics.physicsSimulation.FinishHandle.Complete();
            m_physics.physicsCollisionList.FinishHandle.Complete();
            NativeSlice<RigidBody> bodies = PhysicsWorld.Bodies;
            bodies[PhysicsWorld.NumBodies - 1] = new RigidBody
            {
                WorldFromBody = new RigidTransform(quaternion.identity, float3.zero)
            };

            m_generation.Clear();
            m_physics.SetPhysicsGeneration(m_generation);
            if (numDynamicBodies > 0)
            {
                List<GameEntity> entities = m_dynamicGroup.GetEntities(m_entityBuffer);
                m_generation.AddRange(entities);
                CreateRigidBody(bodies, 0, entities);
                CreateMotions(PhysicsWorld.MotionDatas, PhysicsWorld.MotionVelocities, entities);
            }

            if (numStaticBodies > 0)
            {
                List<GameEntity> entities = m_staticGroup.GetEntities(m_entityBuffer);
                m_generation.AddRange(entities);
                CreateRigidBody(bodies, numDynamicBodies, entities);
            }
            m_physics.physicsGeneration.Retain();

            Physics.StepComponent stepComponentComponent = Physics.StepComponent.Default;
            if (m_physics.hasPhysicsStep)
            {
                stepComponentComponent = m_physics.physicsStep;
            }

            float timeStep = m_game.time.UnityFixedDeltaTime;
            
            JobHandle handle = PhysicsWorld.CollisionWorld.Broadphase.ScheduleBuildJobs(
                ref PhysicsWorld, timeStep,
                stepComponentComponent.Gravity,
                stepComponentComponent.ThreadCountHint, ref staticLayerChangeInfo,
                m_physics.physicsSimulation.FinishHandle);
            m_physics.physicsSimulation.FinishHandle = handle;
            
            staticLayerChangeInfo.NumStaticBodiesArray.Dispose(handle);
            staticLayerChangeInfo.HaveStaticBodiesChangedArray.Dispose(handle);
        }

        private static unsafe void CreateRigidBody(NativeSlice<RigidBody> bodies, int startFromIndex, IReadOnlyList<GameEntity> entities)
        {
            for (int i = 0, rbIndex = startFromIndex; i < entities.Count; ++i, ++rbIndex)
            {
                GameEntity e = entities[i];
                bodies[rbIndex] = new RigidBody()
                {
                    WorldFromBody = new RigidTransform(e.rotation.Value, e.position.Value),
                    Collider = e.hasPhysicsCollider ? e.physicsCollider.ColliderPtr : null,
                    CustomTags = e.hasPhysicsCustomTags ? e.physicsCustomTags.Value : (byte)0
                };
            }
        }
        
        private static void CreateMotions(NativeSlice<MotionData> motions, NativeSlice<MotionVelocity> motionVelocities, IReadOnlyList<GameEntity> entities)
        {
            PhysicsMass defaultPhysicsMass = new PhysicsMass()
            {
                Transform = RigidTransform.identity,
                InverseMass = 0.0f,
                InverseInertia = float3.zero,
                AngularExpansionFactor = 1.0f,
            };

            // Create motion velocities
            float4 defaultInverseInertiaAndMass = new float4(defaultPhysicsMass.InverseInertia, defaultPhysicsMass.InverseMass);
            for (int i = 0; i < entities.Count; ++i)
            {
                GameEntity e = entities[i];
                motionVelocities[i] = new MotionVelocity()
                {
                    LinearVelocity = e.physicsVelocity.Linear,
                    AngularVelocity = e.physicsVelocity.Angular,
                    InverseInertiaAndMass = e.hasPhysicsMass ? new float4(e.physicsMass.Value.InverseInertia, e.physicsMass.Value.InverseMass) : defaultInverseInertiaAndMass,
                    AngularExpansionFactor = e.hasPhysicsMass ? e.physicsMass.Value.AngularExpansionFactor : defaultPhysicsMass.AngularExpansionFactor
                };
            }
            
            Physics.DampingComponent defaultPhysicsDamping = new Physics.DampingComponent()
            {
                Linear = 0.0f,
                Angular = 0.0f,
            };

            for (int i = 0; i < entities.Count; ++i)
            {
                GameEntity e = entities[i];
                motions[i] = CreateMotionData(
                    e.position, e.rotation,
                    e.hasPhysicsMass ? e.physicsMass.Value : defaultPhysicsMass,
                    e.hasPhysicsDamping ? e.physicsDamping : defaultPhysicsDamping,
                    e.hasPhysicsGravityFactor ? e.physicsGravityFactor.Value : e.hasPhysicsMass ? 1f : 0f
                );
            }
        }
        
        private static MotionData CreateMotionData(
            PositionComponent position, RotationComponent orientation,
            PhysicsMass physicsMass, Physics.DampingComponent damping,
            float gravityFactor)
        {
            return new MotionData
            {
                WorldFromMotion = new RigidTransform(
                    math.mul(orientation.Value, physicsMass.InertiaOrientation),
                    math.rotate(orientation.Value, physicsMass.CenterOfMass) + (float3)position.Value
                ),
                BodyFromMotion = physicsMass.Transform,
                LinearDamping = damping.Linear,
                AngularDamping = damping.Angular,
                GravityFactor = gravityFactor
            };
        }
    }
}