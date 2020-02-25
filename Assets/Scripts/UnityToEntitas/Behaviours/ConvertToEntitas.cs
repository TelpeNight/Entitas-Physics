using UnityEngine;
using UnityToEntitas.Systems;

namespace UnityToEntitas.Behaviours
{
    [DisallowMultipleComponent]
    [AddComponentMenu("DotsToEntitas/Convert To Entitas")]
    public class ConvertToEntitas : Unity.Entities.ConvertToEntity
    {
        void Awake()
        {
            ConversionList.Add(this);
        }
    }
}