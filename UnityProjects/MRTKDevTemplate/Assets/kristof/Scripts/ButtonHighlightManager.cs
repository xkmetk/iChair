using UnityEngine;

public class ButtonHighlightManager : MonoBehaviour
{
    [Header("Materials")]
    public Material normalMaterial;
    public Material activeMaterial;

    private Renderer currentRenderer = null;

    public void Highlight(GameObject button)
    {
        if (button == null) return;

        // Reset predošlého highlightu
        if (currentRenderer != null)
            currentRenderer.material = normalMaterial;

        // Nájdeme renderer tlačidla
        Renderer rend = GetButtonRenderer(button);
        if (rend != null)
        {
            rend.material = activeMaterial;
            currentRenderer = rend;
        }
    }

    public void Clear()
    {
        if (currentRenderer != null)
        {
            currentRenderer.material = normalMaterial;
            currentRenderer = null;
        }
    }

    private Renderer GetButtonRenderer(GameObject rootObj)
    {
        string path = "UIBackplateOuterGeometry/UX.Button.BackplateOuterGeometry";
        Transform t = rootObj.transform.Find(path);

        if (t != null)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) return r;
        }

        Transform fallback = rootObj.transform.Find("CompressibleButtonVisuals/FrontPlate");
        if (fallback != null)
        {
            Renderer r = fallback.GetComponent<Renderer>();
            if (r != null) return r;
        }

        return rootObj.GetComponentInChildren<Renderer>();
    }
}
