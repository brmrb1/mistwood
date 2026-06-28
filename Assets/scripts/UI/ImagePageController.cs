using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImagePageController : MonoBehaviour
{
    [Header("Page")]
    public GameObject pageRoot;

    [Header("Image Display")]
    public Image targetImage;

    [Header("Image List")]
    public List<Sprite> images = new List<Sprite>();

    [Header("Navigation Buttons")]
    public Button previousButton;
    public Button nextButton;

    [Header("Return Button")]
    public Button returnButton;

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
        ApplyCurrentImage();
    }

    public void OpenPage()
    {
        if (pageRoot != null)
        {
            pageRoot.SetActive(true);
        }

        ApplyCurrentImage();
    }

    public void ClosePage()
    {
        if (pageRoot != null)
        {
            pageRoot.SetActive(false);
        }
    }

    public void TogglePage()
    {
        if (pageRoot == null)
        {
            return;
        }

        pageRoot.SetActive(!pageRoot.activeSelf);

        if (pageRoot.activeSelf)
        {
            ApplyCurrentImage();
        }
    }

    public void PreviousImage()
    {
        if (images.Count == 0)
        {
            return;
        }

        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = images.Count - 1;
        }

        ApplyCurrentImage();
    }

    public void NextImage()
    {
        if (images.Count == 0)
        {
            return;
        }

        currentIndex++;
        if (currentIndex >= images.Count)
        {
            currentIndex = 0;
        }

        ApplyCurrentImage();
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

    private void ApplyCurrentImage()
    {
        if (targetImage == null)
        {
            return;
        }

        if (images.Count == 0)
        {
            targetImage.sprite = null;
            return;
        }

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        if (currentIndex >= images.Count)
        {
            currentIndex = images.Count - 1;
        }

        Sprite sprite = images[currentIndex];
        if (sprite != null)
        {
            targetImage.sprite = sprite;
        }
    }
}