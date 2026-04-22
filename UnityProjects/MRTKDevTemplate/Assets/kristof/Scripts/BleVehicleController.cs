using UnityEngine;

public class BleVehicleController : MonoBehaviour, IEyeCloseStopper
{
    [Header("BLE Core")]
    public TestBle ble;  // základný BLE skript, ktorý obsahuje SetXY()

    [Header("Forward / Backward speeds")]
    public byte forwardSlow = 158;
    public byte forwardFast = 168;

    public byte leftSlow = 88;
    public byte leftFast = 78;

    public byte rightSlow = 168;
    public byte rightFast = 178;

    public byte backwardSlow = 98;
    public byte backwardFast = 88;

    [Header("Turning configuration")]
    public float maxTurnOffset = 40f;    // max X posun
    public float turnRampTime = 1.2f;    // čas kým dosiahne max intenzitu
    public float turnSmooth = 8f;        // pre plynulosť

    private bool isDrivingForward = false;
    private bool isDrivingBackward = false;

    private bool isTurning = false;
    private int turnDirection = 0;       // -1 = left, 1 = right
    private float turnHoldTime = 0f;

    private byte baseY = 128;
    private byte baseX = 128;
    private float currentX = 128;        // lerpované X pri turningu

    public bool IsTurning => isTurning;
    public int TurnDirection => turnDirection;

    public float TurnIntensity01
    {
        get
        {
            if (!isTurning) return 0f;
            return Mathf.Clamp01(turnHoldTime / turnRampTime);
        }
    }



    // ===============================================================
    // BASE API (VOLÁ GUI)
    // ===============================================================

    // FORWARD
    public void DriveForwardSlow()
    {
        Debug.Log("Forward SLOW");

        baseY = forwardSlow;
        isDrivingForward = true;
        isDrivingBackward = false;
        StopTurningInternal();

        ble.SetXY(128, baseY);
    }

    public void DriveForwardFast()
    {
        Debug.Log("Forward FAST");

        baseY = forwardFast;
        isDrivingForward = true;
        isDrivingBackward = false;
        StopTurningInternal();

        ble.SetXY(128, baseY);
    }

    // LEFT
    public void DriveLeftSlow()
    {
        Debug.Log("Left SLOW");

        baseX = leftSlow;
        isDrivingForward = false;
        isDrivingBackward = false;

        ble.SetXY(baseX, 128);
    }

    public void DriveLeftFast()
    {
        Debug.Log("Left FAST");

        baseX = leftFast;
        isDrivingForward = false;
        isDrivingBackward = false;

        ble.SetXY(baseX, 128);
    }

    // RIGHT
    public void DriveRightSlow()
    {
        Debug.Log("Right SLOW");

        baseX = rightSlow;
        isDrivingForward = false;
        isDrivingBackward = false;

        ble.SetXY(baseX, 128);
    }

    public void DriveRightFast()
    {
        Debug.Log("Right FAST");

        baseX = rightFast;
        isDrivingForward = false;
        isDrivingBackward = false;

        ble.SetXY(baseX, 128);
    }

    //BACKWARD
    public void DriveBackwardSlow()
    {
        Debug.Log("Backward SLOW");

        baseY = backwardSlow;
        isDrivingBackward = true;
        isDrivingForward = false;
        StopTurningInternal();

        ble.SetXY(128, baseY);
    }

    public void DriveBackwardFast()
    {
        Debug.Log("Backward FAST");

        baseY = backwardFast;
        isDrivingBackward = true;
        isDrivingForward = false;
        StopTurningInternal();

        ble.SetXY(128, baseY);
    }


    // ===============================================================
    // TURNING (LEFT / RIGHT)
    // ===============================================================

    public void StartTurnLeft()
    {
        if (!isDrivingForward && !isDrivingBackward)
            return;

        isTurning = true;
        turnDirection = -1;
        turnHoldTime = 0f;
        Debug.Log("<color=yellow>StartTurnLeft</color>");

    }

    public void StartTurnRight()
    {
        if (!isDrivingForward && !isDrivingBackward)
            return;

        isTurning = true;
        turnDirection = 1;
        turnHoldTime = 0f;

        Debug.Log("<color=yellow>StartTurnRight</color>");
    }

    public void StopTurn()
    {
        StopTurningInternal();
        ble.SetXY(128, baseY);  // späť do roviny

        Debug.Log("StopTurn");
    }


    private void StopTurningInternal()
    {
        isTurning = false;
        turnDirection = 0;
        turnHoldTime = 0f;
        currentX = 128;
    }


    // ===============================================================
    // UPDATE DRIVING (DYNAMIC TURNING)
    // ===============================================================

    private void Update()
    {
        if (!isTurning)
            return;

        // čas ako dlho sa človek pozerá na tlačidlo
        turnHoldTime += Time.deltaTime;

        float intensity01 = Mathf.Clamp01(turnHoldTime / turnRampTime);

        float targetOffset = intensity01 * maxTurnOffset * turnDirection;

        // plynule približovanie X hodnoty
        currentX = Mathf.Lerp(currentX, 128 + targetOffset, Time.deltaTime * turnSmooth);

        ble.SetXY((byte)currentX, baseY);

        Debug.Log($"TURNING -> dir: {turnDirection}, " +
                  $"intensity: {intensity01:F2}, " +
                  $"X: {currentX:F1}, Y: {baseY}");

    }


    // ===============================================================
    // STOP EVERYTHING
    // ===============================================================

    public void StopAll()
    {
        isDrivingForward = false;
        isDrivingBackward = false;

        StopTurningInternal();

        ble.SetXY(128, 128);  // neutral
    }

    public void EyeCloseStop()
    {
        Debug.Log("EyeCloseStop");
        StopAll();
    }
}
