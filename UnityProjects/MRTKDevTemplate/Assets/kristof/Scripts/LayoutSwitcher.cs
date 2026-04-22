using UnityEngine;

public class LayoutSwitcher : MonoBehaviour
{
    public enum LayoutMode
    {
        Forward,
        ReverseNoCamera,
        ReverseWithCamera
    }

    [System.Serializable]
    public class LayoutItem
    {
        public Transform button;             // samotné tlačidlo
        public Transform forwardAnchor;      // pozícia v forward móde
        public Transform reverseNoCamAnchor; // pozícia v reverse móde bez kamery
        public Transform reverseCamAnchor;   // pozícia v reverse móde s kamerou
    }

    public LayoutItem[] items;

    public void ApplyLayout(LayoutMode mode)
    {
        foreach (var item in items)
        {
            if (item.button == null) continue;

            Transform target = null;

            switch (mode)
            {
                case LayoutMode.Forward:
                    target = item.forwardAnchor;
                    break;

                case LayoutMode.ReverseNoCamera:
                    target = item.reverseNoCamAnchor;
                    break;

                case LayoutMode.ReverseWithCamera:
                    target = item.reverseCamAnchor;
                    break;
            }

            if (target == null) continue;

            item.button.position = target.position;
            item.button.rotation = target.rotation;
        }
    }

    // pre pohodlie malé helpery
    public void SetForwardLayout()        => ApplyLayout(LayoutMode.Forward);
    public void SetReverseNoCameraLayout() => ApplyLayout(LayoutMode.ReverseNoCamera);
    public void SetReverseCameraLayout()  => ApplyLayout(LayoutMode.ReverseWithCamera);
}
