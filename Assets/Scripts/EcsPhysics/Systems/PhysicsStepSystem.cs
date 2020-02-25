using Unity.Jobs;
using Unity.Physics;

namespace EcsPhysics.Systems
{
    class PhysicsStepSystem : Entitas.IExecuteSystem
    {
        private readonly PhysicsContext m_physics;
        private readonly GameContext m_game;

        public PhysicsStepSystem(PhysicsContext physics, GameContext game)
        {
            m_physics = physics;
            m_game = game;
        }

        public void Execute()
        {
            Physics.StepComponent stepComponentComponent = Physics.StepComponent.Default;
            if (m_physics.hasPhysicsStep)
            {
                stepComponentComponent = m_physics.physicsStep;
            }

            float timeStep = m_game.time.UnityFixedDeltaTime;

            // Schedule the simulation jobs
            m_physics.physicsSimulation.Simulation.ScheduleStepJobs(new SimulationStepInput()
            {
                World = m_physics.physicsWorld.Value,
                TimeStep = timeStep,
                ThreadCountHint = stepComponentComponent.ThreadCountHint,
                Gravity = stepComponentComponent.Gravity,
                SynchronizeCollisionWorld = false,
                NumSolverIterations = stepComponentComponent.SolverIterationCount,
                Callbacks = new SimulationCallbacks()
            }, m_physics.physicsSimulation.FinishHandle);

            // Clear the callbacks. User must enqueue them again before the next step.
            //m_callbacks.Clear();

            // Return the final simulation handle
            // (Not FinalJobHandle since other systems shouldn't need to depend on the dispose jobs) 
            m_physics.physicsSimulation.FinishHandle = JobHandle.CombineDependencies(
                m_physics.physicsSimulation.Simulation.FinalSimulationJobHandle,
                m_physics.physicsSimulation.FinishHandle);
        }
    }
}