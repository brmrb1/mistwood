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
    
    // 【修改】使用静态字典按“生成位置(spawnPoint)”来独立记录图片和状态
    // 只有拖到“同一个位置”的物品才会相互排斥和顶替
    private static Dictionary<Transform, GameObject> spawnedObjsDict = new Dictionary<Transform, GameObject>();
    private static Dictionary<Transform, dragright> lastDragDict = new Dictionary<Transform, dragright>();

    // 【新增】给外部（如判题脚本）提供一个公开的方法来获取当前所有坑位的状态
    public static Dictionary<Transform, dragright> GetCurrentPlacements()
    {
        return lastDragDict;
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
    }

    private void OnMouseExit()
    {
        transform.localScale = originalScale;
    }

    // 拖拽过程
    private void OnMouseDrag()
    {
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
        if (successCount >= 3 || correctTrans == null) return;

        // 计算当前位置和目标位置的距离（忽略Z轴）
        float distance = Vector2.Distance(transform.position, correctTrans.position);

        if (distance <= matchDistance)
        {
            // --- 拖到了指定位置 ---

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
                    // 不再强制挂载到 spawnPoint 下，避免本地坐标和缩放问题，直接在世界坐标系下生成
                    GameObject newlySpawned = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
                    
                    // 使用 spawnPoint（生成点）的真实位置，但将Z轴和当前被拖拽的物体保持一致，防止跑到不正确的前后层级
                    Vector3 newPos = spawnPoint.position;
                    newPos.z = transform.position.z;
                    // 重新赋值给新生成的物体
                    newlySpawned.transform.position = newPos;
                    
                    // 强制设为你指定的固定大小，这样你在面板里填多少，它生出来就是多大！
                    newlySpawned.transform.localScale = customSpawnScale;

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
        else
        {
            // --- 没拖到指定位置，直接回到原位 ---
            transform.position = startPos;
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

    // 【新增可视化】在 Unity 编辑器里画一个圈，让你能直观看到判定的范围有多大！
    private void OnDrawGizmosSelected()
    {
        if (correctTrans != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f); // 半透明绿色
            Gizmos.DrawWireSphere(correctTrans.position, matchDistance);
        }
    }
}
