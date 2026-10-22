using System.Collections.Generic;
using UnityEngine;

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

    public string AnimationStateName => AnimationName;

    public bool CanDOAction(
        ObstacleData obstacle,
        State currentPlayerState)
    {
        if (obstacle == null || possibleStates == null || !possibleStates.Contains(currentPlayerState))
            return false;

        bool heightInRange = obstacle.obstacleHeight >= minHeight &&
                             obstacle.obstacleHeight <= maxHeight;
        bool lengthInRange = obstacle.obstacleLength >= minLength &&
                             obstacle.obstacleLength <= maxLength;

        return heightInRange && lengthInRange && obstacle.distanceToObstacle <= triggerDistance;
    }
}
