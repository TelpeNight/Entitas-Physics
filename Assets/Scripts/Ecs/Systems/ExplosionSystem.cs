using System.Collections.Generic;
using Ecs.Components;
using Entitas;
using UnityEngine;

namespace Ecs.Systems
{
    public class ExplosionSystem : IExecuteSystem
    {
        private readonly GameContext m_context;
        private readonly IGroup<GameEntity> m_group;

        public ExplosionSystem(GameContext context)
        {
            m_context = context;
            m_group = context.GetGroup(GameMatcher.Collision.WithTag(GameTag.Bomb));
        }

        public void Execute()
        {
            foreach (GameEntity entity in m_group.AsEnumerable())
            {
                //TODO cache predicate
                if (entity.collision.Colliders.Exists(e => e.Collider.HasTag(GameTag.Ground)))
                {
                    Debug.Log("EXPLOSION!!!");
                    entity.RemoveTag(GameTag.Bomb);
                }
            }
        }
    }
}