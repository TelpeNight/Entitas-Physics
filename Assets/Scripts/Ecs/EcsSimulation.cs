using System;
using Ecs.Systems;
using EcsPhysics.Systems;
using UnityEngine;
using UnityToEntitas.Systems;

namespace Ecs
{
    public class EcsSimulation : MonoBehaviour
    {
        private Entitas.Systems m_systems; 
        
        private void Start()
        {
            GameContext game = Contexts.sharedInstance.game;
            PhysicsSystems physicSystems = new PhysicsSystems(Contexts.sharedInstance.physics, game);
            
            m_systems = new Feature()
                .Add(new ConversionSystem(game))
                .Add(new Feature("BeginFrame")
                    .Add(physicSystems.BeginFrameSystems)
                    .Add(new TimeSystem(game)))
                .Add(new ExplosionSystem(game))
                .Add(new Feature("EndFrame")
                    .Add(physicSystems.EndFrameSystems)
                    .Add(new GameEventSystems(Contexts.sharedInstance)));
            
            m_systems.Initialize();
        }

        private void OnDestroy()
        {
            m_systems.TearDown();
            //Contexts.sharedInstance.Reset();
        }
        
        private void Update()
        {
            m_systems.Execute();
            m_systems.Cleanup();
        }
    }
}