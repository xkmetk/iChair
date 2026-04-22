using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace PupilLabs
{
    public class GazeZoneJoystickV2 : MonoBehaviour
    {
        [SerializeField] private TMP_Text eyelidSumText;

        [Header("Scene References")]
        [SerializeField] private Transform pointer;
        [SerializeField] private TestBle ble;

        [Header("Arrow UI")]
        [SerializeField] private ArrowStackUI forwardArrows;
        [SerializeField] private ArrowStackUI backwardArrows;
        [SerializeField] private ArrowStackUI leftArrows;
        [SerializeField] private ArrowStackUI rightArrows;

        [Header("Tracking Area")]
        [SerializeField] private float maxDistance = 0.35f;

        [Header("Edge Activation")]
        [SerializeField] private float horizontalActivationEdge = 0.30f;
        [SerializeField] private float verticalActivationEdge = 0.30f;

        [Header("Eye Close Stop")]
        [SerializeField] private float eyeCloseThreshold = 5f;
        [SerializeField] private float eyeCloseStopTime = 0.25f;

        [Header("Test Mode")]
        [SerializeField] private bool testMode = false;
        [SerializeField] private float testModeEyeCloseThreshold = 20f;
        [SerializeField] private Key simulatedEyeCloseKey = Key.B;
        [SerializeField] private float simulatedOpenEyesValue = 20f;
        [SerializeField] private float simulatedClosedEyesValue = 0f;

        [Header("Direct Zone Activation")]
        [SerializeField] private float activationFixationTime = 0.10f;

        [Header("Drive Levels")]
        [SerializeField] private int maxLevel = 3;

        [Header("Step Sizes Per Direction")]
        [SerializeField] private int[] forwardStepSizes = new int[] { 20, 40, 60 };
        [SerializeField] private int[] backwardStepSizes = new int[] { 20, 40, 60 };
        [SerializeField] private int[] leftStepSizes = new int[] { 20, 40, 60 };
        [SerializeField] private int[] rightStepSizes = new int[] { 20, 40, 60 };

        [Header("Fallback Step Size")]
        [SerializeField] private int stepSize = 20;

        [Header("Gaze Loss Stop")]
        [SerializeField] private bool stopWhenGazeLost = true;
        [SerializeField] private float gazeLostStopDelay = 0.2f;

        [Header("Divide/Multiply")]
        [SerializeField] private float divideFactor = 1.25f;

        [Header("Debug")]
        [SerializeField] private bool logZoneChanges = true;
        [SerializeField] private bool logEveryHit = false;
        [SerializeField] private bool logActivation = true;
        [SerializeField] private bool logEyeCloseStop = true;

        private Vector3 defaultPointerPos = Vector3.zero;


        private int forwardLevel = 0;
        private int backwardLevel = 0;
        private int leftLevel = 0;
        private int rightLevel = 0;

        private float lastGazeHitTime = 0f;
        private bool gazeCurrentlyLost = false;
        private float eyeCloseTimer = 0f;

        private const int NeutralXY = 128;

        private GazeDataProvider gazeProvider;

        private enum Zone
        {
            None,
            Left,
            Right,
            Up,
            Down
        }

        private Zone lastLoggedZone = Zone.None;
        private Zone candidateZone = Zone.None;
        private float candidateTimer = 0f;
        private Zone latchedZone = Zone.None;

        private void OnValidate()
        {
            if (maxLevel < 1)
                maxLevel = 1;

            ResizeAndSanitizeArray(ref forwardStepSizes, maxLevel, stepSize);
            ResizeAndSanitizeArray(ref backwardStepSizes, maxLevel, stepSize);
            ResizeAndSanitizeArray(ref leftStepSizes, maxLevel, stepSize);
            ResizeAndSanitizeArray(ref rightStepSizes, maxLevel, stepSize);

            if (gazeLostStopDelay < 0f)
                gazeLostStopDelay = 0f;

            if (eyeCloseStopTime < 0f)
                eyeCloseStopTime = 0f;

            if (eyeCloseThreshold < 0f)
                eyeCloseThreshold = 0f;

            if (testModeEyeCloseThreshold < 0f)
                testModeEyeCloseThreshold = 0f;
        }

        private void ResizeAndSanitizeArray(ref int[] array, int targetSize, int defaultValue)
        {
            if (array == null)
                array = new int[0];

            if (array.Length != targetSize)
            {
                int[] newArray = new int[targetSize];

                for (int i = 0; i < targetSize; i++)
                {
                    if (i < array.Length)
                        newArray[i] = array[i];
                    else
                        newArray[i] = defaultValue;
                }

                array = newArray;
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < 0)
                    array[i] = 0;
            }
        }

        private void Start()
        {
            if (pointer == null)
            {
                Debug.LogError("GazeZoneJoystickV2: Pointer is not assigned.");
                enabled = false;
                return;
            }

            if (ble == null)
            {
                Debug.LogError("GazeZoneJoystickV2: TestBle is not assigned.");
                enabled = false;
                return;
            }

            if (!testMode)
            {
                gazeProvider = ServiceLocator.Instance.GetComponentInChildren<GazeDataProvider>();
                if (gazeProvider == null)
                {
                    Debug.LogError("GazeZoneJoystickV2: GazeDataProvider not found via ServiceLocator.");
                    enabled = false;
                    return;
                }
            }

            defaultPointerPos = pointer.localPosition;
            lastGazeHitTime = Time.time;

            var visualizer = ServiceLocator.Instance.GetComponentInChildren<GazeDataVisualizer>();
            if (visualizer == null)
            {
                Debug.LogError("GazeZoneJoystickV2: GazeDataVisualizer not found.");
                enabled = false;
                return;
            }

            visualizer.onHit.AddListener(OnRaycastHit);

            SendCurrentDriveState();
            UpdateArrowUI();
            Debug.Log($"GazeZoneJoystickV2 initialized. TestMode: {testMode}");
        }

        private void Update()
        {
            HandleEyeCloseStop();
            HandleGazeLostStop();
        }

        private void HandleEyeCloseStop()
        {
            bool simulatedEyesClosed =
                testMode &&
                Keyboard.current != null &&
                Keyboard.current[simulatedEyeCloseKey] != null &&
                Keyboard.current[simulatedEyeCloseKey].isPressed;

            float displayedEyelidSum;
            float activeThreshold;

            if (testMode)
            {
                displayedEyelidSum = simulatedEyesClosed ? simulatedClosedEyesValue : simulatedOpenEyesValue;
                activeThreshold = testModeEyeCloseThreshold;
            }
            else
            {
                Eyelid eyelid = gazeProvider.Eyelid;
                displayedEyelidSum = eyelid.eyelidApertureLeft + eyelid.eyelidApertureRight;
                activeThreshold = eyeCloseThreshold;
            }

            if (eyelidSumText != null)
            {
                eyelidSumText.text = $"EyelidSum: {displayedEyelidSum:F2} | Threshold: {activeThreshold:F2} | TestMode: {testMode}";
            }

            if (displayedEyelidSum < activeThreshold)
            {
                eyeCloseTimer += Time.deltaTime;

                if (eyeCloseTimer >= eyeCloseStopTime)
                {
                    if (logEyeCloseStop)
                    {
                        Debug.Log($"EYE CLOSE STOP | eyelidSum: {displayedEyelidSum:F2} | threshold: {activeThreshold:F2} | timer: {eyeCloseTimer:F2} | testMode: {testMode}");
                    }

                    StopMovement();
                    eyeCloseTimer = 0f;
                }
            }
            else
            {
                eyeCloseTimer = 0f;
            }
        }

        private void HandleGazeLostStop()
        {
            if (!stopWhenGazeLost)
                return;

            if (Time.time - lastGazeHitTime >= gazeLostStopDelay)
            {
                if (!gazeCurrentlyLost)
                {
                    gazeCurrentlyLost = true;
                    StopMovement();
                    Debug.Log("STOP EXECUTED: Gaze lost.");
                }
            }
        }

        private void UpdateArrowUI()
        {
            if (forwardArrows != null) forwardArrows.SetLevel(forwardLevel);
            if (backwardArrows != null) backwardArrows.SetLevel(backwardLevel);
            if (leftArrows != null) leftArrows.SetLevel(leftLevel);
            if (rightArrows != null) rightArrows.SetLevel(rightLevel);
        }

        public void OnRaycastHit(Vector3 hitPoint)
        {
            lastGazeHitTime = Time.time;

            if (gazeCurrentlyLost)
            {
                gazeCurrentlyLost = false;
            }

            Vector3 targetPos = transform.InverseTransformPoint(hitPoint);
            targetPos.z = defaultPointerPos.z;

            Vector3 offset = targetPos - defaultPointerPos;
            float distance = offset.magnitude;

            if (distance > maxDistance)
            {
                offset = offset.normalized * maxDistance;
                targetPos = defaultPointerPos + offset;
                targetPos.z = defaultPointerPos.z;
            }

            pointer.localPosition = targetPos;

            Zone zone = GetZone(pointer.localPosition);

            if (logEveryHit)
            {
                Debug.Log($"HIT | localPos: {pointer.localPosition} | zone: {zone}");
            }

            if (logZoneChanges && zone != lastLoggedZone)
            {
                Debug.Log($"ZONE: {zone} | localPos: {pointer.localPosition}");
                lastLoggedZone = zone;
            }

            ProcessDirectActivation(zone);
        }

        private Zone GetZone(Vector3 localPos)
        {
            float x = localPos.x;
            float y = localPos.y;

            if (x <= -horizontalActivationEdge) return Zone.Left;
            if (x >= horizontalActivationEdge) return Zone.Right;
            if (y >= verticalActivationEdge) return Zone.Up;
            if (y <= -verticalActivationEdge) return Zone.Down;

            return Zone.None;
        }

        private void ProcessDirectActivation(Zone zone)
        {
            bool isDirectionZone =
                zone == Zone.Up ||
                zone == Zone.Down ||
                zone == Zone.Left ||
                zone == Zone.Right;

            if (latchedZone != Zone.None)
            {
                if (zone != latchedZone)
                {
                    if (logActivation)
                    {
                        Debug.Log($"Activation reset: left latched zone {latchedZone}.");
                    }

                    latchedZone = Zone.None;
                    candidateZone = Zone.None;
                    candidateTimer = 0f;
                }

                return;
            }

            if (!isDirectionZone)
            {
                candidateZone = Zone.None;
                candidateTimer = 0f;
                return;
            }

            if (zone != candidateZone)
            {
                candidateZone = zone;
                candidateTimer = 0f;
                return;
            }

            candidateTimer += Time.deltaTime;

            if (candidateTimer >= activationFixationTime)
            {
                if (logActivation)
                {
                    Debug.Log($"DIRECT ACTIVATION: {zone}");
                }

                TriggerGesture(zone);

                latchedZone = zone;
                candidateZone = Zone.None;
                candidateTimer = 0f;
            }
        }

        private void TriggerGesture(Zone direction)
        {
            switch (direction)
            {
                case Zone.Up:
                    if (backwardLevel > 0)
                    {
                        backwardLevel--;
                    }
                    else
                    {
                        forwardLevel = Mathf.Min(forwardLevel + 1, maxLevel);
                    }
                    break;

                case Zone.Down:
                    if (forwardLevel > 0)
                    {
                        forwardLevel--;
                    }
                    else
                    {
                        backwardLevel = Mathf.Min(backwardLevel + 1, maxLevel);
                    }
                    break;

                case Zone.Left:
                    if (rightLevel > 0)
                    {
                        rightLevel--;
                    }
                    else
                    {
                        leftLevel = Mathf.Min(leftLevel + 1, maxLevel);
                    }
                    break;

                case Zone.Right:
                    if (leftLevel > 0)
                    {
                        leftLevel--;
                    }
                    else
                    {
                        rightLevel = Mathf.Min(rightLevel + 1, maxLevel);
                    }
                    break;

                default:
                    return;
            }

            Debug.Log($"COMMAND EXECUTED: {direction} | Forward: {forwardLevel}, Backward: {backwardLevel}, Left: {leftLevel}, Right: {rightLevel}");

            SendCurrentDriveState();
            UpdateArrowUI();
        }

        private void StopMovement()
        {
            forwardLevel = 0;
            backwardLevel = 0;
            leftLevel = 0;
            rightLevel = 0;

            latchedZone = Zone.None;
            candidateZone = Zone.None;
            candidateTimer = 0f;

            SendCurrentDriveState();
            UpdateArrowUI();

            Debug.Log("STOP EXECUTED");
        }

        private int GetStepValue(int level, int[] stepArray)
        {
            if (level <= 0 || stepArray == null || stepArray.Length == 0)
                return 0;

            int index = level - 1;

            if (index < 0 || index >= stepArray.Length)
                return 0;

            return stepArray[index];
        }

        private void SendCurrentDriveState()
        {
            int rightValue = GetStepValue(rightLevel, rightStepSizes);
            int leftValue = GetStepValue(leftLevel, leftStepSizes);
            int forwardValue = GetStepValue(forwardLevel, forwardStepSizes);
            int backwardValue = GetStepValue(backwardLevel, backwardStepSizes);

            float x = rightValue - leftValue;
            float y = forwardValue - backwardValue;

            if (x != 0 && y != 0)
            {
                float sideReduction = Mathf.Abs(x) / 2f;

                if (y > 0)
                {
                    y = Mathf.Max(0f, y - sideReduction);
                }
                else if (y < 0)
                {
                    y = Mathf.Min(0f, y + sideReduction);
                }

                x /= divideFactor;
            }
            else if (x != 0 && y == 0)
            {
                x *= divideFactor;
            }

            int finalX = Mathf.Clamp(Mathf.RoundToInt(x) + NeutralXY, 0, 255);
            int finalY = Mathf.Clamp(Mathf.RoundToInt(y) + NeutralXY, 0, 255);

            ble.SetXY(finalX, finalY);
            Debug.Log($"BLE SEND | x: {finalX}, y: {finalY}");
        }
    }
}
