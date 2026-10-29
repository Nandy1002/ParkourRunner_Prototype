using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public enum State
{
    Walk,
    Run,
    Sprint
}

[CreateAssetMenu(fileName = "ParkourActionSO", menuName = "Parkour Action")]
public class ParkourActionSO : ScriptableObject
{
    [SerializeField] private string AnimationName;
    [SerializeField] private float minHeight;
    [SerializeField] private float maxHeight;
    [SerializeField] private float minLength;
    [SerializeField] private float maxLength;
    [SerializeField] private List<State> possibleStates;
    [SerializeField, Min(0f)] private float triggerDistance = 2.5f;
    [SerializeField] bool rotateToObstacle; 

    [Header("Target Matching")]
    [SerializeField] bool enableTargetMatching;
    [SerializeField] AvatarTarget matchBodyPart;
    [SerializeField] float matchStartTime;
    [SerializeField] float matchTargetTime;
    [SerializeField] Vector3 matchPosWeight = new Vector3(0,1,0);

    public string AnimationStateName => AnimationName;
    public bool RotateToObstacle => rotateToObstacle;
    public float TriggerDistance => triggerDistance;
    
    public bool EnableTargetMatching => enableTargetMatching;
    public AvatarTarget MatchBodyPart => matchBodyPart;
    public float MatchStartTime => matchStartTime;
    public float MatchTargetTime => matchTargetTime;
    public Vector3 MatchPosWeight => matchPosWeight;
    public Vector3 MatchPos {get; set;}


    public bool CanDOAction(ObstacleData obstacle,State currentPlayerState)
    {
        return CanMatchObstacle(obstacle, currentPlayerState) &&
               obstacle.distanceToObstacle <= triggerDistance;
    }

    public bool CanMatchObstacle(ObstacleData obstacle, State currentPlayerState)
    {
        if (obstacle == null || possibleStates == null || !possibleStates.Contains(currentPlayerState))
            return false;

        bool heightInRange = obstacle.obstacleHeight >= minHeight &&
                             obstacle.obstacleHeight <= maxHeight;
        bool lengthInRange = obstacle.obstacleLength >= minLength &&
                             obstacle.obstacleLength <= maxLength;
        return heightInRange && lengthInRange;
    }



}
