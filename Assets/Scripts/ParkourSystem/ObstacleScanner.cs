using System;
using UnityEngine;

public class ObstacleScanner : MonoBehaviour
{
    [SerializeField] private Vector3 forwardOffset;
    [SerializeField] private float forwardLength = 2f;
    [SerializeField] private LayerMask obstacleLayer;

    [NonSerialized] public ObstacleData currentObstacle;

    // private void Update()
    // {
    //     CheckForObstacle();
    // }

    public void CheckForObstacle()
    {
        Vector3 origin = transform.position + forwardOffset;
        Vector3 direction = transform.forward;

        bool forwardHit = Physics.Raycast(
            origin,
            direction,
            out RaycastHit forwardObstacle,
            forwardLength,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );

        currentObstacle = forwardHit
            ? new ObstacleData(forwardObstacle.collider.gameObject)
            : null;

        // Green = no obstacle, Red = obstacle detected.
        Debug.DrawRay(
            origin,
            direction * forwardLength,
            forwardHit ? Color.red : Color.green
        );

        if (forwardHit)
        {
            Debug.Log("Forward Obstacle: " +
                      currentObstacle.obstacle.name+"Height: "+currentObstacle.obstacleHeight+"Length: "+currentObstacle.obstacleLength);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.TransformPoint(forwardOffset);

        Gizmos.color =
            Application.isPlaying && currentObstacle != null
                ? Color.red
                : Color.green;

        Gizmos.DrawRay(origin, transform.forward * forwardLength);
        Gizmos.DrawSphere(origin, 0.04f);
    }
}