using System;
using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Physics
{
    [Physics][Unique]
    public class WorldComponent : IComponent
    {
        public PhysicsWorld Value;
    }

    [Physics][Unique]
    public class SimulationComponent : IComponent
    {
        public Simulation Simulation;
        public JobHandle FinishHandle;
    }

    public struct CollisionData
    {
        public BodyIndexPair BodyIndexPair;
        public float3 AverageContactPointPosition;
    }

    [Physics][Unique]
    public class CollisionListComponent : IComponent
    {
        private NativeList<BodyIndexPair> m_triggers = new NativeList<BodyIndexPair>(Allocator.Persistent);
        private NativeList<CollisionData> m_collisions = new NativeList<CollisionData>(Allocator.Persistent);

        public ref NativeList<BodyIndexPair> Triggers => ref m_triggers;
        public ref NativeList<CollisionData> Collisions => ref m_collisions;
        public JobHandle FinishHandle;
    }

    [Physics][Unique]
    public class GenerationComponent : IComponent
    {
        public List<GameEntity> Value;

        public void Retain()
        {
            foreach (GameEntity entity in Value)
            {
                entity.Retain(this);
            }
        }

        public void Release()
        {
            foreach (GameEntity entity in Value)
            {
                entity.Release(this);
            }
        }
    }

    [Physics][Unique]
    public class StepComponent : IComponent
    {
        public SimulationType SimulationType;
        public Vector3 Gravity;
        public int SolverIterationCount;

        // DOTS doesn't yet expose the number of worker threads, which is needed for tuning the simulation.
        // For optimal  performance set this to the number of physical CPU cores on your target device.
        public int ThreadCountHint;

        public static readonly StepComponent Default = new StepComponent
        {
            SimulationType = SimulationType.UnityPhysics,
            Gravity = -9.81f * Vector3.up,
            SolverIterationCount = 4,
            ThreadCountHint = Environment.ProcessorCount
        };
    }
}