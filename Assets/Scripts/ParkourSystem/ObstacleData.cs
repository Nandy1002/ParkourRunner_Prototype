using UnityEngine;

[System.Serializable]
public class ObstacleData
{
    public GameObject obstacle;
    public float obstacleHeight;
    public float obstacleLength;
    public float distanceToObstacle;
    public Vector3 obstacleNormal;
    public Vector3 heightHitPoint;

    public ObstacleData(
        Collider obstacleCollider,
        Vector3 forwardDirection,
        Vector3 playerPosition,
        Vector3 hitPoint,
        Vector3 hitNormal,
        float rayHeightOffset)
    {
        if (obstacleCollider == null)
            return;

        obstacle = obstacleCollider.gameObject;
        Bounds bounds = obstacleCollider.bounds;
        heightHitPoint = new Vector3(hitPoint.x, bounds.max.y, hitPoint.z);
        obstacleHeight = Mathf.Max(0f, bounds.max.y - hitPoint.y + rayHeightOffset);
        obstacleNormal = hitNormal;

        Vector3 forward = Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        distanceToObstacle = Mathf.Max(0f, Vector3.Dot(hitPoint - playerPosition, forward));

        // Measure the obstacle along the horizontal hit-normal axis. The surface
        // normal identifies the face that was hit, even when the ray approaches
        // from an angle. Fall back to the ray direction for near-horizontal surfaces.
        Vector3 lengthAxis = Vector3.ProjectOnPlane(hitNormal, Vector3.up);
        if (lengthAxis.sqrMagnitude < 0.0001f)
            lengthAxis = forward;
        else
            lengthAxis.Normalize();

        obstacleLength = Mathf.Abs(lengthAxis.x) * bounds.size.x +
                         Mathf.Abs(lengthAxis.z) * bounds.size.z;
    }
}
