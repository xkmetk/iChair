using UnityEngine;

namespace PupilLabs
{
    public class GazeZoneJoystickV3 : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform pointer;
        [SerializeField] private TestBle ble;

        [Header("Stop Highlights")]
        [SerializeField] private ZoneHighlightFade highlightStopLeft;
        [SerializeField] private ZoneHighlightFade highlightStopRight;

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

        [Header("Stop Zones")]
        [SerializeField] private Vector2 stopLeftCenter = new Vector2(-0.14f, 0.14f);
        [SerializeField] private Vector2 stopRightCenter = new Vector2(0.14f, 0.14f);
        [SerializeField] private Vector2 stopZoneSize = new Vector2(0.08f, 0.08f);
        [SerializeField] private float stopSequenceTimeout = 0.8f;

        [Header("Direct Zone Activation")]
        [SerializeField] private float activationFixationTime = 0.10f;

        [Header("Drive Levels")]
        [SerializeField] private int maxLevel = 3;
        [SerializeField] private int stepSize = 20;

        [Header("Debug")]
        [SerializeField] private bool logZoneChanges = true;
        [SerializeField] private bool logEveryHit = false;
        [SerializeField] private bool logStopSequence = true;
        [SerializeField] private bool logActivation = true;

        private Vector3 defaultPointerPos = Vector3.zero;

        private int forwardLevel = 0;
        private int backwardLevel = 0;
        private int leftLevel = 0;
        private int rightLevel = 0;

        private const int NeutralXY = 128;

        private enum Zone
        {
            None,
            Left,
            Right,
            Up,
            Down,
            StopLeft,
            StopRight
        }

        private enum StopState
        {
            Idle,
            WaitingForSecondButton
        }

        private StopState stopState = StopState.Idle;
        private float stopTimer = 0f;

        private Zone lastLoggedZone = Zone.None;

        private Zone candidateZone = Zone.None;
        private float candidateTimer = 0f;

        private Zone latchedZone = Zone.None;

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

            defaultPointerPos = pointer.localPosition;

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
            Debug.Log("GazeZoneJoystickV2 initialized.");
        }

        private void Update()
        {
            if (stopState == StopState.WaitingForSecondButton)
            {
                stopTimer += Time.deltaTime;

                if (stopTimer > stopSequenceTimeout)
                {
                    if (logStopSequence)
                    {
                        Debug.Log("STOP sequence cancelled: timeout.");
                    }

                    ResetStopSequence();
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

        private void UpdateStopHighlights(Zone zone)
        {
            if (highlightStopLeft != null)
                highlightStopLeft.SetHighlighted(zone == Zone.StopLeft);

            if (highlightStopRight != null)
                highlightStopRight.SetHighlighted(zone == Zone.StopRight);
        }

        public void OnRaycastHit(Vector3 hitPoint)
        {
            Vector3 targetPos = transform.InverseTransformPoint(hitPoint);
            targetPos.z = defaultPointerPos.z;

            Vector3 offset = targetPos - defaultPointerPos;
            float distance = offset.magnitude;

            // Namiesto ignorovania pointu mimo tracking area ho orežeme na hranicu.
            if (distance > maxDistance)
            {
                offset = offset.normalized * maxDistance;
                targetPos = defaultPointerPos + offset;
                targetPos.z = defaultPointerPos.z;
            }

            pointer.localPosition = targetPos;

            Zone zone = GetZone(pointer.localPosition);

            UpdateStopHighlights(zone);

            if (logEveryHit)
            {
                Debug.Log($"HIT | localPos: {pointer.localPosition} | zone: {zone}");
            }

            if (logZoneChanges && zone != lastLoggedZone)
            {
                Debug.Log($"ZONE: {zone} | localPos: {pointer.localPosition}");
                lastLoggedZone = zone;
            }

            if (HandleStopSequence(zone))
            {
                ResetActivationStateIfNeeded(zone);
                return;
            }

            ProcessDirectActivation(zone);
        }

        private Zone GetZone(Vector3 localPos)
        {
            float x = localPos.x;
            float y = localPos.y;

            if (IsInsideStopZone(localPos, stopLeftCenter))
                return Zone.StopLeft;

            if (IsInsideStopZone(localPos, stopRightCenter))
                return Zone.StopRight;

            if (x <= -horizontalActivationEdge) return Zone.Left;
            if (x >= horizontalActivationEdge) return Zone.Right;
            if (y >= verticalActivationEdge) return Zone.Up;
            if (y <= -verticalActivationEdge) return Zone.Down;

            return Zone.None;
        }

        private bool IsInsideStopZone(Vector3 localPos, Vector2 zoneCenter)
        {
            float halfZoneWidth = stopZoneSize.x * 0.5f;
            float halfZoneHeight = stopZoneSize.y * 0.5f;

            return Mathf.Abs(localPos.x - zoneCenter.x) <= halfZoneWidth &&
                   Mathf.Abs(localPos.y - zoneCenter.y) <= halfZoneHeight;
        }

        private bool HandleStopSequence(Zone zone)
        {
            switch (stopState)
            {
                case StopState.Idle:
                    if (zone == Zone.StopLeft)
                    {
                        stopState = StopState.WaitingForSecondButton;
                        stopTimer = 0f;

                        if (logStopSequence)
                        {
                            Debug.Log("STOP sequence started: StopLeft hit.");
                        }

                        return true;
                    }
                    break;

                case StopState.WaitingForSecondButton:
                    if (zone == Zone.StopRight)
                    {
                        if (logStopSequence)
                        {
                            Debug.Log("STOP sequence completed: StopRight hit.");
                        }

                        StopMovement();
                        ResetStopSequence();
                        return true;
                    }

                    return true;
            }

            return false;
        }

        private void ResetStopSequence()
        {
            stopState = StopState.Idle;
            stopTimer = 0f;
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

        private void ResetActivationStateIfNeeded(Zone zone)
        {
            if (latchedZone != Zone.None && zone != latchedZone)
            {
                latchedZone = Zone.None;
            }

            candidateZone = Zone.None;
            candidateTimer = 0f;
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

        private void SendCurrentDriveState()
        {
            int x = NeutralXY + (rightLevel * stepSize) - (leftLevel * stepSize);
            int y = NeutralXY + (forwardLevel * stepSize) - (backwardLevel * stepSize);

            x = Mathf.Clamp(x, 0, 255);
            y = Mathf.Clamp(y, 0, 255);

            ble.SetXY(x, y);

            Debug.Log($"BLE SEND | x: {x}, y: {y}");
        }
    }
}
