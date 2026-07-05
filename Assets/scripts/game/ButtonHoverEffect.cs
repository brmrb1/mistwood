using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float scaleMultiplier = 1.2f; // 放大倍数
    [Header("点击音效")]
    [SerializeField] private AudioClip firstClickSfx;  // 点击时先播放的音效
    [SerializeField] private AudioClip secondClickSfx; // 第一个音效播完后再播放的音效

    private Vector3 originalScale;
    private AudioSource audioSource;
    private Coroutine clickSfxCoroutine;

    private void Start()
    {
        // 记录按钮初始的缩放值
        originalScale = transform.localScale;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // 当鼠标指针进入按钮范围时触发
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * scaleMultiplier;
    }

    // 当鼠标指针离开按钮范围时触发
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSfxCoroutine != null)
        {
            StopCoroutine(clickSfxCoroutine);
        }

        clickSfxCoroutine = StartCoroutine(PlayClickSfxSequence());
    }
    
    // （可选）当该脚本被禁用或者按钮被销毁时还原，防止缩放状态残留
    private void OnDisable()
    {
        transform.localScale = originalScale;

        if (clickSfxCoroutine != null)
        {
            StopCoroutine(clickSfxCoroutine);
            clickSfxCoroutine = null;
        }
    }

    private IEnumerator PlayClickSfxSequence()
    {
        if (firstClickSfx != null)
        {
            audioSource.PlayOneShot(firstClickSfx);
            yield return new WaitForSeconds(firstClickSfx.length);
        }

        if (secondClickSfx != null)
        {
            audioSource.PlayOneShot(secondClickSfx);
        }

        clickSfxCoroutine = null;
    }
}
