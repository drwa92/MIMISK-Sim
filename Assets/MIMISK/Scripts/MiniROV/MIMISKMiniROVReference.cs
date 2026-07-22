using System;
using UnityEngine;

[Serializable]
public struct MIMISKMiniROVReference
{
    public bool valid;

    public float missionTimeS;

    public Vector3 positionWorld;
    public Vector3 velocityWorld;

    public float depthMeters;
    public float yawDeg;

    public bool hasVelocity;
    public bool hasYaw;

    public string source;
}
