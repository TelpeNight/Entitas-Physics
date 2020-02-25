using System;
using System.ComponentModel;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    /// <summary>
    /// This struct captures the information needed for ray casting.
    /// It is technically not a Ray as it includes a length.
    /// This is to avoid performance issues with infinite length Rays.
    /// </summary>
    public struct Ray
    {
        /// <summary>
        /// The Origin point of the Ray in World Space.
        /// </summary>
        /// <value> Point vector coordinate. </value>
        public float3 Origin;

        /// <summary>
        /// This represents the line from the Ray's Origin to a second point on the Ray. The second point will be the Ray End if nothing is hit.
        /// </summary>
        /// <value> Line vector. </value>
        public float3 Displacement
        {
            get => m_Displacement;
            set
            {
                m_Displacement = value;
                ReciprocalDisplacement = math.select(math.rcp(m_Displacement), math.sqrt(float.MaxValue), m_Displacement == float3.zero);
            }
        }
        float3 m_Displacement;

        // Performance optimization used in the BoundingVolumeHierarchy casting functions
        internal float3 ReciprocalDisplacement { get; private set; }
    }

    /// <summary>
    /// The input to RayCastQueries consists of the Start and End positions of a line segment as well as a CollisionFilter to cull potential hits.
    /// </summary>
    public struct RaycastInput
    {
        /// <summary>
        /// The Start position of a Ray.
        /// </summary>
        public float3 Start
        {
            get => Ray.Origin;
            set
            {
                float3 end = Ray.Origin + Ray.Displacement;
                Ray.Origin = value;
                Ray.Displacement = end - value;
            }
        }
        /// <summary>
        /// The End position of a Ray.
        /// </summary>
        public float3 End
        {
            get => Ray.Origin + Ray.Displacement;
            set => Ray.Displacement = value - Ray.Origin;
        }
        /// <summary>
        /// The CollisionFilter is used to determine what objects the Ray is and isn't going to hit.
        /// </summary>
        public CollisionFilter Filter;

        internal Ray Ray;
    }

    // A hit from a ray cast query
    /// <summary>
    /// A struct representing the hit from a RaycastQuery.
    /// </summary>
    public struct RaycastHit : IQueryResult
    {
        /// <summary>
        /// Fraction of the distance along the Ray where the hit occurred.
        /// </summary>
        /// <value> Returns a value between 0 and 1. </value>
        public float Fraction { get; set; }

        /// <summary>
        /// The point in World Space where the hit occurred.
        /// </summary>
        /// <value> The value is 0,0,0 if no hit occurs. </value>
        public float3 Position;

        /// <summary>
        /// The normal of the point where the hit occurred.
        /// </summary>
        public float3 SurfaceNormal;

        /// <summary>
        /// The entity index of the RigidBody that was hit.
        /// </summary>
        public int RigidBodyIndex;

        /// <summary>
        /// The ColliderKey is used to identify the hit child in any CompositeCollider (e.g. MeshCollider or CompoundCollider).
        /// </summary>
        /// <value> A reference to which triangle or quad in the collider was hit. </value>
        public ColliderKey ColliderKey;

        /// <summary>
        ///
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="numSubKeyBits"></param>
        /// <param name="subKey"></param>
        public void Transform(MTransform transform, uint numSubKeyBits, uint subKey)
        {
            Position = Mul(transform, Position);
            SurfaceNormal = math.mul(transform.Rotation, SurfaceNormal);
            ColliderKey.PushSubKey(numSubKeyBits, subKey);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="rigidBodyIndex"></param>
        public void Transform(MTransform transform, int rigidBodyIndex)
        {
            Position = Mul(transform, Position);
            SurfaceNormal = math.mul(transform.Rotation, SurfaceNormal);
            RigidBodyIndex = rigidBodyIndex;
        }
    }

    // Raycast query implementations
    static class RaycastQueries
    {
        #region Ray vs primitives

        // Note that the primitives are considered solid.
        // Any ray originating from within the primitive will return a hit,
        // however the hit fraction will be zero, and the hit normal
        // will be the negation of the ray displacement vector.

        public static bool RaySphere(
            float3 rayOrigin, float3 rayDisplacement,
            float3 sphereCenter, float sphereRadius,
            ref float fraction, out float3 normal)
        {
            normal = float3.zero;

            // TODO.ma lots of float inaccuracy problems with this
            float3 diff = rayOrigin - sphereCenter;
            float a = math.dot(rayDisplacement, rayDisplacement);
            float b = 2.0f * math.dot(rayDisplacement, diff);
            float c = math.dot(diff, diff) - sphereRadius * sphereRadius;
            float discriminant = b * b - 4.0f * a * c;

            if (c < 0)
            {
                // Inside hit.
                fraction = 0;
                normal = math.normalize(-rayDisplacement);
                return true;
            }

            if (discriminant < 0)
            {
                return false;
            }

            float sqrtDiscriminant = math.sqrt(discriminant);
            float invDenom = 0.5f / a;

            float t0 = (sqrtDiscriminant - b) * invDenom;
            float t1 = (-sqrtDiscriminant - b) * invDenom;
            float tMin = math.min(t0, t1);

            if (tMin >= 0 && tMin < fraction)
            {
                fraction = tMin;
                normal = (rayOrigin + rayDisplacement * fraction - sphereCenter) / sphereRadius;

                return true;
            }

            return false;
        }

        public static bool RayCapsule(
            float3 rayOrigin, float3 rayDisplacement,
            float3 vertex0, float3 vertex1, float radius,
            ref float fraction, out float3 normal)
        {
            float axisLength = NormalizeWithLength(vertex1 - vertex0, out float3 axis);

            // Ray vs infinite cylinder
            {
                float directionDotAxis = math.dot(rayDisplacement, axis);
                float originDotAxis = math.dot(rayOrigin - vertex0, axis);
                float3 rayDisplacement2D = rayDisplacement - axis * directionDotAxis;
                float3 rayOrigin2D = rayOrigin - axis * originDotAxis;
                float cylinderFraction = fraction;

                if (RaySphere(rayOrigin2D, rayDisplacement2D, vertex0, radius, ref cylinderFraction, out normal))
                {
                    float t = originDotAxis + cylinderFraction * directionDotAxis; // distance of the hit from Vertex0 along axis
                    if (t >= 0.0f && t <= axisLength)
                    {
                        if (cylinderFraction == 0)
                        {
                            // Inside hit
                            normal = math.normalize(-rayDisplacement);
                        }

                        fraction = cylinderFraction;
                        return true;
                    }
                }
            }

            // Ray vs caps
            {
                bool hadHit = false;
                float3 capNormal;
                if (RaySphere(rayOrigin, rayDisplacement, vertex0, radius, ref fraction, out capNormal))
                {
                    hadHit = true;
                    normal = capNormal;
                }
                if (RaySphere(rayOrigin, rayDisplacement, vertex1, radius, ref fraction, out capNormal))
                {
                    hadHit = true;
                    normal = capNormal;
                }
                return hadHit;
            }
        }

        public static bool RayTriangle(
            float3 rayOrigin, float3 rayDisplacement,
            float3 a, float3 b, float3 c, // TODO: float3x3?
            ref float fraction, out float3 unnormalizedNormal)
        {
            float3 vAb = b - a;
            float3 vCa = a - c;

            float3 vN = math.cross(vAb, vCa);
            float3 vAp = rayOrigin - a;
            float3 end0 = vAp + rayDisplacement * fraction;

            float d = math.dot(vN, vAp);
            float e = math.dot(vN, end0);

            if (d * e >= 0)
            {
                unnormalizedNormal = float3.zero;
                return false;
            }

            float3 vBc = c - b;
            fraction *= d / (d - e);
            unnormalizedNormal = vN * math.sign(d);

            // edge normals
            float3 c0 = math.cross(vAb, rayDisplacement);
            float3 c1 = math.cross(vBc, rayDisplacement);
            float3 c2 = math.cross(vCa, rayDisplacement);

            float3 dots;
            {
                float3 o2 = rayOrigin + rayOrigin;
                float3 r0 = o2 - (a + b);
                float3 r1 = o2 - (b + c);
                float3 r2 = o2 - (c + a);

                dots.x = math.dot(r0, c0);
                dots.y = math.dot(r1, c1);
                dots.z = math.dot(r2, c2);
            }

            // hit if all dots have the same sign
            return math.all(dots <= 0) || math.all(dots >= 0);
        }

        public static bool RayQuad(
            float3 rayOrigin, float3 rayDisplacement,
            float3 a, float3 b, float3 c, float3 d, // TODO: float3x4?
            ref float fraction, out float3 unnormalizedNormal)
        {
            float3 vAb = b - a;
            float3 vCa = a - c;

            float3 vN = math.cross(vAb, vCa);
            float3 vAp = rayOrigin - a;
            float3 end0 = vAp + rayDisplacement * fraction;

            float nDotAp = math.dot(vN, vAp);
            float e = math.dot(vN, end0);

            if (nDotAp * e >= 0)
            {
                unnormalizedNormal = float3.zero;
                return false;
            }

            float3 vBc = c - b;
            float3 vDa = a - d;
            float3 vCd = d - c;
            fraction *= nDotAp / (nDotAp - e);
            unnormalizedNormal = vN * math.sign(nDotAp);

            // edge normals
            float3 c0 = math.cross(vAb, rayDisplacement);
            float3 c1 = math.cross(vBc, rayDisplacement);
            float3 c2 = math.cross(vCd, rayDisplacement);
            float3 c3 = math.cross(vDa, rayDisplacement);

            float4 dots;
            {
                float3 o2 = rayOrigin + rayOrigin;
                float3 r0 = o2 - (a + b);
                float3 r1 = o2 - (b + c);
                float3 r2 = o2 - (c + d);
                float3 r3 = o2 - (d + a);

                dots.x = math.dot(r0, c0);
                dots.y = math.dot(r1, c1);
                dots.z = math.dot(r2, c2);
                dots.w = math.dot(r3, c3);
            }

            bool4 notOutSide = dots < 0;
            // hit if all dots have the same sign
            return math.all(dots <= 0) || math.all(dots >= 0);
        }

        public static bool RayConvex(
            float3 rayOrigin, float3 rayDisplacement,
            ref ConvexHull hull, ref float fraction, out float3 normal)
        {
            // TODO: Call RaySphere/Capsule/Triangle() if num vertices <= 3 ?

            float convexRadius = hull.ConvexRadius;
            float fracEnter = -1.0f;
            float fracExit = 2.0f;
            float3 start = rayOrigin;
            float3 end = start + rayDisplacement * fraction;
            normal = new float3(1, 0, 0);
            for (int i = 0; i < hull.NumFaces; i++) // TODO.ma vectorize
            {
                // Calculate the plane's hit fraction
                Plane plane = hull.Planes[i];
                float startDistance = math.dot(start, plane.Normal) + plane.Distance - convexRadius;
                float endDistance = math.dot(end, plane.Normal) + plane.Distance - convexRadius;
                float newFraction = startDistance / (startDistance - endDistance);
                bool startInside = (startDistance < 0);
                bool endInside = (endDistance < 0);

                // If the ray is entirely outside of any plane, then it misses
                if (!(startInside || endInside))
                {
                    return false;
                }

                // If the ray crosses the plane, update the enter or exit fraction
                bool enter = !startInside && newFraction > fracEnter;
                bool exit = !endInside && newFraction < fracExit;
                fracEnter = math.select(fracEnter, newFraction, enter);
                normal = math.select(normal, plane.Normal, enter);
                fracExit = math.select(fracExit, newFraction, exit);
            }

            if (fracEnter < 0)
            {
                // Inside hit.
                fraction = 0;
                normal = math.normalize(-rayDisplacement);
                return true;
            }

            if (fracEnter < fracExit)
            {
                fraction *= fracEnter;
                return true;
            }

            // miss
            return false;
        }

        #endregion

        #region Ray vs colliders

        public static unsafe bool RayCollider<T>(RaycastInput input, Collider* collider, ref T collector) where T : struct, ICollector<RaycastHit>
        {
            if (!CollisionFilter.IsCollisionEnabled(input.Filter, collider->Filter))
            {
                return false;
            }

            float fraction = collector.MaxFraction;
            float3 normal;
            bool hadHit;
            switch (collider->Type)
            {
                case ColliderType.Sphere:
                    var sphere = (SphereCollider*)collider;
                    hadHit = RaySphere(input.Ray.Origin, input.Ray.Displacement, sphere->Center, sphere->Radius, ref fraction, out normal);
                    break;
                case ColliderType.Capsule:
                    var capsule = (CapsuleCollider*)collider;
                    hadHit = RayCapsule(input.Ray.Origin, input.Ray.Displacement, capsule->Vertex0, capsule->Vertex1, capsule->Radius, ref fraction, out normal);
                    break;
                case ColliderType.Triangle:
                    {
                        var triangle = (PolygonCollider*)collider;
                        hadHit = RayTriangle(input.Ray.Origin, input.Ray.Displacement, triangle->Vertices[0], triangle->Vertices[1], triangle->Vertices[2], ref fraction, out float3 unnormalizedNormal);
                        normal = hadHit ? math.normalize(unnormalizedNormal) : float3.zero;
                        break;
                    }
                case ColliderType.Quad:
                    {
                        var quad = (PolygonCollider*)collider;
                        hadHit = RayQuad(input.Ray.Origin, input.Ray.Displacement, quad->Vertices[0], quad->Vertices[1], quad->Vertices[2], quad->Vertices[3], ref fraction, out float3 unnormalizedNormal);
                        normal = hadHit ? math.normalize(unnormalizedNormal) : float3.zero;
                        break;
                    }
                case ColliderType.Box:
                case ColliderType.Cylinder:
                case ColliderType.Convex:
                    hadHit = RayConvex(input.Ray.Origin, input.Ray.Displacement, ref ((ConvexCollider*)collider)->ConvexHull, ref fraction, out normal);
                    break;
                case ColliderType.Mesh:
                    return RayMesh(input, (MeshCollider*)collider, ref collector);
                case ColliderType.Compound:
                    return RayCompound(input, (CompoundCollider*)collider, ref collector);
                case ColliderType.Terrain:
                    return RayTerrain(input, (TerrainCollider*)collider, ref collector);
                default:
                    throw new NotImplementedException();
            }

            if (hadHit)
            {
                return collector.AddHit(new RaycastHit
                {
                    Fraction = fraction,
                    Position = input.Ray.Origin + input.Ray.Displacement * fraction,
                    SurfaceNormal = normal,
                    RigidBodyIndex = -1,
                    ColliderKey = ColliderKey.Empty
                });
            }
            return false;
        }

        // Mesh
        private unsafe struct RayMeshLeafProcessor : BoundingVolumeHierarchy.IRaycastLeafProcessor
        {
            private readonly Mesh* m_Mesh;
            private readonly uint m_NumColliderKeyBits;

            public RayMeshLeafProcessor(MeshCollider* meshCollider)
            {
                m_Mesh = &meshCollider->Mesh;
                m_NumColliderKeyBits = meshCollider->NumColliderKeyBits;
            }

            public bool RayLeaf<T>(RaycastInput input, int primitiveKey, ref T collector) where T : struct, ICollector<RaycastHit>
            {
                m_Mesh->GetPrimitive(primitiveKey, out float3x4 vertices, out Mesh.PrimitiveFlags flags, out CollisionFilter filter);

                if (!CollisionFilter.IsCollisionEnabled(input.Filter, filter)) // TODO: could do this check within GetPrimitive()
                {
                    return false;
                }

                int numPolygons = Mesh.GetNumPolygonsInPrimitive(flags);
                bool isQuad = Mesh.IsPrimitveFlagSet(flags, Mesh.PrimitiveFlags.IsQuad);

                bool acceptHit = false;
                float3 unnormalizedNormal;

                for (int polygonIndex = 0; polygonIndex < numPolygons; polygonIndex++)
                {
                    float fraction = collector.MaxFraction;
                    bool hadHit;
                    if (isQuad)
                    {
                        hadHit = RayQuad(input.Ray.Origin, input.Ray.Displacement, vertices[0], vertices[1], vertices[2], vertices[3], ref fraction, out unnormalizedNormal);
                    }
                    else
                    {
                        hadHit = RayTriangle(input.Ray.Origin, input.Ray.Displacement, vertices[0], vertices[polygonIndex + 1], vertices[polygonIndex + 2], ref fraction, out unnormalizedNormal);
                    }

                    if (hadHit && fraction < collector.MaxFraction)
                    {
                        acceptHit |= collector.AddHit(new RaycastHit
                        {
                            Fraction = fraction,
                            Position = input.Ray.Origin + input.Ray.Displacement * fraction,
                            SurfaceNormal = math.normalize(unnormalizedNormal),
                            RigidBodyIndex = -1,
                            ColliderKey = new ColliderKey(m_NumColliderKeyBits, (uint)(primitiveKey << 1 | polygonIndex))
                        });
                    }
                }

                return acceptHit;
            }
        }

        private static unsafe bool RayMesh<T>(RaycastInput input, MeshCollider* meshCollider, ref T collector) where T : struct, ICollector<RaycastHit>
        {
            var leafProcessor = new RayMeshLeafProcessor(meshCollider);
            return meshCollider->Mesh.BoundingVolumeHierarchy.Raycast(input, ref leafProcessor, ref collector);
        }

        // Compound

        private unsafe struct RayCompoundLeafProcessor : BoundingVolumeHierarchy.IRaycastLeafProcessor
        {
            private readonly CompoundCollider* m_CompoundCollider;

            public RayCompoundLeafProcessor(CompoundCollider* compoundCollider)
            {
                m_CompoundCollider = compoundCollider;
            }

            public bool RayLeaf<T>(RaycastInput input, int leafData, ref T collector) where T : struct, ICollector<RaycastHit>
            {
                ref CompoundCollider.Child child = ref m_CompoundCollider->Children[leafData];

                if (!CollisionFilter.IsCollisionEnabled(input.Filter, child.Collider->Filter))
                {
                    return false;
                }

                MTransform compoundFromChild = new MTransform(child.CompoundFromChild);

                // Transform the ray into child space
                RaycastInput inputLs = input;
                {
                    MTransform childFromCompound = Inverse(compoundFromChild);
                    inputLs.Ray.Origin = Mul(childFromCompound, input.Ray.Origin);
                    inputLs.Ray.Displacement = math.mul(childFromCompound.Rotation, input.Ray.Displacement);
                }

                int numHits = collector.NumHits;
                float fraction = collector.MaxFraction;

                if (child.Collider->CastRay(inputLs, ref collector))
                {
                    // Transform results back to compound space
                    collector.TransformNewHits(numHits, fraction, compoundFromChild, m_CompoundCollider->NumColliderKeyBits, (uint)leafData);
                    return true;
                }
                return false;
            }
        }

        private static unsafe bool RayCompound<T>(RaycastInput input, CompoundCollider* compoundCollider, ref T collector) where T : struct, ICollector<RaycastHit>
        {
            if (!CollisionFilter.IsCollisionEnabled(input.Filter, compoundCollider->Filter))
            {
                return false;
            }

            var leafProcessor = new RayCompoundLeafProcessor(compoundCollider);
            return compoundCollider->BoundingVolumeHierarchy.Raycast(input, ref leafProcessor, ref collector);
        }

        private static unsafe bool RayTerrain<T>(RaycastInput input, TerrainCollider* terrainCollider, ref T collector) where T : struct, ICollector<RaycastHit>
        {
            ref var terrain = ref terrainCollider->Terrain;

            bool hadHit = false;

            // Transform the ray into heightfield space
            var ray = new Ray
            {
                Origin = input.Ray.Origin * terrain.InverseScale,
                Displacement = input.Ray.Displacement * terrain.InverseScale
            };
            Terrain.QuadTreeWalker walker;
            {
                float3 maxDisplacement = ray.Displacement * collector.MaxFraction;
                var rayAabb = new Aabb
                {
                    Min = ray.Origin + math.min(maxDisplacement, float3.zero),
                    Max = ray.Origin + math.max(maxDisplacement, float3.zero),
                };
                walker = new Terrain.QuadTreeWalker(&terrainCollider->Terrain, rayAabb);
            }

            // Traverse the tree
            while (walker.Pop())
            {
                bool4 hitMask = walker.Bounds.Raycast(ray, collector.MaxFraction, out float4 hitFractions);
                hitMask &= (walker.Bounds.Ly <= walker.Bounds.Hy); // Mask off empty children
                if (walker.IsLeaf)
                {
                    // Leaf node, raycast against hit child quads
                    int4 hitIndex;
                    int hitCount = math.compress((int*)(&hitIndex), 0, new int4(0, 1, 2, 3), hitMask);
                    for (int iHit = 0; iHit < hitCount; iHit++)
                    {
                        // Get the quad vertices
                        walker.GetQuad(hitIndex[iHit], out int2 quadIndex, out float3 a, out float3 b, out float3 c, out float3 d);

                        // Test each triangle in the quad
                        for (int iTriangle = 0; iTriangle < 2; iTriangle++)
                        {
                            // Cast
                            float fraction = collector.MaxFraction;
                            bool triangleHit = RayTriangle(input.Ray.Origin, input.Ray.Displacement, a, b, c, ref fraction, out float3 unnormalizedNormal);
                            if (triangleHit && fraction < collector.MaxFraction)
                            {
                                hadHit |= collector.AddHit(new RaycastHit
                                {
                                    Fraction = fraction,
                                    Position = input.Ray.Origin + input.Ray.Displacement * fraction,
                                    SurfaceNormal = math.normalize(unnormalizedNormal),
                                    RigidBodyIndex = -1,
                                    ColliderKey = terrain.GetColliderKey(quadIndex, iTriangle)
                                });

                                if (collector.EarlyOutOnFirstHit && hadHit)
                                {
                                    return true;
                                }
                            }

                            // Next triangle
                            a = c;
                            c = d;
                        }
                    }
                }
                else
                {
                    // Interior node, add hit child nodes to the stack
                    walker.Push(hitMask);
                }
            }

            return hadHit;
        }

        #endregion
    }
}
