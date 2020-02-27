using System.Collections.Generic;
using Ecs.Components;
using Entitas;
using UnityEngine;

namespace Ecs.Systems
{
    public class ExplosionSystem : IExecuteSystem
    {
        private readonly GameObject m_explosionPrefab;
        private readonly IGroup<GameEntity> m_group;

        public ExplosionSystem(GameContext context, GameObject explosionPrefab)
        {
            m_explosionPrefab = explosionPrefab;
            m_group = context.GetGroup(GameMatcher.Collision.WithTag(GameTag.Bomb));
        }

        public void Execute()
        {
            foreach (GameEntity entity in m_group)
            {
                //TODO cache predicate
                CollisionData collision = entity.collision.Colliders.Find(e => e.Collider.HasTag(GameTag.Ground));
                if (collision != null)
                {
                    GameObject explosion = Object.Instantiate(m_explosionPrefab, collision.AverageContactPointPosition,
                        Quaternion.identity);
                    Object.Destroy(explosion, explosion.GetComponent<ParticleSystem>().main.duration);
                    entity.RemoveTag(GameTag.Bomb);
                }
            }
        }
    }
}