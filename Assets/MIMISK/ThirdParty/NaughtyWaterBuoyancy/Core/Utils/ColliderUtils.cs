using UnityEngine;

namespace NaughtyWaterBuoyancy
{
    public static class ColliderUtils
    {
        public static bool IsPointInsideCollider(Vector3 point, Collider collider, ref Bounds colliderBounds)
        {
            Vector3 closestPoint = collider.ClosestPoint(point);
            return (closestPoint - point).sqrMagnitude <= 0.000001f;
        }
    }
}
