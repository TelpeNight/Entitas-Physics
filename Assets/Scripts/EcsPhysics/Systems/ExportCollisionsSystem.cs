using System.Collections.Generic;
using System.Linq;
using Entitas;
using Unity.Physics;

namespace EcsPhysics.Systems
{
    public class ExportCollisionsSystem : IExecuteSystem
    {
        private readonly PhysicsContext m_physicsContext;

        public ExportCollisionsSystem(PhysicsContext physicsContext)
        {
            m_physicsContext = physicsContext;
        }
        
        public void Execute()
        {
            if (!m_physicsContext.hasPhysicsCollisionList || !m_physicsContext.hasPhysicsGeneration)
            {
                return;
            }
            
            IReadOnlyList<GameEntity> entities = m_physicsContext.physicsGeneration.Value;
            //TODO cache arrays and lists
            var collisions = new List<CollisionData>[entities.Count];
            var triggers = new List<GameEntity>[entities.Count];

            m_physicsContext.physicsCollisionList.FinishHandle.Complete();
            
            foreach (Physics.CollisionData collisionEvent in m_physicsContext.physicsCollisionList.Collisions)
            {
                GameEntity a = entities[collisionEvent.BodyIndexPair.BodyAIndex];
                GameEntity b = entities[collisionEvent.BodyIndexPair.BodyBIndex];
                if (a.isEnabled && b.isEnabled)
                {
                    if (collisions[collisionEvent.BodyIndexPair.BodyAIndex] == null)
                    {
                        collisions[collisionEvent.BodyIndexPair.BodyAIndex] = new List<CollisionData>(1);
                    }
                    if (collisions[collisionEvent.BodyIndexPair.BodyBIndex] == null)
                    {
                        collisions[collisionEvent.BodyIndexPair.BodyBIndex] = new List<CollisionData>(1);
                    }
                    collisions[collisionEvent.BodyIndexPair.BodyAIndex].Add(new CollisionData()
                    {
                        Collider = b,
                        AverageContactPointPosition = collisionEvent.AverageContactPointPosition
                    });
                    collisions[collisionEvent.BodyIndexPair.BodyBIndex].Add(new CollisionData()
                    {
                        Collider = a,
                        AverageContactPointPosition = collisionEvent.AverageContactPointPosition
                    });
                }
            }
            
            foreach (BodyIndexPair pair in m_physicsContext.physicsCollisionList.Triggers)
            {
                GameEntity a = entities[pair.BodyAIndex];
                GameEntity b = entities[pair.BodyBIndex];
                if (a.isEnabled && b.isEnabled)
                {
                    if (triggers[pair.BodyAIndex] == null)
                    {
                        triggers[pair.BodyAIndex] = new List<GameEntity>();
                    }
                    if (triggers[pair.BodyBIndex] == null)
                    {
                        triggers[pair.BodyBIndex] = new List<GameEntity>();
                    }
                    triggers[pair.BodyAIndex].Add(b);
                    triggers[pair.BodyBIndex].Add(a);
                }
            }

            for (int i = 0; i < entities.Count; ++i)
            {
                GameEntity e = entities[i];
                if (collisions[i] != null)
                {
                    e.AddCollision(collisions[i]);
                    Retain(e, e.collision, collisions[i].Select(c => c.Collider));
                }

                if (triggers[i] != null)
                {
                    e.AddTriggerContact(triggers[i]);
                    Retain(e, e.triggerContact, triggers[i]);
                }
            }
        }

        private static void Retain(IEntity gameEntity, IComponent collisionComponent, IEnumerable<GameEntity> list)
        {
            // ReSharper disable once ObjectCreationAsStatement
            new Retainer(gameEntity, collisionComponent, list);
        }

        private class Retainer
        {
            private readonly IComponent m_collisionComponent;
            private readonly List<GameEntity> m_list;

            public Retainer(IEntity gameEntity, IComponent collisionComponent, IEnumerable<GameEntity> list)
            {
                m_collisionComponent = collisionComponent;
                m_list = new List<GameEntity>(list);
                
                gameEntity.OnComponentReplaced += Replaced;
                gameEntity.OnComponentRemoved += Removed;

                foreach (GameEntity entity in m_list)
                {
                    entity.Retain(this);
                }
            }
            
            void Replaced(IEntity entity, int index, IComponent component, IComponent newComponent)
            {
                if (component == m_collisionComponent)
                {
                    Release();
                    entity.OnComponentReplaced -= Replaced;
                    entity.OnComponentRemoved -= Removed;
                }
            }

            private void Removed(IEntity entity, int index, IComponent component)
            {
                if (component == m_collisionComponent)
                {
                    Release();
                    entity.OnComponentReplaced -= Replaced;
                    entity.OnComponentRemoved -= Removed;
                }
            }
            
            private void Release()
            {
                foreach (GameEntity gameEntity in m_list)
                {
                    gameEntity.Release(this);
                }
            }
        }
    }
}