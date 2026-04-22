using UnityEngine;

public class MenuManager : MonoBehaviour, IEyeCloseStopper
{
    // === Prepoj v Unity Inspectore ===
    [Header("Submenu Panels")]
    public GameObject subMenuUp;
    public GameObject subMenuDown;
    public GameObject subMenuLeft;
    public GameObject subMenuRight;
    public GameObject subMenuStop;

    [Header("Visual Settings - Materials")]
    public Material activeMaterial;
    public Material normalMaterial;

    // === Interné premenné ===
    private GameObject currentActiveSubMenu = null; // Menu, na ktoré sa práve pozeráš
    private GameObject lockedSubMenu = null;        // Menu, ktoré má aktívny príkaz (Zelené)
    private Renderer currentActiveRenderer = null;

    // ---------------------------------------------------------
    // LOGIKA PRE SUBMENU (Upravená pre "uzamykanie")
    // ---------------------------------------------------------

    public void ShowSubMenu(GameObject subMenuToShow)
    {
        // Ak už je nejaké menu otvorené, skryjeme ho, ALE IBA AK:
        // 1. To nie je to isté menu, čo chceme otvoriť
        // 2. A zároveň to menu NIE JE UZAMKNUTÉ (nie je tam aktívny príkaz)
        if (currentActiveSubMenu != null
            && currentActiveSubMenu != subMenuToShow
            && currentActiveSubMenu != lockedSubMenu)
        {
            currentActiveSubMenu.SetActive(false);
        }

        if (subMenuToShow != null)
        {
            subMenuToShow.SetActive(true);
            currentActiveSubMenu = subMenuToShow;
        }
    }

    public void HideSubMenu(GameObject subMenuToHide)
    {
        // Skryjeme menu iba ak NIE JE uzamknuté.
        // Ak mám zapnutý pohyb Vpred (Up je uzamknuté), a prestanem sa pozerať na Vpred,
        // menu Vpred musí ostať svietiť.
        if (subMenuToHide != null && subMenuToHide != lockedSubMenu)
        {
            subMenuToHide.SetActive(false);

            if (currentActiveSubMenu == subMenuToHide)
            {
                currentActiveSubMenu = null;
            }
        }
    }

    // Táto metóda natvrdo všetko skryje (použije sa pri STOP alebo zmene príkazu)
    private void ForceHideAllExcept(GameObject exceptionMenu)
    {
        if (subMenuUp != null && subMenuUp != exceptionMenu) subMenuUp.SetActive(false);
        if (subMenuDown != null && subMenuDown != exceptionMenu) subMenuDown.SetActive(false);
        if (subMenuLeft != null && subMenuLeft != exceptionMenu) subMenuLeft.SetActive(false);
        if (subMenuRight != null && subMenuRight != exceptionMenu) subMenuRight.SetActive(false);
        if (subMenuStop != null && subMenuStop != exceptionMenu) subMenuStop.SetActive(false);
    }

    // ---------------------------------------------------------
    // LOGIKA: VÝMENA MATERIÁLOV + LOGIKA UZAMKNUTIA
    // ---------------------------------------------------------

    public void HighlightButton(GameObject buttonObj)
    {
        ResetActiveButtonColor();

        GameObject newLockedMenu = GetParentSubMenu(buttonObj);

        ForceHideAllExcept(newLockedMenu);

        lockedSubMenu = newLockedMenu;
        if (lockedSubMenu != null)
        {
            lockedSubMenu.SetActive(true);
            currentActiveSubMenu = lockedSubMenu;
        }

        Renderer rend = GetButtonPlateRenderer(buttonObj);
        if (rend != null)
        {
            rend.material = activeMaterial;
            currentActiveRenderer = rend;
        }
    }


    public void ResetAllVisuals()
    {
        ResetActiveButtonColor();
        lockedSubMenu = null;

        ForceHideAllExcept(null);

        Debug.Log("Vizuál resetovaný (STOP).");
    }


    private void ResetActiveButtonColor()
    {
        if (currentActiveRenderer != null && normalMaterial != null)
        {
            currentActiveRenderer.material = normalMaterial;
            currentActiveRenderer = null;
        }
    }

    private GameObject GetParentSubMenu(GameObject buttonObj)
    {
        Transform t = buttonObj.transform;
        if (t.IsChildOf(subMenuUp.transform)) return subMenuUp;
        if (t.IsChildOf(subMenuDown.transform)) return subMenuDown;
        if (t.IsChildOf(subMenuLeft.transform)) return subMenuLeft;
        if (t.IsChildOf(subMenuRight.transform)) return subMenuRight;
        return null;
    }

    private Renderer GetButtonPlateRenderer(GameObject rootObj)
    {
        // Tvoja cesta z predchádzajúceho riešenia
        string fullPath = "UIBackplateOuterGeometry/UX.Button.BackplateOuterGeometry";
        Transform target = rootObj.transform.Find(fullPath);

        if (target != null)
        {
            Renderer r = target.GetComponent<Renderer>();
            if (r != null) return r;
        }

        // Záloha
        Transform frontPlate = rootObj.transform.Find("CompressibleButtonVisuals/FrontPlate");
        if (frontPlate != null)
        {
            Renderer r = frontPlate.GetComponent<Renderer>();
            if (r != null) return r;
        }

        return null;
    }

    // --- Wrappery pre Unity Eventy ---
    public void ShowUpOptions() => ShowSubMenu(subMenuUp);
    public void HideUpOptions() => HideSubMenu(subMenuUp);
    public void ShowDownOptions() => ShowSubMenu(subMenuDown);
    public void HideDownOptions() => HideSubMenu(subMenuDown);
    public void ShowLeftOptions() => ShowSubMenu(subMenuLeft);
    public void HideLeftOptions() => HideSubMenu(subMenuLeft);
    public void ShowRightOptions() => ShowSubMenu(subMenuRight);
    public void HideRightOptions() => HideSubMenu(subMenuRight);
    public void ShowStopOptions() => ShowSubMenu(subMenuStop);
    public void HideStopOptions() => HideSubMenu(subMenuStop);

    public void EyeCloseStop()
    {
        ResetAllVisuals();
    }
}
