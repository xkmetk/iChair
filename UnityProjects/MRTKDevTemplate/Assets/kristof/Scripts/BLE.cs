using UnityEngine;

namespace PupilLabs
{
    public class BLE : MonoBehaviour
    {
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

            //transform to 0..255
            int x = Mathf.RoundToInt((clamped.x / maxEffectiveDistance / 2f + 0.5f) * 140); //left right+
            int y = Mathf.RoundToInt((clamped.y / maxEffectiveDistance / 2f + 0.5f) * 140); //forward+ reverse

            x += 58;
            y += 58;

            ble.SetXY(x, y);
            //Debug.Log($"x: {x}, y:{y}");
        }

        private void Update() //this will reset wasHit in the end, but wasHit info is from previous frame which should be OK
        {
            //send actual values
            SendData(pointer.localPosition);

            //if not hit previous frame stop follow
            FollowGaze &= wasHit;
            //OnRaycastHit happens during late update
            //reset prior
            pointer.localPosition = defaultPointerPos;
            wasHit = false;
        }

        public void OnRaycastHit(Vector3 hitPoint) //this will be triggered during LateUpdate, so after we reset wasHit
        {
            if (FollowGaze)
            {
                Vector3 targetPos = transform.InverseTransformPoint(hitPoint);
                targetPos.z = defaultPointerPos.z;
                pointer.localPosition = targetPos;

                if (Vector3.Distance(targetPos, defaultPointerPos) > maxDistance) //collider can be bigger
                {
                    targetPos = defaultPointerPos;
                    FollowGaze = false;
                }
            }
            wasHit = true;
        }
    }
}
