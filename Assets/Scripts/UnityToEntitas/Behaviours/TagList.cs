using System.Collections.Generic;
using Ecs.Components;
using UnityEngine;

namespace UnityToEntitas.Behaviours
{
    public class TagList : MonoBehaviour, IConvertToEntitas
    {
        public List<GameTag> m_tags;
        
        public void Convert(GameEntity entity)
        {
            if (m_tags != null && m_tags.Count > 0)
            {
                entity.AddTags(new HashSet<GameTag>(m_tags));
            }
        }
    }
}