using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float scaleMultiplier = 1.2f; // 放大倍数
    private Vector3 originalScale;

    private void Start()
    {
        // 记录按钮初始的缩放值
        originalScale = transform.localScale;
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
    
    // （可选）当该脚本被禁用或者按钮被销毁时还原，防止缩放状态残留
    private void OnDisable()
    {
        transform.localScale = originalScale;
    }
}
