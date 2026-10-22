using UnityEngine;

public class ObstacleScanner : MonoBehaviour
{
    [SerializeField] private Vector3 forwardOffset;
    [SerializeField] private float forwardLength = 2f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private bool logScanResults = true;

    public ObstacleData currentObstacle;

    private GameObject _lastLoggedObstacle;
    private bool _loggedScanStart;
    private bool _loggedInitialMiss;

    public bool CheckForObstacle()
    {
        return GetObstacleData() != null;
    }

    public ObstacleData GetObstacleData()
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
            ? new ObstacleData(
                forwardObstacle.collider,
                direction,
                transform.position,
                forwardObstacle.point,
                forwardObstacle.normal,
                forwardOffset.y)
            : null;

        Debug.DrawRay(
            origin,
            direction * forwardLength,
            forwardHit ? Color.red : Color.green
        );

        return currentObstacle;
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position + forwardOffset;

        Gizmos.color = currentObstacle != null ? Color.red : Color.cyan;
        Gizmos.DrawRay(origin, transform.forward * forwardLength);
        Gizmos.DrawSphere(origin, 0.06f);
    }
}
