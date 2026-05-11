using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 引入场景管理，用于重新开始游戏

[System.Serializable]
public class PuzzleAnswer
{
    [Header("需要检查的生成点(Spawn Point)")]
    public Transform targetSpawnPoint;      
    
    [Header("期望这个点上放什么(把你希望的图片直接拖进来)")]
    public dragright expectedItem;    

    [Header("期望它是第几次形态(1 2 或 3)")]
    [Range(1, 3)]
    public int expectedCount;         
}

public class PuzzleChecker : MonoBehaviour
{
    [Header("在下面配置你的标准答案")]
    public List<PuzzleAnswer> correctAnswers;

    [Header("答题反馈位置")]
    public Transform feedbackSpawnPos1;
    public Transform feedbackSpawnPos2;

    [Header("位置1的5种反馈图片(例如：对0个 到 对4个)")]
    public GameObject[] feedbackPrefabs1;

    [Header("位置2的5种反馈图片(同上对应)")]
    public GameObject[] feedbackPrefabs2;

    [Header("统一控制反馈图片的生成大小")]
    public Vector3 uniformFeedbackScale = new Vector3(1f, 1f, 1f);

    [Header("新增：数字文字反馈预制体(需要包含TextMeshPro或Text组件)")]
    public GameObject typeTextPrefab; // 负责显示种类正确的数字
    public GameObject doseTextPrefab; // 负责显示剂量/形态正确的数字

    [Header("新增：数字文字的生成位置与下移量")]
    public Transform textSpawnPos1;
    public Transform textSpawnPos2;
    [Tooltip("UI文字由于通常挂在UI层，如果需要单独往下偏移可以设置它，比如Y设为 -50")]
    public Vector3 textRoundOffset = new Vector3(0, -50f, 0);

    [Header("新增：滑动视图内容容器")]
    [Tooltip("如果你希望随次数增加让外框可滚动，把 Scroll View 的 Content 拖入此处")]
    public RectTransform scrollContent;

    [Header("多轮机制：每轮下移设置")]
    [Tooltip("每按一次判定按钮，整体往下偏移多少？(例如Y:-2)")]
    public Vector3 roundOffset = new Vector3(0, -2f, 0); 
    [Tooltip("把需要跟着下移的“空节点”拖进来（包含：所有的 Spawn Point、反馈位置、以及拖拽接收的目标点）")]
    public List<Transform> transformsToShiftDown;

    [Header("游戏进程与UI控制")]
    public int maxAttempts = 7;           // 最大挑战次数
    private int currentAttempt = 0;       // 当前已经是第几次挑战
    
    // 供外部获取当前挑战次数，以便同步下移位置
    public int GetCurrentAttempt()
    {
        return currentAttempt;
    }

    [Tooltip("全部答对时弹出的成功CG面板")]
    public GameObject successPanel;
    [Tooltip("7次机会用完后弹出的失败黑屏面板")]
    public GameObject failPanel;

    private Vector3 initialFb1Pos;
    private Vector3 initialFb2Pos;
    private GameObject currentFb1;
    private GameObject currentFb2;
    private float initialContentHeight;

    private void Start()
    {
        if (feedbackSpawnPos1 != null) initialFb1Pos = feedbackSpawnPos1.position;
        if (feedbackSpawnPos2 != null) initialFb2Pos = feedbackSpawnPos2.position;
        
        if (scrollContent != null)
        {
            initialContentHeight = scrollContent.sizeDelta.y;
        }
    }

    // 这个方法用来在 Unity 面板中绑定给 Button (OnClick) 事件
    public void CheckResult()
    {
        // 如果已经结束（胜利或失败），禁止继续判定
        if (currentAttempt >= maxAttempts || (successPanel != null && successPanel.activeSelf))
        {
            return; 
        }

        currentAttempt++; // 增加一次挑战次数
        Debug.Log($"当前是第 {currentAttempt} 次挑战，剩余 {maxAttempts - currentAttempt} 次机会。");

        // 1. 获取全局正在被占据的所有的坑位和上面的物品
        var currentPlacements = dragright.GetCurrentPlacements();
        
        int correctTypeCount = 0; // 种类正确的数量（位置1对应的判定）
        int correctFormCount = 0; // 种类和形态都正确的数量（位置2对应的判定）

        Debug.Log("=== 开始核对答案 ===");
        Debug.Log($"配置的标准答案数量：{correctAnswers.Count} 个");
        Debug.Log($"前场上记录的物品数量：{currentPlacements.Count} 个");

        // 2. 遍历所有的标准答案进行核对
        foreach (var answer in correctAnswers)
        {
            if (answer.targetSpawnPoint == null)
            {
                Debug.LogWarning("注意：有一个标准答案没有配置目标生成点(targetSpawnPoint)，被跳过。");
                continue;
            }

            // 如果这个坑位上根本没放东西
            if (!currentPlacements.ContainsKey(answer.targetSpawnPoint))
            {
                Debug.Log($"坑位 [{answer.targetSpawnPoint.name}]：上方没有放置任何物品，算错。");
                continue; // 没放东西算错，直接跳过看下一个
            }

            // 获取目前放在这个坑位上的物体 (可能带(Clone)后缀)
            dragright itemOnPoint = currentPlacements[answer.targetSpawnPoint];

            if (itemOnPoint == null || answer.expectedItem == null) continue;

            // 核对“种类”是否正确 (通过名称对比)
            // 修改：彻底剥离预制体可能带上的 "(加数字)" 或 "(Clone)" 后缀
            // 比如 "f1 (1)(Clone)" -> 提取核心名字 "f1"，再互相比较
            string currentItemName = itemOnPoint.gameObject.name.Split('(')[0].Trim();
            string expectedItemName = answer.expectedItem.gameObject.name.Split('(')[0].Trim();

            bool typeMatch = (currentItemName == expectedItemName);
            bool formMatch = false;

            if (typeMatch)
            {
                correctTypeCount++; // 种类正确，记录
                
                // 在种类正确的前提下，再核对“形态（次数）”是否也正确
                if (itemOnPoint.CurrentSuccessCount == answer.expectedCount)
                {
                    formMatch = true;
                    correctFormCount++; // 种类和形态都正确，记录
                }
            }

            Debug.Log($"坑位 [{answer.targetSpawnPoint.name}] 的判定详情：" + 
                      $"\n   放置的是: {currentItemName} (其形态次数: {itemOnPoint.CurrentSuccessCount})" +
                      $"\n   期待的是: {expectedItemName} (其形态次数: {answer.expectedCount})" +
                      $"\n   当前坑位对比结果 -> 种类是否一致: {typeMatch} | 形态是否一致: {formMatch}");
        }
        Debug.Log("=== 核对结束 ===");

        // 4. 判断胜利或失败的终极条件
        // 动态根据你配置的题目数量来判断是否“全对”，而不是写死固定的数字(如3或4)。
        // 只要答对的数量等于你配置的标准答案总数，就算过关
        bool isAllCorrect = (correctTypeCount == correctAnswers.Count && correctFormCount == correctAnswers.Count);

        if (isAllCorrect)
        {
            Debug.Log("拼图全部正确！弹出成功CG！");
            // 生成最后的正确反馈图标
            int maxIdx1 = Mathf.Clamp(correctTypeCount, 0, feedbackPrefabs1 != null ? feedbackPrefabs1.Length - 1 : 0);
            int maxIdx2 = Mathf.Clamp(correctFormCount, 0, feedbackPrefabs2 != null ? feedbackPrefabs2.Length - 1 : 0);
            
            if (currentFb1 != null) Destroy(currentFb1);
            if (feedbackPrefabs1 != null && feedbackPrefabs1.Length > 0 && feedbackPrefabs1[maxIdx1] != null)
            {
                currentFb1 = Instantiate(feedbackPrefabs1[maxIdx1], initialFb1Pos, Quaternion.identity);
                currentFb1.transform.localScale = uniformFeedbackScale;
            }

            if (currentFb2 != null) Destroy(currentFb2);
            if (feedbackPrefabs2 != null && feedbackPrefabs2.Length > 0 && feedbackPrefabs2[maxIdx2] != null)
            {
                currentFb2 = Instantiate(feedbackPrefabs2[maxIdx2], initialFb2Pos, Quaternion.identity);
                currentFb2.transform.localScale = uniformFeedbackScale;
            }
            
            SpawnTextFeedback(correctTypeCount, correctFormCount);

            if (successPanel != null) successPanel.SetActive(true);
            return; // 胜出了就不再执行后面的下移操作
        }

        if (currentAttempt >= maxAttempts)
        {
            Debug.Log("挑战次数达到上限，游戏失败！弹出黑屏重试面板。");
            if (failPanel != null) failPanel.SetActive(true);
            return; // 失败了也不再执行后面的下移操作
        }

        // 5. 根据答对的数量（0,1,2,3,4），决定生成哪一张反馈图片
        // 限制防错：确保不会超出数组范围 (假设最多只有5种形态对应下标0~4)
        int feedbackIndex1 = Mathf.Clamp(correctTypeCount, 0, 4);
        int feedbackIndex2 = Mathf.Clamp(correctFormCount, 0, 4);

        Debug.Log($"结算完成！玩家共答对了 {correctTypeCount} 个各类别的图片，以及 {correctFormCount} 个正确形态的图片。");

        if (currentFb1 != null) Destroy(currentFb1);
        if (feedbackPrefabs1 != null && feedbackIndex1 < feedbackPrefabs1.Length && feedbackPrefabs1[feedbackIndex1] != null)
        {
            currentFb1 = Instantiate(feedbackPrefabs1[feedbackIndex1], initialFb1Pos, Quaternion.identity);
            currentFb1.transform.localScale = uniformFeedbackScale;
        }
                
        if (currentFb2 != null) Destroy(currentFb2);
        if (feedbackPrefabs2 != null && feedbackIndex2 < feedbackPrefabs2.Length && feedbackPrefabs2[feedbackIndex2] != null)
        {
            currentFb2 = Instantiate(feedbackPrefabs2[feedbackIndex2], initialFb2Pos, Quaternion.identity);
            currentFb2.transform.localScale = uniformFeedbackScale;
        }

        SpawnTextFeedback(correctTypeCount, correctFormCount);

        // 5. 准备开启新的一轮！
        
        // (1) 通知所有拖拽物品退回原位，且代码“失忆”（不再记录上一轮的生成图，让上一轮的图留作历史）
        dragright.StartNewRound();

        // (2) 把所有指定的工作区标志点，一口气整体往下平移
        if (transformsToShiftDown != null)
        {
            foreach (var t in transformsToShiftDown)
            {
                if (t != null)
                {
                    t.position += roundOffset;
                }
            }
        }
    }

    // 【新增】重新开始游戏，绑定给“重新开始按钮”的 OnClick 事件
    public void RestartGame()
    {
        // 因为我们在拖拽脚本里用了静态字典，重新开始时必须清理一下！
        dragright.StartNewRound(); 
        
        // 重新加载当前场景，彻底重置一切
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void SpawnTextFeedback(int typeCount, int doseCount)
    {
        // 根据当前挑战次数(currentAttempt)，独立计算文字该下移多少次
        Vector3 currentTextOffset = textRoundOffset * (currentAttempt - 1);

        // --- 核心：如果设置了 Scroll View，自动撑开它的长度 ---
        if (scrollContent != null)
        {
            // 按照下移跨度，累加出这一轮需要多长的额外滑动空间
            float extraHeight = Mathf.Abs(textRoundOffset.y) * currentAttempt;
            if (initialContentHeight + extraHeight > scrollContent.sizeDelta.y)
            {
                // 把 Content（容器）拉长，滑动条才会出现并生效
                scrollContent.sizeDelta = new Vector2(scrollContent.sizeDelta.x, initialContentHeight + extraHeight);
            }
        }

        if (typeTextPrefab != null && textSpawnPos1 != null)
        {
            // 如果指派了滚动容器，将其作为父物体；否则用普通的父物体
            Transform targetParent = scrollContent != null ? scrollContent : textSpawnPos1.parent;
            GameObject typeTxtObj = Instantiate(typeTextPrefab, targetParent);
            
            typeTxtObj.transform.position = textSpawnPos1.position + currentTextOffset;
            typeTxtObj.transform.localScale = typeTextPrefab.transform.localScale;
            typeTxtObj.SetActive(true);

            var tmp = typeTxtObj.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) tmp.text = typeCount.ToString();
            else
            {
                var txt = typeTxtObj.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (txt != null) txt.text = typeCount.ToString();
            }
        }
        
        if (doseTextPrefab != null && textSpawnPos2 != null)
        {
            Transform targetParent = scrollContent != null ? scrollContent : textSpawnPos2.parent;
            GameObject doseTxtObj = Instantiate(doseTextPrefab, targetParent);
            
            doseTxtObj.transform.position = textSpawnPos2.position + currentTextOffset;
            doseTxtObj.transform.localScale = doseTextPrefab.transform.localScale;
            doseTxtObj.SetActive(true);

            var tmp = doseTxtObj.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) tmp.text = doseCount.ToString();
            else
            {
                var txt = doseTxtObj.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (txt != null) txt.text = doseCount.ToString();
            }
        }
    }
}

