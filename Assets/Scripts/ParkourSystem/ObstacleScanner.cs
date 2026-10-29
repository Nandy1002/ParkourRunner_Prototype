using UnityEngine;

public class ObstacleScanner : MonoBehaviour
{
    [SerializeField] private Vector3 forwardOffset;
    [SerializeField] private float forwardLength = 2f;
    [SerializeField, Range(0f, 1f)] private float minimumFacingDot = 0.7f;
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
        return GetObstacleData(forwardLength);
    }

    public ObstacleData GetObstacleData(float minimumScanDistance)
    {
        Vector3 origin = transform.position + forwardOffset;
        Vector3 direction = transform.forward;
        float scanDistance = Mathf.Max(forwardLength, minimumScanDistance);

        bool forwardHit = Physics.Raycast(
            origin,
            direction,
            out RaycastHit forwardObstacle,
            scanDistance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );

        // A ray can graze the side of an obstacle at a corner. Only accept
        // vertical faces that are reasonably square to the player's approach.
        // The dot product is 1 for a head-on face and 0 for a side-on face.
        if (forwardHit)
        {
            Vector3 horizontalNormal = Vector3.ProjectOnPlane(forwardObstacle.normal, Vector3.up).normalized;
            float facingDot = Vector3.Dot(-horizontalNormal, direction);
            forwardHit = horizontalNormal.sqrMagnitude > 0.0001f && facingDot >= minimumFacingDot;
        }

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
            direction * scanDistance,
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
