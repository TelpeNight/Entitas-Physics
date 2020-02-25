using System.Collections.Generic;
using System.Linq;
using Ecs.Components;
using Entitas;
// ReSharper disable CheckNamespace

[Game]
public class TagsListComponent : IComponent
{
    public HashSet<GameTag> Value;
}

public partial class GameEntity
{
    public bool HasTag(GameTag tag)
    {
        return hasTagsList && tagsList.Value.Contains(tag);
    }

    public bool HasAllTags(IEnumerable<GameTag> tags)
    {
        return hasTagsList && this.tagsList.Value.IsSupersetOf(tags);
    }

    public void AddTag(GameTag tag)
    {
        if (hasTagsList && !HasTag(tag))
        {
            tagsList.Value.Add(tag);
            ReplaceComponent(GameComponentsLookup.TagsList, tagsList);
        }
        else if (!hasTagsList)
        {
            AddTags(new HashSet<GameTag>(new []{tag}));
        }
    }
    
    public void AddTags(IEnumerable<GameTag> tags)
    {
        List<GameTag> gameTags = tags.ToList();
        if (hasTagsList && !HasAllTags(gameTags))
        {
            tagsList.Value.UnionWith(gameTags);
            ReplaceComponent(GameComponentsLookup.TagsList, tagsList);
        }
        else if (!hasTagsList)
        {
            AddTagsList(new HashSet<GameTag>(gameTags));
        }
    }

    public void RemoveTag(GameTag tag)
    {
        if (HasTag(tag))
        {
            tagsList.Value.Remove(tag);
            ReplaceComponent(GameComponentsLookup.TagsList, tagsList);
        }
    }
}

public static class TagMatcherExtensions
{
    public static IMatcher<GameEntity> WithTag(this IMatcher<GameEntity> matcher, GameTag tag)
    {
        return new TagMatcher(matcher, tag);
    }

    private class TagMatcher : IMatcher<GameEntity>
    {
        private readonly IMatcher<GameEntity> m_matcher;
        private readonly GameTag m_tag;
        private readonly int m_hash;
        private string m_toString;

        public TagMatcher(IMatcher<GameEntity> matcher, GameTag tag)
        {
            m_matcher = matcher;
            m_tag = tag;
            m_hash = m_matcher.GetHashCode() ^ tag.GetHashCode();
            if (matcher.indices.Contains(GameComponentsLookup.TagsList))
            {
                indices = m_matcher.indices;
            }
            else
            {
                indices = new int[matcher.indices.Length + 1];
                matcher.indices.CopyTo(indices, 0);
                indices[indices.Length - 1] = GameComponentsLookup.TagsList;   
            }
        }
        
        public bool Matches(GameEntity entity)
        {
            return entity.HasTag(m_tag) && m_matcher.Matches(entity);
        }

        public int[] indices { get; }

        public override string ToString()
        {
            return m_toString ?? (m_toString = m_matcher + $".WithTag({m_tag})");
        }

        public override int GetHashCode()
        {
            return m_hash;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            switch (obj)
            {
                case null:
                    return false;
                case TagMatcher other:
                    return this.m_tag == other.m_tag && this.m_matcher.Equals(other.m_matcher);
                default:
                    return false;
            }
        }
    }
}