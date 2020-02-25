using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

// ReSharper disable CheckNamespace

[Game][Unique]
public class TimeComponent : IComponent
{
    public int DeltaTime;
    public int FixedDeltaTime;
    public float UnityDeltaTime;
    public float UnityFixedDeltaTime;
}

//TODO Entitas Drawer crashes with float3
[Game][Event(EventTarget.Self)]
public class PositionComponent : IComponent
{
    public Vector3 Value;
}

[Game][Event(EventTarget.Self)]
public class RotationComponent : IComponent
{
    public Quaternion Value;
}