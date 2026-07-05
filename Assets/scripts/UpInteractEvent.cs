using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 放大查看交互事件（支持提升UI层级，放大，显示提示图，点击恢复）
public class UpInteractEvent : MonoBehaviour
{
    [Header("交互配置")]
    [Tooltip("弹出的提示图片/文字对象 (需要提前放在 Canvas 下并隐藏)")]
    public GameObject promptUI;

    [Tooltip("交互时播放的音效")]
    public AudioClip interactSfx;
    
    [Tooltip("放大倍数")]
    public float scaleMultiplier = 1.5f;

    // 用来记录物体最开始的状态
    private Vector3 originalScale;
    private Canvas myCanvas;
    private int originalSortingOrder;
    private bool originalOverrideSorting;
    private string originalSortingLayerName;
    private GraphicRaycaster raycaster;
    private bool isScaled = false; // 用于标记当前是否处于被放大的互动状态
    
    private SpriteRenderer mySprite;
    private int originalSpriteOrder;
    private string originalSpriteLayer;

    [Header("视觉效果")]
    [Tooltip("忽明忽暗的速度")]
    public float pulseSpeed = 3f;
    [Tooltip("忽明忽暗的最低亮度/透明度倍率 (0~1)")]
    public float minPulse = 0.5f;
    
    private Graphic myGraphic;
    private Color originalSpriteColor = Color.white;
    private Color originalGraphicColor = Color.white;

    // 记录是否是自己动态添加的 Canvas 和 Raycaster
    private bool addedCanvasDynamically = false;
    private bool addedRaycasterDynamically = false;

    private void Awake()
    {
        // 记录原始缩放值
        originalScale = transform.localScale;

        // 获取并记录原始颜色
        mySprite = GetComponent<SpriteRenderer>();
        if (mySprite != null) originalSpriteColor = mySprite.color;

        myGraphic = GetComponent<Graphic>();
        if (myGraphic != null) originalGraphicColor = myGraphic.color;
    }

    // 由 DialogueManager 唤醒
    public void StartInteraction()
    {
        PlayInteractSfx();
        // 1. 确保自身激活
        gameObject.SetActive(true);
        isScaled = true;

        // 2. 对于 UI 元素，动态添加或获取 Canvas 组件
        if (GetComponent<RectTransform>() != null)
        {
            myCanvas = GetComponent<Canvas>();
            if (myCanvas == null) 
            {
                myCanvas = gameObject.AddComponent<Canvas>();
                addedCanvasDynamically = true; // 标记是动态加的
                // 添加 Canvas 后，需要 GraphicRaycaster 才能独立接收点击事件
                raycaster = gameObject.AddComponent<GraphicRaycaster>(); 
                addedRaycasterDynamically = true;
            }

            // 保存原有的渲染层级信息
            originalOverrideSorting = myCanvas.overrideSorting;
            originalSortingLayerName = myCanvas.sortingLayerName;
            originalSortingOrder = myCanvas.sortingOrder;

            // 设置为覆盖层级，并且设为UI层级和巨大的数字保证在最上面
            myCanvas.overrideSorting = true;
            myCanvas.sortingLayerName = "UI";
            myCanvas.sortingOrder = 10; 
        }

        // 3. 对于 2D 游戏物体，尝试获取SpriteRenderer并提升层级
        mySprite = GetComponent<SpriteRenderer>();
        if (mySprite != null)
        {
            originalSpriteOrder = mySprite.sortingOrder;
            originalSpriteLayer = mySprite.sortingLayerName;
            mySprite.sortingLayerName = "UI";
            mySprite.sortingOrder = 1;
        }

        // 4. 放大物体
        transform.localScale = originalScale * scaleMultiplier;

        // 5. 显示对应的提示图片/文字
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

        PlayInteractSfx();
        
        isScaled = false;

        // 2. 恢复原来大小
        transform.localScale = originalScale;

        // 3. 恢复原来的渲染层级
        if (myCanvas != null)
        {
            if (addedCanvasDynamically)
            {
                // 如果是动态添加的，为了不影响原本其他系统的判定，我们在结束时销毁它
                if (raycaster != null) Destroy(raycaster);
                Destroy(myCanvas);
            }
            else
            {
                // 否则只是恢复之前的数值
                myCanvas.sortingOrder = originalSortingOrder;
                myCanvas.sortingLayerName = originalSortingLayerName;
                myCanvas.overrideSorting = originalOverrideSorting;
            }
        }
        
        if (mySprite != null)
        {
            mySprite.sortingOrder = originalSpriteOrder;
            mySprite.sortingLayerName = originalSpriteLayer;
            mySprite.color = originalSpriteColor;
        }

        if (myGraphic != null)
        {
            myGraphic.color = originalGraphicColor;
        }

        // 4. 恢复剧情（把协程交给一直存活的 DialogueManager 来运行，防止自己被隐藏后卡死）
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
        }

        // 1. 最后再隐藏提示图片（防止 promptUI 就是自己导致物体被干掉，协程跑不起来）
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        Debug.Log($"【{gameObject.name}】结束放大事项，恢复原状并继续剧情。");
    }

    private void PlayInteractSfx()
    {
        if (interactSfx == null) return;
        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(interactSfx, playPosition);
    }

    // 延迟一帧恢复对话，这是为了消化掉当前那一帧玩家点鼠标的输入动作
    private System.Collections.IEnumerator ResumeDialogueRoutine()
    {
        yield return null; 
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }

    private void Update()
    {
        // 如果当前处于放大状态
        if (isScaled)
        {
            // 忽明忽暗效果演算（基于时间和PingPong实现来回过渡）
            float pulseValue = Mathf.Lerp(minPulse, 1f, Mathf.PingPong(Time.time * pulseSpeed, 1f));

            if (mySprite != null)
            {
                Color c = originalSpriteColor;
                c.r *= pulseValue; c.g *= pulseValue; c.b *= pulseValue; // 降低RGB亮度
                mySprite.color = c;
            }
            if (myGraphic != null)
            {
                Color c = originalGraphicColor;
                c.r *= pulseValue; c.g *= pulseValue; c.b *= pulseValue; // 降低RGB亮度
                myGraphic.color = c;
            }

            // 如果玩家按下了鼠标左键（或触屏）
            if (Input.GetMouseButtonDown(0))
            {
                FinishInteraction();
            }
        }
    }
}