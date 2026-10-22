using UnityEngine;

[System.Serializable]
public class ObstacleData
{
    public GameObject obstacle;
    public float obstacleHeight;
    public float obstacleLength;
    public float distanceToObstacle;
    public Vector3 obstacleNormal;

    public ObstacleData(
        Collider obstacleCollider,
        Vector3 forwardDirection,
        Vector3 playerPosition,
        Vector3 hitPoint,
        Vector3 hitNormal)
    {
        if (obstacleCollider == null)
            return;

        obstacle = obstacleCollider.gameObject;
        Bounds bounds = obstacleCollider.bounds;
        obstacleHeight = bounds.size.y;
        obstacleNormal = hitNormal;

        Vector3 forward = Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        distanceToObstacle = Mathf.Max(0f, Vector3.Dot(hitPoint - playerPosition, forward));

        // Project the world-space bounds onto the runner's forward axis so rotated
        // obstacles are measured by the depth the player actually has to cross.
        obstacleLength = Mathf.Abs(forward.x) * bounds.size.x +
                         Mathf.Abs(forward.z) * bounds.size.z;
    }
}
