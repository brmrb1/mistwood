using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImagePageController : MonoBehaviour
{
    [Header("Page")]
    public GameObject pageRoot;

    [Header("Page Items")]
    public List<GameObject> pages = new List<GameObject>();

    [Header("Navigation Buttons")]
    public Button previousButton;
    public Button nextButton;

    [Header("Return Button")]
    public Button returnButton;

    [Header("UI点击音效")]
    public AudioClip uiClickSfx;

    private bool isBound;
    private int currentIndex;

    private void Awake()
    {
        BindUI();

        if (pageRoot != null)
        {
            pageRoot.SetActive(false);
        }

        currentIndex = 0;
        ApplyCurrentContent();
    }

    public void OpenPage()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        BindUI();

        if (pageRoot != null)
        {
            pageRoot.SetActive(true);
        }

        ApplyCurrentContent();
    }

    public void ClosePage()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        if (pageRoot != null)
        {
            pageRoot.SetActive(false);
        }
    }

    public void TogglePage()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        BindUI();

        if (pageRoot == null)
        {
            return;
        }

        pageRoot.SetActive(!pageRoot.activeSelf);

        if (pageRoot.activeSelf)
        {
            ApplyCurrentContent();
        }
    }

    public void PreviousImage()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        if (pages.Count == 0)
        {
            return;
        }

        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = pages.Count - 1;
        }

        ApplyCurrentContent();
    }

    public void NextImage()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        if (pages.Count == 0)
        {
            return;
        }

        currentIndex++;
        if (currentIndex >= pages.Count)
        {
            currentIndex = 0;
        }

        ApplyCurrentContent();
    }

    private void BindUI()
    {
        if (isBound)
        {
            return;
        }

        isBound = true;

        if (previousButton != null)
        {
            previousButton.onClick.AddListener(PreviousImage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextImage);
        }

        if (returnButton != null)
        {
            returnButton.onClick.AddListener(ClosePage);
        }
    }

    private void ApplyCurrentContent()
    {
        if (pages.Count == 0)
        {
            return;
        }

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        if (currentIndex >= pages.Count)
        {
            currentIndex = pages.Count - 1;
        }

        for (int i = 0; i < pages.Count; i++)
        {
            GameObject page = pages[i];
            if (page != null)
            {
                page.SetActive(i == currentIndex);
            }
        }

        Debug.Log($"[ImagePageController] Applied page index={currentIndex}, totalPages={pages.Count}");
    }

    private void OnValidate()
    {
        if (pages == null)
        {
            return;
        }

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == null)
            {
                continue;
            }

            if (pages[i].transform.parent != null && pageRoot != null && pages[i].transform.parent != pageRoot.transform)
            {
                Debug.LogWarning($"[ImagePageController] 页面 {pages[i].name} 不在 Page Root 下面，切页时可能看起来像没切换。", this);
            }
        }
    }
}