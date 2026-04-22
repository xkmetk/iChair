using System.Collections.Generic;
using UnityEngine;

public class VectorArrowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform rotationRoot;
    [SerializeField] private Transform startOffsetRoot;
    [SerializeField] private Transform bodyRoot;
    [SerializeField] private Transform arrowHead;
    [SerializeField] private GameObject squarePrefab;

    [Header("Layout")]
    [SerializeField] private int initialCapacity = 5;
    [SerializeField] private float stepSpacing = 0.3f;
    [SerializeField] private float startOffset = 0.4f;
    [SerializeField] private float arrowHeadExtraOffset = 0f;

    [Header("Scaling")]
    [SerializeField] private float scaleFalloff = 0.06f;
    [SerializeField] private float minScaleMultiplier = 0.55f;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenZero = true;

    private readonly List<Transform> squares = new List<Transform>();
    private readonly List<Vector3> baseScales = new List<Vector3>();

    private void Awake()
    {
        if (rotationRoot == null)
            rotationRoot = transform;

        if (startOffsetRoot != null)
            startOffsetRoot.localPosition = new Vector3(0f, startOffset, 0f);

        EnsureCapacity(initialCapacity);
        ApplyVisuals(0, 0f);
    }

    public void SetVector(int longitudinalLevel, int lateralLevel, int maxLongitudinalLevel, int maxLateralLevel)
    {
        int visibleCount = GetVisibleCount(longitudinalLevel, lateralLevel);
        float angle = GetAngle(longitudinalLevel, lateralLevel, maxLongitudinalLevel, maxLateralLevel);

        EnsureCapacity(visibleCount);
        ApplyVisuals(visibleCount, angle);
    }

    private int GetVisibleCount(int longitudinalLevel, int lateralLevel)
    {
        if (longitudinalLevel != 0)
            return Mathf.Abs(longitudinalLevel);

        return Mathf.Abs(lateralLevel);
    }

    private float GetAngle(int longitudinalLevel, int lateralLevel, int maxLongitudinalLevel, int maxLateralLevel)
    {
        if (longitudinalLevel == 0 && lateralLevel == 0)
            return 0f;

        float x;
        float y;

        if (longitudinalLevel == 0)
        {
            x = Mathf.Sign(lateralLevel);
            y = 0f;
        }
        else if (lateralLevel == 0)
        {
            x = 0f;
            y = Mathf.Sign(longitudinalLevel);
        }
        else
        {
            float safeMaxLong = Mathf.Max(1, maxLongitudinalLevel);
            float safeMaxLat = Mathf.Max(1, maxLateralLevel);

            x = (float)lateralLevel / safeMaxLat;
            y = (float)longitudinalLevel / safeMaxLong;
        }

        Vector2 dir = new Vector2(x, y).normalized;

        // 0° = hore
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
    }

    private void EnsureCapacity(int count)
    {
        if (squarePrefab == null || bodyRoot == null)
            return;

        while (squares.Count < count)
        {
            GameObject go = Instantiate(squarePrefab, bodyRoot);
            go.name = $"VectorSquare_{squares.Count}";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            squares.Add(go.transform);
            baseScales.Add(go.transform.localScale);
        }
    }

    private void ApplyVisuals(int visibleCount, float angle)
    {
        bool showArrow = visibleCount > 0 || !hideWhenZero;

        if (rotationRoot != null)
            rotationRoot.gameObject.SetActive(showArrow);

        if (!showArrow)
            return;

        if (rotationRoot != null)
            rotationRoot.localEulerAngles = new Vector3(0f, 0f, angle);

        if (startOffsetRoot != null)
            startOffsetRoot.localPosition = new Vector3(0f, startOffset, 0f);

        Transform lastActiveSegment = null;

        for (int i = 0; i < squares.Count; i++)
        {
            bool active = i < visibleCount;

            if (squares[i] != null)
                squares[i].gameObject.SetActive(active);

            if (!active || squares[i] == null)
                continue;

            squares[i].localPosition = new Vector3(0f, stepSpacing * i, 0f);

            float scaleMultiplier = 1f - i * scaleFalloff;
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, minScaleMultiplier, 1f);

            // zachová veľkosť z prefabu
            squares[i].localScale = baseScales[i] * scaleMultiplier;

            lastActiveSegment = squares[i];
        }

        if (arrowHead != null)
        {
            arrowHead.gameObject.SetActive(showArrow);

            if (lastActiveSegment != null)
            {
                Vector3 lastPos = lastActiveSegment.localPosition;
                arrowHead.localPosition = new Vector3(
                    lastPos.x,
                    lastPos.y + stepSpacing + arrowHeadExtraOffset,
                    lastPos.z
                );
            }
            else
            {
                arrowHead.localPosition = Vector3.zero;
            }
        }
    }
}
