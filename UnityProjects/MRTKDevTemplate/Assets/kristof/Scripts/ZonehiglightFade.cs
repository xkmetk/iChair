using UnityEngine;

public class ZoneHighlightFade : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float inactiveAlpha = 0f;
    [SerializeField] private float activeAlpha = 0.35f;

    private float targetAlpha = 0f;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        SetAlphaImmediate(inactiveAlpha);
        targetAlpha = inactiveAlpha;
    }

    private void Update()
    {
        if (spriteRenderer == null)
            return;

        Color c = spriteRenderer.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
        spriteRenderer.color = c;
    }

    public void SetHighlighted(bool highlighted)
    {
        //Debug.Log(gameObject.name + " highlighted: " + highlighted);
        targetAlpha = highlighted ? activeAlpha : inactiveAlpha;
    }

    public void SetAlphaImmediate(float alpha)
    {
        if (spriteRenderer == null)
            return;

        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }
}
