using System;
using Entitas;
using Entitas.Unity;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityToEntitas.Behaviours;
using BoxCollider = Unity.Physics.BoxCollider;
using Material = Unity.Physics.Material;
using Math = System.Math;
using Object = UnityEngine.Object;
using SphereCollider = Unity.Physics.SphereCollider;

namespace UnityToEntitas.Systems
{
    public class ConversionSystem : IInitializeSystem
    {
        private readonly GameContext m_context;

        public ConversionSystem(GameContext context)
        {
            m_context = context;
        }
        
        public void Initialize()
        {
            foreach (ConvertToEntitas conversionComponent in ConversionList.PickConversionQueue())
            {
                Convert(conversionComponent);
            }
        }

        private void Convert(ConvertToEntitas conversionComponent)
        {
            //TODO once this will be the great flexible piece of code. But not now
            GameEntity entity = m_context.CreateEntity();
            GameObject gameObject = conversionComponent.gameObject;
            ConvertTransform(entity, gameObject);
            ConvertPhysicShape(entity, gameObject);
            ConvertPhysicBody(entity, gameObject);
            ConvertBehaviors(entity, gameObject);
            gameObject.Link(entity);
            EntityTransform entityTransform = gameObject.AddComponent<EntityTransform>();
            entity.AddPositionListener(entityTransform);
            entity.AddRotationListener(entityTransform);
        }

        private void ConvertBehaviors(GameEntity entity, GameObject gameObject)
        { 
            IConvertToEntitas[] converters = gameObject.GetComponents<IConvertToEntitas>();
            foreach (IConvertToEntitas converter in converters)
            {
                converter.Convert(entity);
                Object.DestroyImmediate((Object) converter);
            }
        }

        private void ConvertTransform(GameEntity entity, GameObject gameObject)
        {
            entity.AddPosition(gameObject.transform.position);
            entity.AddRotation(gameObject.transform.rotation);
        }

        private void ConvertPhysicShape(GameEntity entity, GameObject gameObject)
        {
            PhysicsShapeAuthoring shape = gameObject.GetComponent<PhysicsShapeAuthoring>();
            if (shape == null)
            {
                return;
            }
            
            quaternion orientation;
            Transform transform = shape.transform;
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            float4x4 shapeToWorld = float4x4.TRS(transform.position, transform.rotation, 1f);;
            
            //TODO collider lifetime
            switch (shape.ShapeType)
            {
                case ShapeType.Box:
                    entity.AddPhysicsCollider(BoxCollider.Create(
                        shape.GetBoxProperties(), 
                        shape.GetCollisionFilter(),
                        shape.GetMaterial()
                    ));
                    break;
                case ShapeType.Capsule:
                    break;
                case ShapeType.Sphere:
                    entity.AddPhysicsCollider(SphereCollider.Create(
                        shape.GetSphereProperties(out _),
                        shape.GetCollisionFilter(),
                        shape.GetMaterial()
                        ));
                    break;
                case ShapeType.Cylinder:
                    break;
                case ShapeType.Plane:
                    float3x4 v;
                    shape.GetPlaneProperties(out float3 center, out float2 size, out orientation);
                    BakeToBodySpace(
                        center, size, orientation, localToWorld, shapeToWorld,
                        out v.c0, out v.c1, out v.c2, out v.c3
                    );
                    entity.AddPhysicsCollider(PolygonCollider.CreateQuad(
                        v.c0, v.c1, v.c2, v.c3,
                        shape.GetCollisionFilter(),
                        shape.GetMaterial()
                    ));
                    break;
                case ShapeType.ConvexHull:
                    break;
                case ShapeType.Mesh:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!shape.CustomTags.Equals(CustomPhysicsMaterialTags.Nothing))
            {
                entity.AddPhysicsCustomTags(shape.CustomTags.Value);
            }
            
            Object.DestroyImmediate(shape);
        }
        
        private void ConvertPhysicBody(GameEntity entity, GameObject gameObject)
        {
            PhysicsBodyAuthoring body = gameObject.GetComponent<PhysicsBodyAuthoring>();
            if (body == null)
            {
                return;
            }
            
            //XXX conflict with shape.CustomTags
            CustomPhysicsBodyTags customTags = body.CustomTags;
            if (!customTags.Equals(CustomPhysicsBodyTags.Nothing))
            {
                entity.AddPhysicsCustomTags(customTags.Value);
            }
            
            if (body.MotionType == BodyMotionType.Static)
                return;
            
            var massProperties = MassProperties.UnitSphere;
            if (entity.hasPhysicsCollider)
            {
                // Build mass component
                massProperties = entity.physicsCollider.MassProperties;
            }
            if (body.OverrideDefaultMassDistribution)
            {
                massProperties.MassDistribution = body.CustomMassDistribution;
                // Increase the angular expansion factor to account for the shift in center of mass
                massProperties.AngularExpansionFactor += math.length(massProperties.MassDistribution.Transform.pos - body.CustomMassDistribution.Transform.pos);
            }
            
            entity.AddPhysicsMass(body.MotionType == BodyMotionType.Dynamic ?
                PhysicsMass.CreateDynamic(massProperties, body.Mass) :
                PhysicsMass.CreateKinematic(massProperties));
            
            entity.AddPhysicsVelocity(body.InitialLinearVelocity, body.InitialAngularVelocity);

            if (body.MotionType == BodyMotionType.Dynamic)
            {
                // TODO make these optional in editor?
                entity.AddPhysicsDamping(body.LinearDamping, body.AngularDamping);
                if (Math.Abs(body.GravityFactor - 1) > Mathf.Epsilon)
                {
                    entity.AddPhysicsGravityFactor(body.GravityFactor);
                }
            }
            else if (body.MotionType == BodyMotionType.Kinematic)
            {
                entity.AddPhysicsGravityFactor(0);
            }
            
            Object.DestroyImmediate(body);
        }
        
        internal static void BakeToBodySpace(
            float3 center, float2 size, quaternion orientation, float4x4 localToWorld, float4x4 shapeToWorld,
            out float3 vertex0, out float3 vertex1, out float3 vertex2, out float3 vertex3
        )
        {
            using (var geometry = new NativeArray<float3x4>(1, Allocator.TempJob))
            {
                var job = new BakePlaneJob
                {
                    Vertices = geometry,
                    center = center,
                    size = size,
                    orientation = orientation,
                    localToWorld = localToWorld,
                    shapeToWorld = shapeToWorld
                };
                job.Run();
                vertex0 = geometry[0].c0;
                vertex1 = geometry[0].c1;
                vertex2 = geometry[0].c2;
                vertex3 = geometry[0].c3;
            }
        }
        
        [BurstCompile(CompileSynchronously = true)]
        struct BakePlaneJob : IJob
        {
            public NativeArray<float3x4> Vertices;
            // TODO: make members PascalCase after merging static query fixes
            public float3 center;
            public float2 size;
            public quaternion orientation;
            public float4x4 localToWorld;
            public float4x4 shapeToWorld;

            public void Execute()
            {
                var v = Vertices[0];
                GetPlanePoints(center, size, orientation, out v.c0, out v.c1, out v.c2, out v.c3);
                var localToShape = math.mul(math.inverse(shapeToWorld), localToWorld);
                v.c0 = math.mul(localToShape, new float4(v.c0, 1f)).xyz;
                v.c1 = math.mul(localToShape, new float4(v.c1, 1f)).xyz;
                v.c2 = math.mul(localToShape, new float4(v.c2, 1f)).xyz;
                v.c3 = math.mul(localToShape, new float4(v.c3, 1f)).xyz;
                Vertices[0] = v;
            }
        }
        
        internal static void GetPlanePoints(
            float3 center, float2 size, quaternion orientation,
            out float3 vertex0, out float3 vertex1, out float3 vertex2, out float3 vertex3
        )
        {
            var sizeYUp = math.float3(size.x, 0, size.y);

            vertex0 = center + math.mul(orientation, sizeYUp * math.float3(-0.5f, 0,  0.5f));
            vertex1 = center + math.mul(orientation, sizeYUp * math.float3( 0.5f, 0,  0.5f));
            vertex2 = center + math.mul(orientation, sizeYUp * math.float3( 0.5f, 0, -0.5f));
            vertex3 = center + math.mul(orientation, sizeYUp * math.float3(-0.5f, 0, -0.5f));
        }
    }

    internal static class ConversionSystemHelper
    {
        internal static CollisionFilter GetCollisionFilter(this PhysicsShapeAuthoring shape)
        {
            return new CollisionFilter
            {
                BelongsTo = shape.BelongsTo.Value,
                CollidesWith = shape.CollidesWith.Value
            };
        }
        internal static Material GetMaterial(this PhysicsShapeAuthoring shape)
        {
            // TODO: TBD how we will author editor content for other shape flags
            var flags = new Material.MaterialFlags();
            if (shape.IsTrigger)
            {
                flags = Material.MaterialFlags.IsTrigger;
            }
            else if (shape.RaisesCollisionEvents)
            {
                flags = Material.MaterialFlags.EnableCollisionEvents;
            }

            return new Material
            {
                Friction = shape.Friction.Value,
                FrictionCombinePolicy = shape.Friction.CombineMode,
                Restitution = shape.Restitution.Value,
                RestitutionCombinePolicy = shape.Restitution.CombineMode,
                Flags = flags,
                CustomTags = shape.CustomTags.Value
            };
        }
    }
}