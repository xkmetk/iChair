using UnityEngine;

public class ButtonSingleHighlighter : MonoBehaviour
{
    [Header("Target Button Object")]
    public GameObject targetButton;

    [Header("Materials")]
    public Material highlightMaterial;
    public Material normalMaterial;

    private Renderer buttonRenderer;

    private string backplatePath = "UIBackplateOuterGeometry/UX.Button.BackplateOuterGeometry";
    private string fallbackPath = "CompressibleButtonVisuals/FrontPlate";

    void Start()
    {
        if (targetButton == null)
        {
            Debug.LogError("❌ ButtonSingleHighlighter: targetButton nie je nastavený!");
            return;
        }

        buttonRenderer = FindButtonRenderer();

        if (buttonRenderer == null)
        {
            Debug.LogError("❌ ButtonSingleHighlighter: Renderer sa nenašiel! Skontroluj hierarchiu tlačidla.");
        }
    }

    // ---------------------------------------
    //              PUBLIC METHODS
    // ---------------------------------------

    public void HighlightButton()
    {
        if (buttonRenderer == null || highlightMaterial == null)
        {
            Debug.LogWarning("⚠ HighlightButton: Nie je renderer alebo highlightMaterial.");
            return;
        }

        buttonRenderer.material = highlightMaterial;
    }

    public void DeactivateHighlightButton()
    {
        if (buttonRenderer == null || normalMaterial == null)
        {
            Debug.LogWarning("⚠ DeactivateHighlightButton: Nie je renderer alebo normalMaterial.");
            return;
        }

        buttonRenderer.material = normalMaterial;
    }

    // ---------------------------------------
    //          FIND RENDERER LOGIC
    // ---------------------------------------

    private Renderer FindButtonRenderer()
    {
        // 1️⃣ Najprv skúsime tú istú cestu, ako používa MenuManager
        Transform backplate = targetButton.transform.Find(backplatePath);
        if (backplate != null)
        {
            Renderer r = backplate.GetComponent<Renderer>();
            if (r != null) return r;
        }

        // 2️⃣ Záloha — vrchná časť tlačidla
        Transform fallback = targetButton.transform.Find(fallbackPath);
        if (fallback != null)
        {
            Renderer r = fallback.GetComponent<Renderer>();
            if (r != null) return r;
        }

        return null; // keď sa nič nenašlo
    }
}
