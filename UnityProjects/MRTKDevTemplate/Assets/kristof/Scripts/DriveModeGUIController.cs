using UnityEngine;

public class DriveModeGUIController : MonoBehaviour
{
    public BleGrafSipky ble;

    // MAIN ALWAYS-VISIBLE BUTTONS
    [Header("Main Buttons")]
    public GameObject upButton;
    public GameObject downButton;
    public GameObject leftButton;
    public GameObject rightButton;
    public GameObject stopButton;

    // SPEED SELECT BUTTONS
    [Header("Forward Speed Select")]
    public GameObject forwardSlowButton;
    public GameObject forwardFastButton;

    [Header("Backward Speed Select")]
    public GameObject backwardSlowButton;
    public GameObject backwardFastButton;

    // DRIVING TURN BUTTONS
    [Header("Driving Turn Buttons")]
    public GameObject driveLeftButton;
    public GameObject driveRightButton;

    // MATERIALS
    [Header("Materials")]
    public Material idleTurnMaterial;
    public Material drivingTurnMaterial;

    private enum Mode { Idle, SelectForwardSpeed, SelectBackwardSpeed, DrivingForward, DrivingBackward }
    private Mode mode = Mode.Idle;


    // ======================================================================
    // INITIALIZATION
    // ======================================================================

    private void Start()
    {
        SwitchToIdleUI();
    }


    // ======================================================================
    // RENDERER FINDER FOR MRTK BUTTONS
    // ======================================================================
    private Renderer GetButtonRenderer(GameObject buttonObj)
    {
        if (buttonObj == null) return null;

        string path = "UIBackplateOuterGeometry/UX.Button.BackplateOuterGeometry";
        Transform t = buttonObj.transform.Find(path);

        if (t != null)
        {
            var r = t.GetComponent<Renderer>();
            if (r != null) return r;
        }

        Transform fallback = buttonObj.transform.Find("CompressibleButtonVisuals/FrontPlate");
        if (fallback != null)
        {
            var r = fallback.GetComponent<Renderer>();
            if (r != null) return r;
        }

        return null;
    }


    private void SetDrivingTurnVisuals(bool drivingMode)
    {
        var rendL = GetButtonRenderer(driveLeftButton);
        var rendR = GetButtonRenderer(driveRightButton);

        if (rendL != null) rendL.material = drivingMode ? drivingTurnMaterial : idleTurnMaterial;
        if (rendR != null) rendR.material = drivingMode ? drivingTurnMaterial : idleTurnMaterial;
    }


    // ======================================================================
    // UI STATE SWITCHING
    // ======================================================================

    private void SwitchToIdleUI()
    {
        mode = Mode.Idle;

        // Hide speed selects
        forwardSlowButton.SetActive(false);
        forwardFastButton.SetActive(false);
        backwardSlowButton.SetActive(false);
        backwardFastButton.SetActive(false);

        // Show normal arrows
        leftButton.SetActive(true);
        rightButton.SetActive(true);

        // Hide driving turn buttons
        driveLeftButton.SetActive(false);
        driveRightButton.SetActive(false);

        // Restore normal visuals
        SetDrivingTurnVisuals(false);

        // Main buttons always visible
        upButton.SetActive(true);
        downButton.SetActive(true);
        stopButton.SetActive(false);
    }

    private void SwitchToSelectForwardSpeedUI()
    {
        mode = Mode.SelectForwardSpeed;

        forwardSlowButton.SetActive(true);
        forwardFastButton.SetActive(true);
    }

    private void SwitchToSelectBackwardSpeedUI()
    {
        mode = Mode.SelectBackwardSpeed;

        backwardSlowButton.SetActive(true);
        backwardFastButton.SetActive(true);
    }

    private void SwitchToDrivingForwardUI()
    {
        mode = Mode.DrivingForward;

        // replace DOWN with STOP
        downButton.SetActive(false);
        stopButton.SetActive(true);

        // replace left/right with driving versions
        leftButton.SetActive(false);
        rightButton.SetActive(false);

        driveLeftButton.SetActive(true);
        driveRightButton.SetActive(true);

        SetDrivingTurnVisuals(true);
    }

    private void SwitchToDrivingBackwardUI()
    {
        mode = Mode.DrivingBackward;

        upButton.SetActive(false);
        stopButton.SetActive(true);

        leftButton.SetActive(false);
        rightButton.SetActive(false);

        driveLeftButton.SetActive(true);
        driveRightButton.SetActive(true);

        SetDrivingTurnVisuals(true);
    }


    // ======================================================================
    // BUTTON EVENTS – EXACT BEHAVIOR
    // ======================================================================

    // -------------------------------
    // FORWARD SELECT
    // -------------------------------
    public void OnUpGaze()
    {
        if (mode == Mode.Idle)
            SwitchToSelectForwardSpeedUI();
    }

    public void OnForwardSlow()
    {
        ble.ForwardSlow();
        SwitchToDrivingForwardUI();
    }

    public void OnForwardFast()
    {
        ble.ForwardFast();
        SwitchToDrivingForwardUI();
    }

    // -------------------------------
    // BACKWARD SELECT
    // -------------------------------
    public void OnDownGaze()
    {
        if (mode == Mode.Idle)
            SwitchToSelectBackwardSpeedUI();
    }

    public void OnBackwardSlow()
    {
        ble.BackwardSlow();
        SwitchToDrivingBackwardUI();
    }

    public void OnBackwardFast()
    {
        ble.BackwardFast();
        SwitchToDrivingBackwardUI();
    }


    // -------------------------------
    // DRIVING TURN – FORWARD
    // -------------------------------
    public void OnDriveLeftStart()
    {
        if (mode == Mode.DrivingForward)
            ble.StartForwardLeft();

        if (mode == Mode.DrivingBackward)
            ble.StartBackwardLeft();
    }

    public void OnDriveLeftStop()
    {
        if (mode == Mode.DrivingForward || mode == Mode.DrivingBackward)
            ble.StopTurning();
    }

    public void OnDriveRightStart()
    {
        if (mode == Mode.DrivingForward)
            ble.StartForwardRight();

        if (mode == Mode.DrivingBackward)
            ble.StartBackwardRight();
    }

    public void OnDriveRightStop()
    {
        if (mode == Mode.DrivingForward || mode == Mode.DrivingBackward)
            ble.StopTurning();
    }


    // -------------------------------
    // STOP
    // -------------------------------
    public void OnStop()
    {
        SwitchToIdleUI();
    }
}
