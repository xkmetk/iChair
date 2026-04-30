using UnityEngine;

namespace PupilLabs
{
    public class Joystick : MonoBehaviour, IEyeCloseStopper
    {

        [SerializeField]
        private float outOfZoneResetTime = 1f;

        private float outOfZoneTimer = 0f;
        private bool isOutOfZone = false;

        [SerializeField]
        private TestBle ble;
        [SerializeField]
        private Transform pointer;
        [SerializeField]
        private float maxDistance = 0.25f;
        [SerializeField]
        private float maxEffectiveDistance = 0.2f;

        private Vector3 defaultPointerPos = Vector3.zero;
        private bool wasHit = false;

        public bool FollowGaze { get; set; } = false;

        private void Start()
        {
            defaultPointerPos = pointer.localPosition;
            ServiceLocator.Instance.GetComponentInChildren<GazeDataVisualizer>().onHit.AddListener(OnRaycastHit);
        }

        private void SendData(Vector2 data)
        {
            Vector2 origin = defaultPointerPos;
            Vector2 clamped = Vector2.ClampMagnitude(data - origin, maxEffectiveDistance);

            int x = Mathf.RoundToInt((clamped.x / maxEffectiveDistance / 2f + 0.5f) * 140);
            int y = Mathf.RoundToInt((clamped.y / maxEffectiveDistance / 2f + 0.5f) * 140);

            x += 58;
            y += 58;

            ble.SetXY(x, y);
            Debug.Log($"x: {x}, y:{y}");
        }

        private void Update()
        {
            SendData(pointer.localPosition);

            float dist = Vector3.Distance(pointer.localPosition, defaultPointerPos);
            bool outOfZone = !wasHit || dist > maxDistance;

            if (outOfZone)
            {
                if (!isOutOfZone)
                {
                    isOutOfZone = true;
                    outOfZoneTimer = 0f;
                }

                outOfZoneTimer += Time.deltaTime;

                if (outOfZoneTimer >= outOfZoneResetTime)
                {
                    FollowGaze = false;
                    pointer.localPosition = defaultPointerPos;
                    isOutOfZone = false;
                    outOfZoneTimer = 0f;
                }
            }
            else
            {
                isOutOfZone = false;
                outOfZoneTimer = 0f;
                // FollowGaze necháme zapnutý kým sme v zóne a máme hit
            }

            pointer.localPosition = defaultPointerPos;
            wasHit = false;
        }

        public void OnRaycastHit(Vector3 hitPoint)
        {
            if (FollowGaze)
            {
                Vector3 targetPos = transform.InverseTransformPoint(hitPoint);
                targetPos.z = defaultPointerPos.z;

                // Ak je mimo maxDistance, zaklampuj pointer na okraj kruhu
                // – timer v Update rozhodne, či sa to resetne po 1 sekunde
                if (Vector3.Distance(targetPos, defaultPointerPos) > maxDistance)
                {
                    Vector3 dir = (targetPos - defaultPointerPos).normalized;
                    targetPos = defaultPointerPos + dir * maxDistance;
                }

                pointer.localPosition = targetPos;
            }
            wasHit = true;
        }

        public void EyeCloseStop()
        {
            FollowGaze = false;
            pointer.localPosition = defaultPointerPos;
            isOutOfZone = false;
            outOfZoneTimer = 0f;

            // pošli neutrál (128, 128) do vozíka
            ble.SetXY(128, 128);
        }
    }
}
