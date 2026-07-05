using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class CameraMoveButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("移动目标设置")]
    [Tooltip("你想移动什么？如果不填，默认移动主摄像机。你也可以把一个包含所有背景/物品的父节点拖拉进来移动它")]
    public Transform targetObject;
    
    [Tooltip("你想让目标偏移多少距离？(例如上面设置Y=10)")]
    public Vector3 moveOffset = new Vector3(0, 10f, 0); 
    private Vector3 targetUpPosition; // 计算出的第二位置
    private Vector3 originalDownPosition; // 目标的初始位置
    public float moveSpeed = 5f; // 移动的平滑速度

    [Header("按钮图片设置")]
    public Image buttonImage;
    public Sprite upSprite; // 此时镜头在上方，表示"向下"或处于第二状态的图片
    public Sprite downSprite; // 此时镜头在下方，表示"向上"或初始状态的图片

    [Header("UI点击音效")]
    public AudioClip uiClickSfx;

    [Header("悬停放大设置")]
    public float hoverScaleMultiplier = 1.1f; // 缩放倍数
    private Vector3 originalButtonScale;

    private bool isUp = false; // 当前镜头的状态（是否在上方）
    private Coroutine moveCoroutine;

    private void Start()
    {
        originalButtonScale = transform.localScale; // 记录按钮本身初始大小

        if (targetObject == null && Camera.main != null)
            targetObject = Camera.main.transform;

        if (targetObject != null)
        {
            originalDownPosition = targetObject.position; // 记录初始位置
            targetUpPosition = originalDownPosition + moveOffset;   // 根据偏移量算出目标位置
        }

        if (buttonImage == null)
        {
            buttonImage = GetComponent<Image>();
        }

        // 初始化按钮图片
        if (buttonImage != null && downSprite != null)
        {
            buttonImage.sprite = downSprite;
        }

        // 如果脚本挂在Button上，自动监听点击事件
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnButtonClick);
        }
    }

    public void OnButtonClick()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        isUp = !isUp;

        // 切换显示的图片
        if (buttonImage != null)
        {
            buttonImage.sprite = isUp ? upSprite : downSprite;
        }

        // 停止上一次的移动动画，开始新的移动
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
        
        Vector3 targetPos = isUp ? targetUpPosition : originalDownPosition;
        moveCoroutine = StartCoroutine(MoveCameraTo(targetPos));
    }

    private IEnumerator MoveCameraTo(Vector3 targetPos)
    {
        if (targetObject == null) yield break;

        // 平滑移动目标直到接近目标位置
        while (Vector3.Distance(targetObject.position, targetPos) > 0.01f)
        {
            targetObject.position = Vector3.Lerp(targetObject.position, targetPos, Time.deltaTime * moveSpeed);
            yield return null;
        }
        // 最后精确对其目标位置
        targetObject.position = targetPos;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalButtonScale * hoverScaleMultiplier;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalButtonScale;
    }

    private void OnDisable()
    {
        transform.localScale = originalButtonScale;
    }
}
