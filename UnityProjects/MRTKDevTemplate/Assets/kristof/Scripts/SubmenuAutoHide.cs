using System.Collections;
using UnityEngine;

public class SubmenuAutoHide : MonoBehaviour
{
    [Header("Buttons in this submenu")]
    public GameObject slowButton;
    public GameObject fastButton;

    [Header("Timeout (seconds)")]
    public float timeout = 3f;

    private Coroutine hideRoutine;
    private bool isPinned = false;   // true = už je tu zvolená rýchlosť

    /// <summary>
    /// Otvorí submenu na dočasný výber (spustí timer).
    /// Ak už je submenu pinned, necháme ho otvorené a timer neriešime.
    /// </summary>
    public void OpenTemp()
    {
        if (slowButton != null) slowButton.SetActive(true);
        if (fastButton != null) fastButton.SetActive(true);

        if (isPinned)
        {
            // Už tu máme aktívnu voľbu – submenu necháme otvorené bez auto-hide
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            return;
        }

        // reset timer
        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    /// <summary>
    /// Zavolaj, keď si user vyberie Slow/Fast v tomto submení.
    /// Submenu ostáva viditeľné, len vypneme timer a označíme ho ako "aktívne".
    /// </summary>
    public void PinSelected()
    {
        isPinned = true;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        // slow/fast NEskrývame – ostávajú viditeľné
    }

    /// <summary>
    /// Zavrie submenu natvrdo (pri zmene smeru / Stop / prepnutí na iné submenu).
    /// </summary>
    public void ForceClose()
    {
        isPinned = false;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (slowButton != null) slowButton.SetActive(false);
        if (fastButton != null) fastButton.SetActive(false);
    }

    /// <summary>
    /// Použi pri globálnom resete (Idle).
    /// </summary>
    public void ResetSubmenu()
    {
        ForceClose();
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(timeout);

        // Ak medzičasom nebolo submenu "pinned", auto-hide
        if (!isPinned)
        {
            if (slowButton != null) slowButton.SetActive(false);
            if (fastButton != null) fastButton.SetActive(false);
        }

        hideRoutine = null;
    }
}
