using UnityEngine;

namespace Ecs.Systems
{
    public class TimeSystem : Entitas.IExecuteSystem
    {
        private readonly GameContext m_context;

        public TimeSystem(GameContext context)
        {
            m_context = context;
        }

        public void Execute()
        {
            int delta = (int)(Time.deltaTime * 1000);
            int fixedDelta = (int) (Time.fixedDeltaTime * 1000);
            float unityDelta = delta / 1000f;
            float unityFixedDelta = fixedDelta / 1000f;
            m_context.ReplaceTime(delta, fixedDelta, unityDelta, unityFixedDelta);
        }
    }
}