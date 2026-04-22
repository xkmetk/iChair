using System.Collections.Generic;
using UnityEngine;

public class ArrowStackUI : MonoBehaviour
{
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private int initialArrows = 3;
    [SerializeField] private Vector3 stepOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField] private float scaleFalloff = 0.015f;
    [SerializeField] private float minScale = 0.25f;

    private readonly List<GameObject> arrows = new List<GameObject>();
    private int currentCapacity = 0;

    private void Awake()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError($"{name}: Arrow prefab is not assigned.");
            return;
        }

        if (arrowPrefab.GetComponent<ArrowStackUI>() != null)
        {
            Debug.LogError($"{name}: Arrow prefab must NOT contain ArrowStackUI.");
            return;
        }

        SetCapacity(initialArrows);
        SetLevel(0);
    }

    public void SetCapacity(int count)
    {
        count = Mathf.Max(0, count);

        while (arrows.Count < count)
        {
            GameObject arrow = Instantiate(arrowPrefab, transform);
            arrows.Add(arrow);
        }

        currentCapacity = count;
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] == null)
                continue;

            bool shouldExistInCapacity = i < currentCapacity;
            arrows[i].SetActive(false);

            arrows[i].transform.localPosition = stepOffset * i;

            float scale = 1f - i * scaleFalloff;
            scale = Mathf.Clamp(scale, minScale, 1f);
            arrows[i].transform.localScale = Vector3.one * scale;

            arrows[i].hideFlags = HideFlags.None;

            // šípky nad aktuálnu kapacitu môžu zostať existovať,
            // len sa nikdy nezapnú
            if (!shouldExistInCapacity)
            {
                arrows[i].transform.localPosition = stepOffset * i;
            }
        }
    }

    public void SetLevel(int level)
    {
        level = Mathf.Clamp(level, 0, currentCapacity);

        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] != null)
            {
                arrows[i].SetActive(i < level && i < currentCapacity);
            }
        }
    }
}
