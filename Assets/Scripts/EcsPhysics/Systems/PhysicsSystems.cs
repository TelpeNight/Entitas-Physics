namespace EcsPhysics.Systems
{
    public struct PhysicsSystems
    {
        public readonly Entitas.Systems BeginFrameSystems;
        public readonly Entitas.Systems EndFrameSystems;

        public PhysicsSystems(PhysicsContext physicsContext, GameContext gameContext)
        {
            BeginFrameSystems = new Feature("PhysicsBeginFrame")
                .Add(new PhysicsInitSystem(physicsContext))
                .Add(new ExportWorldSystem(physicsContext))
                .Add(new ExportCollisionsSystem(physicsContext))
                .Add(new CleanupGenerationSystem(physicsContext));
            EndFrameSystems = new Feature("PhysicsEndFrame")
                .Add(new Feature("CollisionCleanup")
                    .Add(new RemoveCollisionGameSystem(Contexts.sharedInstance))
                    .Add(new RemoveTriggerContactGameSystem(Contexts.sharedInstance)))
                .Add(new BuildWorldSystem(physicsContext, gameContext))
                .Add(new PhysicsStepSystem(physicsContext, gameContext))
                .Add(new CollectCollisionsSystem(physicsContext))
                .Add(new PhysicsShutdownSystem(physicsContext));
        }
    }
}