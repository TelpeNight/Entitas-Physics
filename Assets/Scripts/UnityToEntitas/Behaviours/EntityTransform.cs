using UnityEngine;

namespace UnityToEntitas.Behaviours
{
    public class EntityTransform : MonoBehaviour, IPositionListener, IRotationListener
    {
        public void OnPosition(GameEntity entity, Vector3 value)
        {
            transform.position = value;
        }

        public void OnRotation(GameEntity entity, Quaternion value)
        {
            transform.rotation = value;
        }
    }
}