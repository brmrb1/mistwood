using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 当 CSV 运行到 EVENT 类型并触发此脚本所在物体的名字时，
/// 该脚本会提升指定物体的 SortingOrder 层级，并检测拖拽是否到达目标区域。
/// </summary>
public class ObjectDragPuzzleEvent : MonoBehaviour
{
    [System.Serializable]
    public class SortingInfo
    {
        public SpriteRenderer renderer;
        [Tooltip("提升后的 Sorting Layer 名称 (例如 'UI')")]
        public string targetLayerName = "UI";
        [Tooltip("提升后的 Sorting Order 数值")]
        public int targetOrder = 100;

        [HideInInspector] public int originalOrder;
        [HideInInspector] public string originalLayerName;
    }

    [Header("层级提升配置")]
    [Tooltip("在事件开始时，这些物体的层级会根据下方设置进行临时提升")]
    public List<SortingInfo> itemsToHighLevel = new List<SortingInfo>();

    [Header("拖拽检测配置")]
    [Tooltip("玩家需要拖拽的那个物体")]
    public Transform draggableItem;
    [Tooltip("是否在事件开始时激活物体上的 dragright 脚本和 BoxCollider2D")]
    public bool enableInteractionComponents = true;
    [Tooltip("事件结束时需要隐藏的引导物体列表 (可以放多个引导)")]
    public List<GameObject> guideObjectsToHide = new List<GameObject>();
    [Tooltip("目标区域的中心点/位置")]
    public Transform targetArea;
    [Tooltip("判定成功的距离阈值")]
    public float successThreshold = 1.0f;

    [Header("音效")]
    public AudioClip interactSfx;

    private bool isMonitoring = false;
    private bool hasFinished = false;
    private bool wasInRange = false;

    /// <summary>
    /// 对话系统通过 SendMessage 调用的起始方法
    /// </summary>
    public void StartInteraction()
    {
        PlayInteractSfx();
        Debug.Log($"[ObjectDragPuzzleEvent] 事件已开始: {gameObject.name}");
        
        isMonitoring = true;
        hasFinished = false;
        wasInRange = false;

        // 1. 记录并修改各物体的层级与 Sorting Layer
        foreach (var item in itemsToHighLevel)
        {
            if (item.renderer != null)
            {
                item.originalOrder = item.renderer.sortingOrder;
                item.originalLayerName = item.renderer.sortingLayerName;

                item.renderer.sortingLayerName = item.targetLayerName;
                item.renderer.sortingOrder = item.targetOrder;
            }
        }

        // 2. 激活 dragright 脚本和 BoxCollider2D
        if (enableInteractionComponents && draggableItem != null)
        {
            dragright dr = draggableItem.GetComponent<dragright>();
            if (dr != null) dr.enabled = true;

            BoxCollider2D box = draggableItem.GetComponent<BoxCollider2D>();
            if (box != null) box.enabled = true;

            Debug.Log($"[ObjectDragPuzzleEvent] 已激活 {draggableItem.name} 上的交互组件");
        }

        // 为了防止对话框刚关上一帧就立刻触发逻辑，可以根据需要微调
    }

    private void Update()
    {
        if (!isMonitoring || hasFinished) return;

        if (draggableItem != null && targetArea != null)
        {
            float distance = Vector2.Distance(draggableItem.position, targetArea.position);
            bool isInRange = distance <= successThreshold;

            // 逻辑优化：支持两种判定方式
            // 方式 A: 物体在范围内时松开鼠标
            bool mouseReleasedInRange = isInRange && Input.GetMouseButtonUp(0);
            
            // 方式 B: 物体之前在范围内，但现在突然变为了隐藏状态（说明被 dragright 等脚本成功判定并隐藏了）
            bool disappearedAfterInRange = wasInRange && !draggableItem.gameObject.activeInHierarchy;

            if (mouseReleasedInRange || disappearedAfterInRange)
            {
                FinishEvent();
                return;
            }

            // 更新状态记录
            wasInRange = isInRange;
        }
    }

    private void FinishEvent()
    {
        PlayInteractSfx();
        hasFinished = true;
        isMonitoring = false;

        Debug.Log($"[ObjectDragPuzzleEvent] 玩家已成功将 {draggableItem.name} 拖至目标区域！");

        // 3. 还原层级与 Sorting Layer
        foreach (var item in itemsToHighLevel)
        {
            if (item.renderer != null)
            {
                item.renderer.sortingLayerName = item.originalLayerName;
                item.renderer.sortingOrder = item.originalOrder;
            }
        }

        // 4. 关闭交互组件（如果需要）
        if (enableInteractionComponents && draggableItem != null)
        {
            dragright dr = draggableItem.GetComponent<dragright>();
            if (dr != null) dr.enabled = false;

            BoxCollider2D box = draggableItem.GetComponent<BoxCollider2D>();
            if (box != null) box.enabled = false;
        }

        // 5. 隐藏引导物体
        foreach (var guide in guideObjectsToHide)
        {
            if (guide != null)
            {
                guide.SetActive(false);
            }
        }

        // 6. 通知对话系统恢复
        if (DialogueManager.Instance != null)
        {
             // 延迟一帧恢复以防点击冲突（参考 CommonInteractEvent 做法）
            StartCoroutine(ResumeDialogueRoutine());
        }
    }

    private void PlayInteractSfx()
    {
        if (interactSfx == null) return;
        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(interactSfx, playPosition);
    }

    private IEnumerator ResumeDialogueRoutine()
    {
        yield return null;
        DialogueManager.Instance.ResumeFromSuspended();
    }
}
