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
                forwardObstacle.normal)
            : null;

        if (logScanResults && !_loggedScanStart)
        {
            Debug.Log(
                $"[ObstacleScanner] Raycasting {forwardLength:0.##}m ahead " +
                $"(layer mask {obstacleLayer.value}). Origin {origin}, " +
                $"direction {direction}.",
                this);
            _loggedScanStart = true;
        }

        if (logScanResults && forwardHit && currentObstacle.obstacle != _lastLoggedObstacle)
        {
            Debug.Log(
                $"[ObstacleScanner] HIT '{currentObstacle.obstacle.name}' " +
                $"on layer '{LayerMask.LayerToName(currentObstacle.obstacle.layer)}' " +
                $"at {forwardObstacle.distance:0.##}m " +
                $"(height {currentObstacle.obstacleHeight:0.##}m, " +
                $"depth {currentObstacle.obstacleLength:0.##}m, " +
                $"distance {currentObstacle.distanceToObstacle:0.##}m).",
                currentObstacle.obstacle);
            _lastLoggedObstacle = currentObstacle.obstacle;
        }
        else if (!forwardHit && _lastLoggedObstacle != null)
        {
            if (logScanResults)
                Debug.Log($"[ObstacleScanner] Cleared '{_lastLoggedObstacle.name}'.", this);

            _lastLoggedObstacle = null;
        }

        if (logScanResults && !forwardHit && !_loggedInitialMiss)
        {
            Debug.Log(
                $"[ObstacleScanner] No obstacle detected. Confirm it is within " +
                $"{forwardLength:0.##}m, on a layer included by mask {obstacleLayer.value}, " +
                $"and in front of direction {direction}.",
                this);
            _loggedInitialMiss = true;
        }

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
