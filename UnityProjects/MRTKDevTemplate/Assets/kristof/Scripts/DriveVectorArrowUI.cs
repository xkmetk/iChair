using UnityEngine;

public class DriveVectorArrowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform directionPivot;
    [SerializeField] private GameObject arrowRoot;
    [SerializeField] private Renderer[] segmentRenderers;

    [Header("Rotation")]
    [SerializeField] private float neutralAngle = 0f;
    [SerializeField] private bool hideWhenNeutral = false;
    [SerializeField] private bool useFullVectorAngle = true;

    [Header("Colors")]
    [SerializeField] private Color inactiveColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color forwardColor = Color.white;
    [SerializeField] private Color backwardColor = Color.red;
    [SerializeField] private Color turnOnlyColor = Color.yellow;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock[] propertyBlocks;
    private int lastActiveSegments = -1;
    private Vector2 lastDirection = new Vector2(float.MinValue, float.MinValue);

    private void Awake()
    {
        if (segmentRenderers == null)
        {
            segmentRenderers = new Renderer[0];
        }

        propertyBlocks = new MaterialPropertyBlock[segmentRenderers.Length];

        for (int i = 0; i < segmentRenderers.Length; i++)
        {
            propertyBlocks[i] = new MaterialPropertyBlock();
        }

        SetAllSegmentsInactive();
    }

    public void SetState(int forwardLevel, int backwardLevel, int leftLevel, int rightLevel, int maxLevel)
    {
        int drive = forwardLevel - backwardLevel;
        int turn = rightLevel - leftLevel;

        Vector2 rawDirection = new Vector2(turn, drive);
        bool isNeutral = rawDirection == Vector2.zero;

        if (arrowRoot != null)
        {
            arrowRoot.SetActive(!hideWhenNeutral || !isNeutral);
        }

        if (isNeutral)
        {
            if (directionPivot != null)
            {
                directionPivot.localRotation = Quaternion.Euler(0f, 0f, neutralAngle);
            }

            SetAllSegmentsInactive();
            return;
        }

        float zAngle = ComputeArrowAngle(rawDirection);
        if (directionPivot != null)
        {
            directionPivot.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        }

        Color activeColor = ResolveActiveColor(drive, turn);
        int activeSegments = ResolveActiveSegmentCount(forwardLevel, backwardLevel, leftLevel, rightLevel, maxLevel);

        UpdateSegments(activeSegments, activeColor);

        if (logStateChanges)
        {
            if (lastActiveSegments != activeSegments || lastDirection != rawDirection)
            {
                Debug.Log($"DriveVectorArrowUI | drive={drive}, turn={turn}, angle={zAngle}, segments={activeSegments}");
                lastActiveSegments = activeSegments;
                lastDirection = rawDirection;
            }
        }
    }

    private float ComputeArrowAngle(Vector2 rawDirection)
    {
        Vector2 dir;

        if (useFullVectorAngle)
        {
            dir = rawDirection.normalized;
        }
        else
        {
            float x = Mathf.Clamp(rawDirection.x, -1f, 1f);
            float y = Mathf.Sign(rawDirection.y == 0f ? 1f : rawDirection.y);
            dir = new Vector2(x, y).normalized;
        }

        // Predpoklad: model/sprite šípky v neutrálnej orientácii smeruje hore.
        // Ak bude smerovať inam, dolaď neutralAngle v inspectore.
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        return angle + neutralAngle;
    }

    private Color ResolveActiveColor(int drive, int turn)
    {
        if (drive > 0)
            return forwardColor;

        if (drive < 0)
            return backwardColor;

        if (turn != 0)
            return turnOnlyColor;

        return inactiveColor;
    }

    private int ResolveActiveSegmentCount(int forwardLevel, int backwardLevel, int leftLevel, int rightLevel, int maxLevel)
    {
        int driveMagnitude = Mathf.Max(forwardLevel, backwardLevel);
        int turnMagnitude = Mathf.Max(leftLevel, rightLevel);

        int magnitude = Mathf.Max(driveMagnitude, turnMagnitude);
        magnitude = Mathf.Clamp(magnitude, 0, maxLevel);

        int segmentCount = segmentRenderers.Length;
        if (segmentCount == 0 || maxLevel <= 0)
            return 0;

        float normalized = magnitude / (float)maxLevel;
        int activeSegments = Mathf.CeilToInt(normalized * segmentCount);

        if (magnitude > 0)
        {
            activeSegments = Mathf.Max(1, activeSegments);
        }

        return Mathf.Clamp(activeSegments, 0, segmentCount);
    }

    private void SetAllSegmentsInactive()
    {
        UpdateSegments(0, inactiveColor);
    }

    private void UpdateSegments(int activeSegments, Color activeColor)
    {
        for (int i = 0; i < segmentRenderers.Length; i++)
        {
            bool isActive = i < activeSegments;
            SetRendererColor(segmentRenderers[i], propertyBlocks[i], isActive ? activeColor : inactiveColor);
        }
    }

    private void SetRendererColor(Renderer targetRenderer, MaterialPropertyBlock block, Color color)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.GetPropertyBlock(block);
        block.SetColor(ColorProp, color);
        targetRenderer.SetPropertyBlock(block);
    }
}
