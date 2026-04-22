using UnityEngine;
using System;
using System.Reflection;

public class DriveModeGUIController_v2 : MonoBehaviour, IEyeCloseStopper
{
    [Header("Submenu controllers")]
    public SubmenuAutoHide forwardSubmenu;
    public SubmenuAutoHide leftSubmenu;
    public SubmenuAutoHide rightSubmenu;
    public SubmenuAutoHide backwardSubmenu;
    public float confirmTimeout = 3f;

    private SubmenuAutoHide activeSubmenu;

    [Header("Controllers")]
    public BleVehicleController vehicle;
    public ButtonHighlightManager highlight;

    [Header("Main Buttons")]
    public GameObject upButton;
    public GameObject downButton;
    public GameObject leftButton;
    public GameObject rightButton;
    public GameObject stopButtonForward;
    public GameObject stopButtonBackward;
    public GameObject confirmButtonForward;
    public GameObject confirmButtonBackward;

    [Header("Left Speed Select")]
    public GameObject leftSlowButton;
    public GameObject leftFastButton;

    [Header("Right Speed Select")]
    public GameObject rightSlowButton;
    public GameObject rightFastButton;

    [Header("Forward Speed Select")]
    public GameObject forwardSlowButton;
    public GameObject forwardFastButton;

    [Header("Backward Speed Select")]
    public GameObject backwardSlowButton;
    public GameObject backwardFastButton;

    [Header("Driving Turn Buttons")]
    public GameObject driveLeftButton;
    public GameObject driveRightButton;


    [Header("Materials")]
    public Material idleTurnMaterial;
    public Material drivingTurnMaterial;

    [Header("Turn Blend (Visual)")]
    [Tooltip("Ako rýchlo sa vizuál dobieha k cieľovej intenzite (väčšie = rýchlejšie).")]
    public float turnVisualSmooth = 14f;

    [Tooltip("Zvyš intenzitu vizuálu (napr. 1.2) ak je prechod slabý.")]
    public float turnVisualGain = 1f;

    [Tooltip("Debug vypíše, či našiel renderery a či číta turn state.")]
    public bool debugTurnVisuals = false;

    private enum Mode
    {
        Idle,
        SelectForwardSpeed,
        SelectLeftSpeed,
        SelectRightSpeed,
        SelectBackwardSpeed,
        DrivingForward,
        DrivingBackward,
        TurningInPlace
    }

    private bool turnLeftHeld = false;
    private bool turnRightHeld = false;


    private Mode mode = Mode.Idle;
    private Coroutine confirmTimeoutRoutine;

    // ===============================================================
    // TURN VISUALS (Material.Lerp + fallback)
    // ===============================================================

    private Renderer driveLeftRenderer;
    private Renderer driveRightRenderer;

    // runtime instanced materials assigned to renderers
    private Material leftRuntimeMat;
    private Material rightRuntimeMat;

    // for smooth transitions
    private float leftVisualT = 0f;
    private float rightVisualT = 0f;

    // fallback: color lerp using MPB if materials have different shaders or Lerp not usable
    private bool useMaterialLerp = true;
    private MaterialPropertyBlock mpbLeft;
    private MaterialPropertyBlock mpbRight;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // reflection cache for BleVehicleController private fields
    private FieldInfo fiIsTurning;
    private FieldInfo fiTurnDirection;
    private FieldInfo fiTurnHoldTime;
    private FieldInfo fiTurnRampTime;

    private PropertyInfo piIsTurning;
    private PropertyInfo piTurnDirection;
    private PropertyInfo piTurnIntensity;

    // gating: turn povolený len keď bola zvolená rýchlosť
    private bool forwardSpeedSelected = false;
    private bool backwardSpeedSelected = false;

    private bool canTurnNow = false;


    // ===============================================================
    // INIT
    // ===============================================================

    private void Awake()
    {
        // cache renderer references for drive turn buttons
        driveLeftRenderer = GetButtonRenderer(driveLeftButton);
        driveRightRenderer = GetButtonRenderer(driveRightButton);

        mpbLeft = new MaterialPropertyBlock();
        mpbRight = new MaterialPropertyBlock();

        if (driveLeftRenderer == null)
            Debug.LogWarning("[DriveModeGUIController_v2] DriveLeft renderer NOT found (check button hierarchy/path).");
        if (driveRightRenderer == null)
            Debug.LogWarning("[DriveModeGUIController_v2] DriveRight renderer NOT found (check button hierarchy/path).");

        // validate materials
        if (idleTurnMaterial == null || drivingTurnMaterial == null)
        {
            Debug.LogWarning("[DriveModeGUIController_v2] idleTurnMaterial or drivingTurnMaterial is not assigned. Turn blending will not work.");
            useMaterialLerp = false;
        }
        else
        {
            // Material.Lerp works best if shaders match
            if (idleTurnMaterial.shader != drivingTurnMaterial.shader)
            {
                useMaterialLerp = false;
                Debug.LogWarning("[DriveModeGUIController_v2] idleTurnMaterial and drivingTurnMaterial have DIFFERENT shaders. " +
                                 "Falling back to color-only lerp via MaterialPropertyBlock (if shader supports _Color/_BaseColor).");
            }
            else
            {
                useMaterialLerp = true;
            }
        }

        // Create runtime materials and assign them so we never touch shared assets
        SetupRuntimeTurnMaterials();

        // cache reflection access to turn values (so you don't need to change BleVehicleController)
        CacheVehicleTurnAccessors();
    }

    private void Start()
    {
        SwitchToIdleUI();
    }

    private void SetupRuntimeTurnMaterials()
    {
        if (driveLeftRenderer != null)
        {
            if (idleTurnMaterial != null)
            {
                leftRuntimeMat = new Material(idleTurnMaterial);
                driveLeftRenderer.material = leftRuntimeMat;
            }
        }

        if (driveRightRenderer != null)
        {
            if (idleTurnMaterial != null)
            {
                rightRuntimeMat = new Material(idleTurnMaterial);
                driveRightRenderer.material = rightRuntimeMat;
            }
        }
    }

    private void CacheVehicleTurnAccessors()
    {
        if (vehicle == null) return;

        Type t = vehicle.GetType();

        // prefer public properties if they exist
        piIsTurning = t.GetProperty("IsTurning", BindingFlags.Instance | BindingFlags.Public);
        piTurnDirection = t.GetProperty("TurnDirection", BindingFlags.Instance | BindingFlags.Public);
        piTurnIntensity = t.GetProperty("TurnIntensity01", BindingFlags.Instance | BindingFlags.Public);

        // fallback to private fields (your current BleVehicleController has these)
        fiIsTurning = t.GetField("isTurning", BindingFlags.Instance | BindingFlags.NonPublic);
        fiTurnDirection = t.GetField("turnDirection", BindingFlags.Instance | BindingFlags.NonPublic);
        fiTurnHoldTime = t.GetField("turnHoldTime", BindingFlags.Instance | BindingFlags.NonPublic);
        fiTurnRampTime = t.GetField("turnRampTime", BindingFlags.Instance | BindingFlags.NonPublic);

        if (debugTurnVisuals)
        {
            Debug.Log($"[DriveModeGUIController_v2] Turn accessors: props(IsTurning={piIsTurning!=null}, TurnDirection={piTurnDirection!=null}, TurnIntensity01={piTurnIntensity!=null}) " +
                      $"fields(isTurning={fiIsTurning!=null}, turnDirection={fiTurnDirection!=null}, turnHoldTime={fiTurnHoldTime!=null}, turnRampTime={fiTurnRampTime!=null})");
        }
    }

    // ===============================================================
    // MRTK BUTTON RENDERER
    // ===============================================================

    private Renderer GetButtonRenderer(GameObject btn)
    {
        if (btn == null) return null;

        string p = "UIBackplateOuterGeometry/UX.Button.BackplateOuterGeometry";
        Transform t = btn.transform.Find(p);

        if (t != null)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) return r;
        }

        Transform fallback = btn.transform.Find("CompressibleButtonVisuals/FrontPlate");
        if (fallback != null)
        {
            Renderer r = fallback.GetComponent<Renderer>();
            if (r != null) return r;
        }

        return null;
    }

    // ===============================================================
    // TURN VISUAL BLEND APPLY
    // ===============================================================

    private bool TryGetTurnState(out bool isTurning, out int direction, out float intensity01)
    {
        isTurning = false;
        direction = 0;
        intensity01 = 0f;

        if (vehicle == null) return false;

        try
        {
            // 1) public properties (if you added them)
            if (piIsTurning != null && piTurnDirection != null)
            {
                isTurning = (bool)piIsTurning.GetValue(vehicle);
                direction = Convert.ToInt32(piTurnDirection.GetValue(vehicle));

                if (piTurnIntensity != null)
                {
                    intensity01 = Mathf.Clamp01(Convert.ToSingle(piTurnIntensity.GetValue(vehicle)));
                }
                else
                {
                    // if intensity prop doesn't exist, compute from fields if available
                    if (fiTurnHoldTime != null && fiTurnRampTime != null)
                    {
                        float hold = Convert.ToSingle(fiTurnHoldTime.GetValue(vehicle));
                        float ramp = Convert.ToSingle(fiTurnRampTime.GetValue(vehicle));
                        intensity01 = (ramp <= 0.0001f) ? 1f : Mathf.Clamp01(hold / ramp);
                    }
                }

                return true;
            }

            // 2) private fields fallback (works with your current BleVehicleController)
            if (fiIsTurning != null && fiTurnDirection != null)
            {
                isTurning = (bool)fiIsTurning.GetValue(vehicle);
                direction = Convert.ToInt32(fiTurnDirection.GetValue(vehicle));

                if (fiTurnHoldTime != null && fiTurnRampTime != null)
                {
                    float hold = Convert.ToSingle(fiTurnHoldTime.GetValue(vehicle));
                    float ramp = Convert.ToSingle(fiTurnRampTime.GetValue(vehicle));
                    intensity01 = (ramp <= 0.0001f) ? 1f : Mathf.Clamp01(hold / ramp);
                }
                else
                {
                    intensity01 = isTurning ? 1f : 0f;
                }

                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DriveModeGUIController_v2] Failed to read turn state: " + e.Message);
            return false;
        }

        return false;
    }

    private void ApplyTurnBlend(float leftTarget01, float rightTarget01)
    {
        // gain + clamp
        leftTarget01 = Mathf.Clamp01(leftTarget01 * Mathf.Max(0f, turnVisualGain));
        rightTarget01 = Mathf.Clamp01(rightTarget01 * Mathf.Max(0f, turnVisualGain));

        // smooth
        leftVisualT = Mathf.Lerp(leftVisualT, leftTarget01, Time.deltaTime * turnVisualSmooth);
        rightVisualT = Mathf.Lerp(rightVisualT, rightTarget01, Time.deltaTime * turnVisualSmooth);

        // If we can do material lerp (same shader), do it
        if (useMaterialLerp && idleTurnMaterial != null && drivingTurnMaterial != null)
        {
            if (leftRuntimeMat != null) leftRuntimeMat.Lerp(idleTurnMaterial, drivingTurnMaterial, leftVisualT);
            if (rightRuntimeMat != null) rightRuntimeMat.Lerp(idleTurnMaterial, drivingTurnMaterial, rightVisualT);
            return;
        }

        // Fallback: color-only lerp using MPB if shader supports _Color/_BaseColor
        if (idleTurnMaterial == null || drivingTurnMaterial == null) return;

        Color idleC = ReadMatColor(idleTurnMaterial, Color.white);
        Color driveC = ReadMatColor(drivingTurnMaterial, idleC);

        Color lc = Color.Lerp(idleC, driveC, leftVisualT);
        Color rc = Color.Lerp(idleC, driveC, rightVisualT);

        ApplyColorMPB(driveLeftRenderer, mpbLeft, lc);
        ApplyColorMPB(driveRightRenderer, mpbRight, rc);
    }

    private Color ReadMatColor(Material m, Color fallback)
    {
        if (m == null) return fallback;
        if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
        if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
        return fallback;
    }

    private void ApplyColorMPB(Renderer r, MaterialPropertyBlock mpb, Color c)
    {
        if (r == null) return;

        r.GetPropertyBlock(mpb);

        Material sm = r.sharedMaterial;
        if (sm != null)
        {
            if (sm.HasProperty(BaseColorId)) mpb.SetColor(BaseColorId, c);
            if (sm.HasProperty(ColorId)) mpb.SetColor(ColorId, c);
        }

        r.SetPropertyBlock(mpb);
    }

    // ===============================================================
    // UPDATE: turn visuals driven by turn intensity
    // ===============================================================

    private void Update()
    {
        // only when turn buttons are meant to be active/visible in driving
        bool inDrivingMode = (mode == Mode.DrivingForward || mode == Mode.DrivingBackward);

        if (!inDrivingMode || vehicle == null)
        {
            ApplyTurnBlend(0f, 0f);
            return;
        }

        bool ok = TryGetTurnState(out bool turning, out int dir, out float intensity);

        if (debugTurnVisuals && ok)
        {
            // Avoid spamming too much; keep it lightweight
            // (You can comment this out if it annoys)
            // Debug.Log($"Turn: turning={turning}, dir={dir}, intensity={intensity:0.00}");
        }

        float leftT = 0f;
        float rightT = 0f;

        if (turning)
        {
            if (dir < 0) leftT = intensity;
            else if (dir > 0) rightT = intensity;
        }

        ApplyTurnBlend(leftT, rightT);

        if (inDrivingMode && vehicle != null)
        {
            if (!turnLeftHeld && !turnRightHeld && vehicle.IsTurning)
            {
                vehicle.StopTurn();
            }
        }

    }

    // ===============================================================
    // UI MODE CONTROL
    // ===============================================================

    private void HideCurrentSelectionSubmenu()
    {
        switch (mode)
        {
            case Mode.SelectForwardSpeed:
                forwardSlowButton.SetActive(false);
                forwardFastButton.SetActive(false);
                break;

            case Mode.SelectLeftSpeed:
                leftSlowButton.SetActive(false);
                leftFastButton.SetActive(false);
                break;

            case Mode.SelectRightSpeed:
                rightSlowButton.SetActive(false);
                rightFastButton.SetActive(false);
                break;

            case Mode.SelectBackwardSpeed:
                backwardSlowButton.SetActive(false);
                backwardFastButton.SetActive(false);
                break;
        }

        mode = Mode.Idle;
    }

    private void SwitchToIdleUI()
    {
        CancelConfirmTimeout();

        mode = Mode.Idle;

        forwardSubmenu?.ResetSubmenu();
        leftSubmenu?.ResetSubmenu();
        rightSubmenu?.ResetSubmenu();
        backwardSubmenu?.ResetSubmenu();
        activeSubmenu = null;

        forwardSlowButton.SetActive(false);
        forwardFastButton.SetActive(false);
        backwardSlowButton.SetActive(false);
        backwardFastButton.SetActive(false);

        leftButton.SetActive(true);
        leftFastButton.SetActive(false);
        leftSlowButton.SetActive(false);

        rightButton.SetActive(true);
        rightSlowButton.SetActive(false);
        rightFastButton.SetActive(false);

        driveLeftButton.SetActive(false);
        driveRightButton.SetActive(false);

        upButton.SetActive(true);
        downButton.SetActive(true);

        HideStop();
        HideConfirm();

        if (vehicle != null) vehicle.StopAll();

        // reset turn visuals
        leftVisualT = 0f;
        rightVisualT = 0f;
        ApplyTurnBlend(0f, 0f);
    }

    private void ShowStop(bool forward)
    {
        stopButtonForward.SetActive(forward);
        stopButtonBackward.SetActive(!forward);
    }

    private void HideStop()
    {
        stopButtonForward.SetActive(false);
        stopButtonBackward.SetActive(false);
    }

    private void ShowConfirm(bool forward)
    {
        confirmButtonForward.SetActive(forward);
        confirmButtonBackward.SetActive(!forward);
    }

    private void HideConfirm()
    {
        confirmButtonForward.SetActive(false);
        confirmButtonBackward.SetActive(false);
    }


    private void SwitchToSelectForwardSpeedUI()
    {
        mode = Mode.SelectForwardSpeed;
        forwardSlowButton.SetActive(true);
        forwardFastButton.SetActive(true);
    }

    private void SwitchToSelectLeftSpeedUI()
    {
        mode = Mode.SelectLeftSpeed;
        leftFastButton.SetActive(true);
        leftSlowButton.SetActive(true);
    }

    private void SwitchToSelectRightSpeedUI()
    {
        mode = Mode.SelectRightSpeed;
        rightFastButton.SetActive(true);
        rightSlowButton.SetActive(true);
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

        downButton.SetActive(false);
        ShowStop(forward: true);

        leftButton.SetActive(false);
        leftFastButton.SetActive(false);
        leftSlowButton.SetActive(false);

        rightButton.SetActive(false);
        rightFastButton.SetActive(false);
        rightSlowButton.SetActive(false);

        driveLeftButton.SetActive(true);
        driveRightButton.SetActive(true);

        // ensure runtime mats exist even if scene assigned later
        if (leftRuntimeMat == null || rightRuntimeMat == null) SetupRuntimeTurnMaterials();
    }

    private void SwitchToTurningUI()
    {
        mode = Mode.TurningInPlace;
        downButton.SetActive(false);
        ShowStop(forward: true);

        // in-place turning uses other controls; keep drive turn visuals neutral
        ApplyTurnBlend(0f, 0f);
    }

    private void CloseOtherSubmenus(SubmenuAutoHide currentSubmenu)
    {
        if (forwardSubmenu != null && forwardSubmenu != currentSubmenu)
            forwardSubmenu.ForceClose();

        if (leftSubmenu != null && leftSubmenu != currentSubmenu)
            leftSubmenu.ForceClose();

        if (rightSubmenu != null && rightSubmenu != currentSubmenu)
            rightSubmenu.ForceClose();

        if (backwardSubmenu != null && backwardSubmenu != currentSubmenu)
            backwardSubmenu.ForceClose();
    }

    private void StartConfirmTimeout()
    {
        if (confirmTimeoutRoutine != null)
            StopCoroutine(confirmTimeoutRoutine);

        confirmTimeoutRoutine = StartCoroutine(ConfirmTimeoutCoroutine());
    }

    private void CancelConfirmTimeout()
    {
        if (confirmTimeoutRoutine != null)
        {
            StopCoroutine(confirmTimeoutRoutine);
            confirmTimeoutRoutine = null;
        }
    }

    private System.Collections.IEnumerator ConfirmTimeoutCoroutine()
    {
        yield return new WaitForSeconds(confirmTimeout);

        HideConfirm();
        confirmTimeoutRoutine = null;
    }

    public void CanTurn()
    {
        canTurnNow = true;
    }

    public void CantTurn()
    {
        canTurnNow = false;
    }

    // ===============================================================
    // UI EVENTS → DRIVING LOGIC
    // ===============================================================

    // FORWARD SELECT
    public void OnUpGaze()
    {
        CloseOtherSubmenus(forwardSubmenu);
        forwardSubmenu?.OpenTemp();
    }

    public void OnForwardSlow()
    {
        CloseOtherSubmenus(forwardSubmenu);

        activeSubmenu = forwardSubmenu;
        forwardSubmenu.PinSelected();

        vehicle.DriveForwardSlow();
        highlight.Highlight(forwardSlowButton);
        forwardSpeedSelected = true;
        backwardSpeedSelected = false;
        SwitchToDrivingForwardUI();
    }

    public void OnForwardFast()
    {
        CloseOtherSubmenus(forwardSubmenu);

        activeSubmenu = forwardSubmenu;
        forwardSubmenu.PinSelected();

        vehicle.DriveForwardFast();
        highlight.Highlight(forwardFastButton);
        forwardSpeedSelected = true;
        backwardSpeedSelected = false;
        SwitchToDrivingForwardUI();
    }

    // LEFT SELECT
    public void OnLeftGaze()
    {
        CloseOtherSubmenus(leftSubmenu);
        leftSubmenu?.OpenTemp();
    }

    public void OnLeftSlow()
    {
        CloseOtherSubmenus(leftSubmenu);

        activeSubmenu = leftSubmenu;
        leftSubmenu.PinSelected();

        vehicle.DriveLeftSlow();
        highlight.Highlight(leftSlowButton);
        SwitchToTurningUI();
    }

    public void OnLeftFast()
    {
        CloseOtherSubmenus(leftSubmenu);

        activeSubmenu = leftSubmenu;
        leftSubmenu.PinSelected();

        vehicle.DriveLeftFast();
        highlight.Highlight(leftFastButton);
        SwitchToTurningUI();
    }

    // RIGHT SELECT
    public void OnRightGaze()
    {
        CloseOtherSubmenus(rightSubmenu);
        rightSubmenu?.OpenTemp();
    }

    public void OnRightSlow()
    {
        CloseOtherSubmenus(rightSubmenu);

        activeSubmenu = rightSubmenu;
        rightSubmenu.PinSelected();

        vehicle.DriveRightSlow();
        highlight.Highlight(rightSlowButton);
        SwitchToTurningUI();
    }

    public void OnRightFast()
    {
        CloseOtherSubmenus(rightSubmenu);

        activeSubmenu = rightSubmenu;
        rightSubmenu.PinSelected();

        vehicle.DriveRightFast();
        highlight.Highlight(rightFastButton);
        SwitchToTurningUI();
    }

    // BACKWARD SELECT

    public void OnDownGaze()
    {
        CloseOtherSubmenus(backwardSubmenu);
        backwardSubmenu?.OpenTemp();
    }

    public void OnBackwardSlow()
    {
        CloseOtherSubmenus(backwardSubmenu);

        activeSubmenu = backwardSubmenu;
        backwardSubmenu.PinSelected();

        vehicle.DriveBackwardSlow();
        highlight.Highlight(backwardSlowButton);
        backwardSpeedSelected = true;
        forwardSpeedSelected = false;
        SwitchToDrivingBackwardUI();   // <-- pridať
    }

    public void OnBackwardFast()
    {
        CloseOtherSubmenus(backwardSubmenu);

        activeSubmenu = backwardSubmenu;
        backwardSubmenu.PinSelected();

        vehicle.DriveBackwardFast();
        highlight.Highlight(backwardFastButton);
        backwardSpeedSelected = true;
        forwardSpeedSelected = false;
        SwitchToDrivingBackwardUI();   // <-- pridať
    }

    private void SwitchToDrivingBackwardUI()
    {
        mode = Mode.DrivingBackward;

        upButton.SetActive(false);
        ShowStop(forward: false);        // stop namiesto neho

        leftButton.SetActive(false);
        leftFastButton.SetActive(false);
        leftSlowButton.SetActive(false);

        rightButton.SetActive(false);
        rightFastButton.SetActive(false);
        rightSlowButton.SetActive(false);

        driveLeftButton.SetActive(true);
        driveRightButton.SetActive(true);

        if (leftRuntimeMat == null || rightRuntimeMat == null)
            SetupRuntimeTurnMaterials();
    }

    // TURNING DURING DRIVING
    // (Vizuál riadi Update() podľa intenzity z BleVehicleController.)
    public void OnDriveLeftStart()
    {
        bool canTurn = (mode == Mode.DrivingForward && forwardSpeedSelected)
                       || (mode == Mode.DrivingBackward && backwardSpeedSelected);

        if (!canTurn)
        {
            vehicle?.StopTurn();   // 🔥 DÔLEŽITÉ: zruš starý stav
            turnLeftHeld = false;
            turnRightHeld = false;
            ApplyTurnBlend(0f, 0f);
            return;
        }

        turnLeftHeld = true;
        turnRightHeld = false;

        if (canTurnNow)
        {
            vehicle.StartTurnLeft();
        }
    }

    public void OnDriveLeftStop()
    {
        turnLeftHeld = false;
        if (!turnRightHeld)
            vehicle.StopTurn();
        canTurnNow = false;
    }

    public void OnDriveRightStart()
    {
        bool canTurn = (mode == Mode.DrivingForward && forwardSpeedSelected)
                       || (mode == Mode.DrivingBackward && backwardSpeedSelected);

        if (!canTurn)
        {
            vehicle?.StopTurn();   // 🔥 reset stale turn
            turnLeftHeld = false;
            turnRightHeld = false;
            ApplyTurnBlend(0f, 0f);
            return;
        }

        turnRightHeld = true;
        turnLeftHeld = false;

        if (canTurnNow)
        {
            vehicle.StartTurnRight();
        }
    }

    public void OnDriveRightStop()
    {
        turnRightHeld = false;
        if (!turnLeftHeld)
            vehicle.StopTurn();
        canTurnNow = false;
    }



    public void OnStopGaze()
    {
        ShowConfirm(forward: mode == Mode.DrivingForward || mode == Mode.TurningInPlace);
        StartConfirmTimeout();
    }

    public void OnStop()
    {
        if (vehicle != null)
            vehicle.StopAll();

        if (activeSubmenu != null)
        {
            activeSubmenu.ForceClose();
            activeSubmenu = null;
        }

        forwardSpeedSelected = false;
        backwardSpeedSelected = false;
        turnLeftHeld = false;
        turnRightHeld = false;

        leftVisualT = 0f;
        rightVisualT = 0f;
        ApplyTurnBlend(0f, 0f);

        highlight.Clear();       // reset zvýraznenia ako prvé
        SwitchToIdleUI();
    }

    public void EyeCloseStop()
    {
        OnStop();
    }
}
