using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dragright : MonoBehaviour
{
    private Vector3 startPos;
    [SerializeField] private Transform correctTrans;    // 目标正确位置
    [SerializeField] private float matchDistance = 1.0f; // 距离判定范围（多近算拖到位了）
    
    [Header("生成图片相关")]
    [SerializeField] private Transform spawnPoint;       // 另一个生成位置
    [SerializeField] private GameObject[] resultPrefabs; // 拖拽成功生成的物体（可以放入3个不同形态的预制体）
    [SerializeField] private Vector3 customSpawnScale = new Vector3(1f, 1f, 1f); // 如果生成出来太大/太小，直接调节这里！

    [Header("成功动画")]
    [SerializeField] private Animator successAnimator;   // 拖动到目标位置成功时播放动画的Animator组件
    [SerializeField] private string successTriggerName = "Play"; // 动画触发器的名称
    [SerializeField] private float animationDuration = 1.0f; // 【新增】动画时长，等待该时间后再消失/生成新图
    
    [Header("悬浮提示")]
    [SerializeField] private GameObject hoverTooltipObj; // 鼠标悬浮时显示的提示图（建议把该提示图作为子物体，默认设为不激活/隐藏，然后拖入此格子）
    
    private bool isAnimating = false; // 是否在播放动画中，防止多次拖拽
    
    // 【修改】使用静态字典按“生成位置(spawnPoint)”来独立记录图片和状态
    // 只有拖到“同一个位置”的物品才会相互排斥和顶替
    private static Dictionary<Transform, GameObject> spawnedObjsDict = new Dictionary<Transform, GameObject>();
    private static Dictionary<Transform, dragright> lastDragDict = new Dictionary<Transform, dragright>();

    // 【新增】给外部（如判题脚本）提供一个公开的方法来获取当前所有坑位的状态
    public static Dictionary<Transform, dragright> GetCurrentPlacements()
    {
        return lastDragDict;
    }

    // 【新增】维护一个全局注册表，便于一次性重置所有拖拽物
    private static List<dragright> allDragrights = new List<dragright>();

    private void OnEnable()
    {
        if (!allDragrights.Contains(this)) allDragrights.Add(this);
    }

    private void OnDisable()
    {
        if (allDragrights.Contains(this)) allDragrights.Remove(this);
    }

    // 【新增】当点击反馈图时，希望把所有拖拽图片恢复到初始状态
    public static void ResetAllToInitial()
    {
        // 把场上注册的所有 dragright 都重置
        foreach (var d in allDragrights)
        {
            if (d != null)
            {
                d.ResetProgress();
            }
        }

        // 清空本脚本维护的占位字典信息，保证下一次可以重新占位
        lastDragDict.Clear();
        spawnedObjsDict.Clear();
    }

    // 【新增多轮机制】每轮结束后，清空本轮判定记忆。这样旧的图片就会留在原地变成“历史”。
    public static void StartNewRound()
    {
        // 1. 把所有这轮被拖拽出来的物品，统一打回原位
        foreach (var item in lastDragDict.Values)
        {
            if (item != null)
            {
                item.ResetProgress();
            }
        }
        
        // 2. 清空字典！！这样下一轮的时候，就不会去销毁上一轮留在原地的图片了！
        lastDragDict.Clear();
        spawnedObjsDict.Clear();
    }

    private int successCount = 0;                        // 成功次数记录
    // 【新增】让外部能够读取当前这个物品到底拖成功了几次
    public int CurrentSuccessCount => successCount;

    private SpriteRenderer spriteRenderer;               // 用于控制图片颜色和透明度

    private Vector3 originalScale;
    private Color originalColor;                         // 【新增】记录初始颜色，用于重置

    private void Start()
    {
        startPos = transform.position;
        originalScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void OnMouseEnter()
    {
        transform.localScale = originalScale * 1.2f;
        
        // 鼠标移入时显示悬浮图
        if (hoverTooltipObj != null)
        {
            hoverTooltipObj.SetActive(true);
        }
    }

    private void OnMouseExit()
    {
        transform.localScale = originalScale;
        
        // 鼠标移出时隐藏悬浮图
        if (hoverTooltipObj != null)
        {
            hoverTooltipObj.SetActive(false);
        }
    }

    private void OnMouseDown()
    {
        // 鼠标刚刚按下去（准备拖拽时）立刻让它消失
        if (hoverTooltipObj != null)
        {
            hoverTooltipObj.SetActive(false);
        }
    }

    // 拖拽过程
    private void OnMouseDrag()
    {
        // 正在播放成功动画时，不允许继续拖拽
        if (isAnimating) return;

        // 只要还没成功3次就可以拖拽
        if (successCount < 3)
        {
            Vector3 cursorPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            transform.position = new Vector3(cursorPos.x, cursorPos.y, transform.position.z);
        }
    }

    // 鼠标松开时判定
    private void OnMouseUp()
    {
        if (isAnimating || successCount >= 3 || correctTrans == null) return;

        // 计算当前位置和目标位置的距离（忽略Z轴）
        float distance = Vector2.Distance(transform.position, correctTrans.position);

        Debug.Log($"【拖拽判定】图片[{gameObject.name}]所在坐标: {transform.position}, 目标坑位[{correctTrans.name}]中心坐标: {correctTrans.position}。\n两点距离: {distance}，允许范围: {matchDistance}");

        if (distance <= matchDistance)
        {
            // --- 拖到了指定位置 ---

            // 【修改】如果配置了成功动画，则开始协程播放动画并延迟执行
            if (successAnimator != null && !string.IsNullOrEmpty(successTriggerName))
            {
                // 吸附到目标位置并开始延迟逻辑
                transform.position = new Vector3(correctTrans.position.x, correctTrans.position.y, transform.position.z);
                StartCoroutine(PlayAnimationAndFinish());
            }
            else
            {
                FinishSuccessLogic();
            }
        }
        else
        {
            // --- 没拖到指定位置，直接回到原位 ---
            transform.position = startPos;
        }
    }

    // 新增：播放动画并延迟生效的协程
    private IEnumerator PlayAnimationAndFinish()
    {
        isAnimating = true;
        successAnimator.SetTrigger(successTriggerName);
        
        // 等待设定的动画时长
        yield return new WaitForSeconds(animationDuration);
        
        FinishSuccessLogic();
        isAnimating = false;
    }

    // 分离出来的原本处理替换贴图和消失逻辑的方法
    private void FinishSuccessLogic()
    {
        // 【新增逻辑】只对“同一个目标生成点”起效的独立互斥判断
        if (spawnPoint != null)
            {
                // 检查同一个坑位上，之前是不是有别人拖成功过了
                if (lastDragDict.ContainsKey(spawnPoint))
                {
                    dragright lastDrag = lastDragDict[spawnPoint];
                    if (lastDrag != null && lastDrag != this)
                    {
                        // 发现是不同的图片，让占据该位置的前一个图片重置回家
                        lastDrag.ResetProgress();
                    }
                }
                // 登记这个坑位最新的占据者是我自己
                lastDragDict[spawnPoint] = this;
            }

            successCount++;

            // 在另一个位置生成对应形态的图片（销毁旧形态，生成新形态）
            if (spawnPoint != null && resultPrefabs != null && successCount <= resultPrefabs.Length)
            {
                // 按坑位去找之前生成的图片并销毁
                if (spawnedObjsDict.ContainsKey(spawnPoint) && spawnedObjsDict[spawnPoint] != null) 
                    Destroy(spawnedObjsDict[spawnPoint]);
                
                // 根据成功次数减1来获取对应的预制体索引 (0, 1, 2)
                GameObject prefabToSpawn = resultPrefabs[successCount - 1];
                if (prefabToSpawn != null)
                {
                    // 动态查找 PuzzleChecker 获取 Content 容器，将生成的预制体设为该 Content 的子物体
                    PuzzleChecker checker = Object.FindObjectOfType<PuzzleChecker>();
                    Transform targetContent = (checker != null && checker.scrollContent != null) ? checker.scrollContent : spawnPoint;

                    // 作为 targetContent 的子物体生成，以便跟随滑动条一起移动
                    GameObject newlySpawned = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation, targetContent);
                    
                    // 使用 spawnPoint（生成点）的真实位置，但将Z轴和当前被拖拽的物体保持一致，防止跑到不正确的前后层级
                    Vector3 newPos = spawnPoint.position;
                    newPos.z = transform.position.z;
                    // 重新赋值给新生成的物体
                    newlySpawned.transform.position = newPos;
                    
                    // 强制设为你指定的固定大小。由于它现在进入了 UI 画布的子层级，画布缩放会把它压得很小，这里通过除以父物体的缩放来抵消，让它在屏幕上保持你填写的真实大小！
                    if (targetContent != null)
                    {
                        Vector3 parentScale = targetContent.lossyScale;
                        newlySpawned.transform.localScale = new Vector3(
                            customSpawnScale.x / parentScale.x,
                            customSpawnScale.y / parentScale.y,
                            customSpawnScale.z / parentScale.z
                        );
                    }
                    else
                    {
                        newlySpawned.transform.localScale = customSpawnScale;
                    }

                    // 【新增：使其可点击】为实例生成的图片添加点击组件，并传给它当前这个拖拽物体
                    FeedbackClickable clickable = newlySpawned.AddComponent<FeedbackClickable>();
                    clickable.ownerDragright = this;

                    // 把新生成的物品登记记录在这个坑位上
                    spawnedObjsDict[spawnPoint] = newlySpawned;
                    
                    // 打印日志，告诉你是不是真的生成了
                    Debug.Log("成功在(" + spawnPoint.name + ")生成了图片: " + prefabToSpawn.name + "，当前世界坐标：" + newPos + "，且大小被强行缩放为了: " + customSpawnScale);
                }
                else
                {
                    Debug.LogWarning("你没有在 Result Prefabs 数组的第 " + (successCount) + " 格放预制体！！");
                }
            }
            else
            {
                if (spawnPoint == null) Debug.LogWarning("你没有指定 Spawn Point (生成位置)！请在面板里拖入。");
                if (resultPrefabs == null || resultPrefabs.Length == 0) Debug.LogWarning("你没有设置 Result Prefabs 数组！");
            }

            if (successCount >= 3)
            {
                // 成功3次，图片彻底消失
                gameObject.SetActive(false); 
                // 或者用 Destroy(gameObject);
            }
            else
            {
                // 没到3次：回到原位
                transform.position = startPos;
                
                // 颜色减淡（降低Alpha透明度，每次降低 0.25）
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a -= 0.25f; 
                    spriteRenderer.color = c;
                }
            }
    }

    // 【新增方法】重置该图片的进度和颜色
    public void ResetProgress()
    {
        successCount = 0;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        // 关键：确保如果它之前因为3次成功被隐藏了，现在把它重新显示出来，并放回原位
        transform.position = startPos;
        gameObject.SetActive(true);
    }

    // 【新增方法】清空在目标点生成的物体并重置拖拽物体
    public void ClearSpawnedAndReset()
    {
        // 如果在目标点生成了图（即拖拽成功后实例化出的图片），这里进行销毁
        if (spawnPoint != null && spawnedObjsDict.ContainsKey(spawnPoint))
        {
            if (spawnedObjsDict[spawnPoint] != null)
            {
                Destroy(spawnedObjsDict[spawnPoint]);
            }
            spawnedObjsDict.Remove(spawnPoint);
            
            // 同时将本被拖拽图从占据字典中移除
            if (lastDragDict.ContainsKey(spawnPoint) && lastDragDict[spawnPoint] == this)
            {
                lastDragDict.Remove(spawnPoint);
            }
        }
        
        // 恢复拖拽图片的初始状态（回到起点、恢复次数、恢复颜色并将自身激活）
        ResetProgress();
    }

    // 【新增可视化】在 Unity 编辑器里画一个圈，让你能直观看到判定的范围有多大！
    private void OnDrawGizmosSelected()
    {
        if (correctTrans != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f); // 半透明绿色
            Gizmos.DrawWireSphere(correctTrans.position, matchDistance);
            
            // 【重要排错】画一条红线：把“当前物体”和“代码认准的目标”连起来！
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, correctTrans.position);
        }

        if (spawnPoint != null)
        {
            Gizmos.color = new Color(0, 0, 1, 0.5f); // 半透明蓝色表示生成位置
            Gizmos.DrawWireCube(spawnPoint.position, customSpawnScale);
            
            // 画一条黄线：把“判定位置”和“最终生成位置”连起来！
            if (correctTrans != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(correctTrans.position, spawnPoint.position);
            }
        }
    }
}
