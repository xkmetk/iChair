using UnityEngine;

namespace PupilLabs
{
    public class GazeGestureJoystick: MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform pointer;
        [SerializeField] private TestBle ble;

        [Header("Zone Highlights")]
        [SerializeField] private ZoneHighlightFade highlightLeft;
        [SerializeField] private ZoneHighlightFade highlightRight;
        [SerializeField] private ZoneHighlightFade highlightUp;
        [SerializeField] private ZoneHighlightFade highlightDown;
        [SerializeField] private ZoneHighlightFade highlightStopLeft;
        [SerializeField] private ZoneHighlightFade highlightStopRight;

        [Header("Arrow UI")]
        [SerializeField] private ArrowStackUI forwardArrows;
        [SerializeField] private ArrowStackUI backwardArrows;
        [SerializeField] private ArrowStackUI leftArrows;
        [SerializeField] private ArrowStackUI rightArrows;

        [Header("Tracking Area")]
        [SerializeField] private float maxDistance = 0.35f;

        [Header("Center Square")]
        [SerializeField] private float halfWidth = 0.20f;
        [SerializeField] private float halfHeight = 0.20f;

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
            Center,
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

        // Fixation / activation
        private Zone candidateZone = Zone.None;
        private float candidateTimer = 0f;

        // Latched zone = zóna, ktorá už bola aktivovaná a čakáme,
        // kým ju používateľ opustí, aby sa mohlo aktivovať niečo ďalšie.
        private Zone latchedZone = Zone.None;

        private void Start()
        {
            if (pointer == null)
            {
                Debug.LogError("GazeGestureJoystick: Pointer is not assigned.");
                enabled = false;
                return;
            }

            if (ble == null)
            {
                Debug.LogError("GazeGestureJoystick: TestBle is not assigned.");
                enabled = false;
                return;
            }

            defaultPointerPos = pointer.localPosition;

            var visualizer = ServiceLocator.Instance.GetComponentInChildren<GazeDataVisualizer>();
            if (visualizer == null)
            {
                Debug.LogError("GazeGestureJoystick: GazeDataVisualizer not found.");
                enabled = false;
                return;
            }

            visualizer.onHit.AddListener(OnRaycastHit);

            SendCurrentDriveState();
            UpdateArrowUI();
            Debug.Log("GazeGestureJoystick initialized.");
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

        private void UpdateHighlights(Zone zone)
        {
            if (highlightLeft != null) highlightLeft.SetHighlighted(zone == Zone.Left);
            if (highlightRight != null) highlightRight.SetHighlighted(zone == Zone.Right);
            if (highlightUp != null) highlightUp.SetHighlighted(zone == Zone.Up);
            if (highlightDown != null) highlightDown.SetHighlighted(zone == Zone.Down);
            if (highlightStopLeft != null) highlightStopLeft.SetHighlighted(zone == Zone.StopLeft);
            if (highlightStopRight != null) highlightStopRight.SetHighlighted(zone == Zone.StopRight);
        }

        private void ClearHighlights()
        {
            if (highlightLeft != null) highlightLeft.SetHighlighted(false);
            if (highlightRight != null) highlightRight.SetHighlighted(false);
            if (highlightUp != null) highlightUp.SetHighlighted(false);
            if (highlightDown != null) highlightDown.SetHighlighted(false);
            if (highlightStopLeft != null) highlightStopLeft.SetHighlighted(false);
            if (highlightStopRight != null) highlightStopRight.SetHighlighted(false);
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
            Vector3 targetPos = transform.InverseTransformPoint(hitPoint);
            targetPos.z = defaultPointerPos.z;

            float distance = Vector3.Distance(targetPos, defaultPointerPos);

            if (distance > maxDistance)
            {
                return;
            }

            pointer.localPosition = targetPos;

            Zone zone = GetZone(pointer.localPosition);

            UpdateHighlights(zone);

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

            bool insideCenterX = Mathf.Abs(x) <= halfWidth;
            bool insideCenterY = Mathf.Abs(y) <= halfHeight;

            if (insideCenterX && insideCenterY)
            {
                return Zone.Center;
            }

            if (x < -halfWidth) return Zone.Left;
            if (x > halfWidth) return Zone.Right;
            if (y > halfHeight) return Zone.Up;
            if (y < -halfHeight) return Zone.Down;

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
            // reagujeme iba na smerové zóny
            bool isDirectionZone =
                zone == Zone.Up ||
                zone == Zone.Down ||
                zone == Zone.Left ||
                zone == Zone.Right;

            // Ak už bola nejaká zóna aktivovaná, čakáme iba na jej opustenie.
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

            // mimo smerových zón len reset kandidáta
            if (!isDirectionZone)
            {
                candidateZone = Zone.None;
                candidateTimer = 0f;
                return;
            }

            // nová kandidátska zóna
            if (zone != candidateZone)
            {
                candidateZone = zone;
                candidateTimer = 0f;
                return;
            }

            // tá istá zóna sa drží -> načítavame fixáciu
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
            // Ak sa pozerá mimo latched zóny, povolíme ďalšiu aktiváciu.
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

            Debug.Log(
                $"COMMAND EXECUTED: {direction} | Forward: {forwardLevel}, Backward: {backwardLevel}, Left: {leftLevel}, Right: {rightLevel}"
            );

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
