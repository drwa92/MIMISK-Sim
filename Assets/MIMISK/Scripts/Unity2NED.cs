using UnityEngine;


public static class Unity2NED 
{
 

    public static Vector3 VectorUnityToNED(Vector3 unityVec)
    {
        return new Vector3(
            unityVec.z,   // North
            unityVec.x,   // East
           -unityVec.y    // Down
        );
    }
    
    public static Quaternion QuaternionUnityToNED(Quaternion unityQuat)
    {
        // Rotation corrective Unity -> NED : permute axes
        Vector3 euler = unityQuat.eulerAngles;
        Vector3 eulerNED = VectorUnityToNED(euler);
        return  Quaternion.Euler(eulerNED);
    }
}