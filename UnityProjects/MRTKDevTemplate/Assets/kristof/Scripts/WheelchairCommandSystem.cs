using UnityEngine;

public class WheelchairCommandSystem : MonoBehaviour
{
    public static WheelchairCommandSystem Instance;

    public int forwardSpeed = 0;
    public int turnSpeed = 0;

    public int maxSpeed = 3;

    void Awake()
    {
        Instance = this;
    }

    public void Forward()
    {
        forwardSpeed = Mathf.Clamp(forwardSpeed + 1, -maxSpeed, maxSpeed);
        Debug.Log("Forward speed: " + forwardSpeed);
    }

    public void Backward()
    {
        forwardSpeed = Mathf.Clamp(forwardSpeed - 1, -maxSpeed, maxSpeed);
        Debug.Log("Forward speed: " + forwardSpeed);
    }

    public void TurnLeft()
    {
        turnSpeed = Mathf.Clamp(turnSpeed - 1, -maxSpeed, maxSpeed);
        Debug.Log("Turn speed: " + turnSpeed);
    }

    public void TurnRight()
    {
        turnSpeed = Mathf.Clamp(turnSpeed + 1, -maxSpeed, maxSpeed);
        Debug.Log("Turn speed: " + turnSpeed);
    }

    public void Stop()
    {
        forwardSpeed = 0;
        turnSpeed = 0;
    }
}
