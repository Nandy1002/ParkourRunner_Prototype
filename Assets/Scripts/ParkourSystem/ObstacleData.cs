using UnityEngine;

public class ObstacleData
{
    public GameObject obstacle;
    public float obstacleHeight;
    public float obstacleLength;

    public ObstacleData(GameObject obstacle)
    {
        this.obstacle = obstacle;

        Collider obstacleCollider = obstacle.GetComponent<Collider>();

        if (obstacleCollider != null)
        {
            Bounds bounds = obstacleCollider.bounds;

            obstacleHeight = bounds.size.y;
            obstacleLength = bounds.size.z;
        }
        else
        {
            obstacleHeight = 0f;
            obstacleLength = 0f;

            Debug.LogWarning(
                $"Obstacle '{obstacle.name}' has no Collider."
            );
        }
    }
}