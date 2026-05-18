using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 放大查看交互事件（支持提升UI层级，放大，显示提示图，点击恢复）
public class UpInteractEvent : MonoBehaviour
{
    [Header("交互配置")]
    [Tooltip("弹出的提示图片/文字对象 (需要提前放在 Canvas 下并隐藏)")]
    public GameObject promptUI;
    
    [Tooltip("放大倍数")]
    public float scaleMultiplier = 1.5f;

    // 用来记录物体最开始的状态
    private Vector3 originalScale;
    private Canvas myCanvas;
    private int originalSortingOrder;
    private bool originalOverrideSorting;
    private GraphicRaycaster raycaster;
    private bool isScaled = false; // 用于标记当前是否处于被放大的互动状态

    private void Awake()
    {
        // 记录原始缩放值
        originalScale = transform.localScale;
    }

    // 由 DialogueManager 唤醒
    public void StartInteraction()
    {
        // 1. 确保自身激活
        gameObject.SetActive(true);
        isScaled = true;

        // 2. 动态添加或获取 Canvas 组件，强行把该物体拔高到屏幕最前方
        myCanvas = GetComponent<Canvas>();
        if (myCanvas == null) 
        {
            myCanvas = gameObject.AddComponent<Canvas>();
            // 添加 Canvas 后，需要 GraphicRaycaster 才能独立接收点击事件
            raycaster = gameObject.AddComponent<GraphicRaycaster>(); 
        }

        // 保存原有的渲染层级信息
        originalOverrideSorting = myCanvas.overrideSorting;
        originalSortingOrder = myCanvas.sortingOrder;

        // 设置为覆盖层级，并且设为一个巨大的数字保证在最上面
        myCanvas.overrideSorting = true;
        myCanvas.sortingOrder = 999; 

        // 3. 放大物体
        transform.localScale = originalScale * scaleMultiplier;

        // 4. 显示对应的提示图片/文字
        if (promptUI != null)
        {
            promptUI.SetActive(true);
        }

        Debug.Log($"【{gameObject.name}】已强行放大并提升渲染层级！");
    }

    // 玩家点击以后，恢复原状并继续对话
    public void FinishInteraction()
    {
        if (!isScaled) return;
        
        isScaled = false;
        
        // 1. 隐藏提示图片
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        // 2. 恢复原来大小
        transform.localScale = originalScale;

        // 3. 恢复原来的渲染层级
        if (myCanvas != null)
        {
            myCanvas.sortingOrder = originalSortingOrder;
            myCanvas.overrideSorting = originalOverrideSorting;
        }

        // 4. 恢复剧情（使用协程延迟一帧，防止点击穿透）
        StartCoroutine(ResumeDialogueDelay());

        Debug.Log($"【{gameObject.name}】结束放大事项，恢复原状并继续剧情。");
    }

    // 延迟一帧恢复对话，这是为了消化掉当前那一帧玩家点鼠标的输入动作
    private System.Collections.IEnumerator ResumeDialogueDelay()
    {
        yield return null; 
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }

    private void Update()
    {
        // 如果当前处于放大状态，并且玩家按下了鼠标左键（或触屏）
        if (isScaled && Input.GetMouseButtonDown(0))
        {
            FinishInteraction();
        }
    }
}