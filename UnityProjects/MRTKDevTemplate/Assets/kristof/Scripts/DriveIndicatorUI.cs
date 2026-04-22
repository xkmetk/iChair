using System.Collections.Generic;
using UnityEngine;

public class DriveIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform rotationRoot;
    [SerializeField] private RectTransform arrowHead;
    [SerializeField] private RectTransform segmentContainer;
    [SerializeField] private GameObject segmentPrefab;

    [Header("Segments")]
    [SerializeField] private int maxSegments = 4;
    [SerializeField] private float segmentSpacing = 8f;
    [SerializeField] private Vector2 firstSegmentLocalPosition = new Vector2(0f, 8f);
    [SerializeField] private float arrowHeadOffsetAboveLastSegment = 8f;

    [Header("Display Scaling")]
    [SerializeField] private float displayMaxMagnitude = 60f;
    [SerializeField] private float deadzoneMagnitude = 1f;

    [Header("Rotation")]
    [SerializeField] private float angleOffset = 0f;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private float rotationLerpSpeed = 12f;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenIdle = true;
    [SerializeField] private GameObject wholeIndicator;

    private readonly List<GameObject> segments = new List<GameObject>();

    private float currentAngle;
    private float targetAngle;

    private void Awake()
    {
        RebuildSegments();
        ApplyVectorImmediate(Vector2.zero);
    }

    private void Update()
    {
        if (rotationRoot == null)
            return;

        if (smoothRotation)
        {
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * rotationLerpSpeed);
            rotationRoot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }
    }

    // ===============================
    // PUBLIC API
    // ===============================

    public void SetSignedVector(float x, float y)
    {
        ApplyVector(new Vector2(x, y), false);
    }

    public void ApplyVectorImmediate(Vector2 v)
    {
        ApplyVector(v, true);
    }

    // ===============================
    // CORE LOGIC
    // ===============================

    private void ApplyVector(Vector2 vector, bool immediate)
    {
        float magnitude = vector.magnitude;
        bool isIdle = magnitude <= deadzoneMagnitude;

        float normalized = Mathf.Clamp01(magnitude / displayMaxMagnitude);

        int visibleSegments = isIdle
            ? 0
            : Mathf.Clamp(Mathf.CeilToInt(normalized * maxSegments), 1, maxSegments);

        // ROTATION (vector-based)
        if (!isIdle)
        {
            targetAngle = -Mathf.Atan2(vector.x, vector.y) * Mathf.Rad2Deg + angleOffset;
        }

        if (immediate || !smoothRotation)
        {
            currentAngle = targetAngle;
            if (rotationRoot != null)
                rotationRoot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }

        UpdateSegments(visibleSegments);
        UpdateArrowHead(visibleSegments);
        UpdateVisibility(isIdle);
    }

    // ===============================
    // SEGMENTS
    // ===============================

    public void RebuildSegments()
    {
        foreach (var s in segments)
        {
            if (Application.isPlaying) Destroy(s);
            else DestroyImmediate(s);
        }

        segments.Clear();

        for (int i = 0; i < maxSegments; i++)
        {
            var seg = Instantiate(segmentPrefab, segmentContainer);
            seg.name = $"Segment_{i + 1}";

            var rt = seg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            rt.anchoredPosition = firstSegmentLocalPosition + new Vector2(0, segmentSpacing * i);

            seg.SetActive(false);
            segments.Add(seg);
        }
    }

    private void UpdateSegments(int count)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].SetActive(i < count);
        }
    }

    // ===============================
    // ARROW HEAD
    // ===============================

    private void UpdateArrowHead(int visibleSegments)
    {
        if (arrowHead == null)
            return;

        if (visibleSegments <= 0)
        {
            arrowHead.gameObject.SetActive(false);
            return;
        }

        float y =
            firstSegmentLocalPosition.y +
            segmentSpacing * (visibleSegments - 1) +
            arrowHeadOffsetAboveLastSegment;

        arrowHead.anchoredPosition = new Vector2(0, y);
        arrowHead.gameObject.SetActive(true);
    }

    // ===============================
    // VISIBILITY
    // ===============================

    private void UpdateVisibility(bool isIdle)
    {
        if (wholeIndicator == null)
            return;

        wholeIndicator.SetActive(!hideWhenIdle || !isIdle);
    }
}
