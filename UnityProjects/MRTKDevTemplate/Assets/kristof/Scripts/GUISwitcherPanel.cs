using PupilLabs;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GUISwitcherPanel : MonoBehaviour
{
    [System.Serializable]
    public class GUIOption
    {
        public GameObject gazeButton;
        public GameObject guiObject;

        [Header("Object that contains EyeCloseStopper scripts")]
        public GameObject eyeCloseObject;
    }

    [SerializeField] private List<GUIOption> options = new List<GUIOption>();

    [Header("BLE controller")]
    [SerializeField] private BleVehicleController bleVehicleController;

    [Header("Gaze Settings")]
    [SerializeField] private float activationTime = 1.5f;

    [Header("Eye Close Switcher")]
    [SerializeField] private TestBle ble;
    [SerializeField] private float stopAfterSeconds = 1f;
    [SerializeField] private float switchAfterSeconds = 5f;
    [SerializeField] private float eyeCloseThreshold = 5f;

    [Header("Test Mode")]
    [SerializeField] private bool testMode = false;
    [SerializeField] private float testModeEyeCloseThreshold = 20f;
    [SerializeField] private Key simulatedEyeCloseKey = Key.B;
    [SerializeField] private float simulatedOpenEyesValue = 20f;
    [SerializeField] private float simulatedClosedEyesValue = 0f;

    private GazeDataProvider gazeProvider;
    private float eyeCloseTimer = 0f;
    private bool stopSent = false;

    private GameObject currentCandidate = null;
    private float gazeTimer = 0f;

    private GameObject eyeCloseRunner;

    private void Start()
    {
        ServiceLocator.Instance
            .GetComponentInChildren<GazeDataVisualizer>()
            .onHit.AddListener(OnRaycastHit);

        if (!testMode)
        {
            gazeProvider = ServiceLocator.Instance
                .GetComponentInChildren<GazeDataProvider>();

            if (gazeProvider == null)
            {
                Debug.LogError("GUISwitcherPanel: GazeDataProvider not found.");
                enabled = false;
                return;
            }
        }

        eyeCloseRunner = new GameObject("EyeCloseRunner");
        DontDestroyOnLoad(eyeCloseRunner);

        var runner = eyeCloseRunner.AddComponent<EyeCloseRunner>();
        runner.Init(this);

        Debug.Log("GUISwitcherPanel: Start OK");
    }

    public void TickEyeClose()
    {
        bool simulatedEyesClosed =
            testMode &&
            Keyboard.current != null &&
            Keyboard.current[simulatedEyeCloseKey].isPressed;

        float eyelidSum;
        float activeThreshold;

        if (testMode)
        {
            eyelidSum = simulatedEyesClosed ? simulatedClosedEyesValue : simulatedOpenEyesValue;
            activeThreshold = testModeEyeCloseThreshold;
        }
        else
        {
            Eyelid eyelid = gazeProvider.Eyelid;
            eyelidSum = eyelid.eyelidApertureLeft + eyelid.eyelidApertureRight;
            activeThreshold = eyeCloseThreshold;
        }

        if (eyelidSum < activeThreshold)
        {
            eyeCloseTimer += Time.deltaTime;

            if (eyeCloseTimer >= stopAfterSeconds && !stopSent)
            {
                bool handled = false;

                foreach (var option in options)
                {
                    if (option.guiObject != null && option.guiObject.activeSelf)
                    {
                        if (option.eyeCloseObject != null)
                        {
                            var stoppers = option.eyeCloseObject.GetComponents<IEyeCloseStopper>();

                            foreach (var stopper in stoppers)
                            {
                                stopper.EyeCloseStop();
                                handled = true;
                            }
                        }
                    }
                }

                if (!handled && bleVehicleController != null)
                    bleVehicleController.StopAll();

                stopSent = true;
            }

            if (eyeCloseTimer >= switchAfterSeconds)
            {
                OpenSwitcher();
                eyeCloseTimer = 0f;
                stopSent = false;
            }
        }
        else
        {
            eyeCloseTimer = 0f;
            stopSent = false;
        }
    }

    private void OpenSwitcher()
    {
        foreach (var option in options)
        {
            if (option.guiObject != null)
                option.guiObject.SetActive(false);
        }

        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        if (currentCandidate == null)
        {
            gazeTimer = 0f;
            return;
        }

        gazeTimer += Time.deltaTime;

        if (gazeTimer >= activationTime)
        {
            HandleActivation(currentCandidate);
            gazeTimer = 0f;
            currentCandidate = null;
        }
    }

    private void OnRaycastHit(Vector3 hitPoint)
    {
        foreach (var option in options)
        {
            if (option.gazeButton != null && IsHitting(hitPoint, option.gazeButton))
            {
                if (currentCandidate != option.gazeButton)
                {
                    currentCandidate = option.gazeButton;
                    gazeTimer = 0f;
                }
                return;
            }
        }

        currentCandidate = null;
        gazeTimer = 0f;
    }

    private bool IsHitting(Vector3 hitPoint, GameObject target)
    {
        Collider col = target.GetComponent<Collider>();
        if (col != null)
            return col.bounds.Contains(hitPoint);

        Collider2D col2d = target.GetComponent<Collider2D>();
        if (col2d != null)
            return col2d.OverlapPoint(hitPoint);

        return false;
    }

    private void HandleActivation(GameObject candidate)
    {
        foreach (var option in options)
        {
            if (option.gazeButton == candidate)
            {
                ActivateGUI(option);
                return;
            }
        }
    }

    private void ActivateGUI(GUIOption selected)
    {
        foreach (var option in options)
        {
            if (option.guiObject != null)
                option.guiObject.SetActive(option == selected);
        }

        gameObject.SetActive(false);
    }

    public void SelectOption(int index)
    {
        if (index < 0 || index >= options.Count) return;

        ActivateGUI(options[index]);
    }
}

public class EyeCloseRunner : MonoBehaviour
{
    private GUISwitcherPanel panel;

    public void Init(GUISwitcherPanel p)
    {
        panel = p;
    }

    private void Update()
    {
        if (panel != null)
            panel.TickEyeClose();
    }
}
