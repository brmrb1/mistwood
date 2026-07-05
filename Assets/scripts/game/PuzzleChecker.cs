using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 引入场景管理，用于重新开始游戏

[System.Serializable]
public class PlacementRecord
{
    public string spawnPointName;
    public string dragrightName;
    public int successCount;
}

[System.Serializable]
public class RoundRecord
{
    public List<PlacementRecord> placements = new List<PlacementRecord>();
}

[System.Serializable]
public class PuzzleHistoryData
{
    public List<RoundRecord> rounds = new List<RoundRecord>();
}

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

    [Header("直接生成在此节点下(拖入canmove)")]
    public Transform feedbackParent;

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
    [Tooltip("【新增】如果有填入正确答案的坑位没放东西，点击结算弹出的提示面板")]
    public GameObject emptyPromptPanel;

    [Header("音效")]
    public AudioClip successSfx;

    private Vector3 initialFb1Pos;
    private Vector3 initialFb2Pos;
    private GameObject currentFb1;
    private GameObject currentFb2;
    private float initialContentHeight;

    // 【新增】保存的历史记录
    private PuzzleHistoryData historyData = new PuzzleHistoryData();

    // 【新增】保存需要下移的工作区的初始坐标
    private Dictionary<Transform, Vector3> initialShiftPositions = new Dictionary<Transform, Vector3>();

    private void Awake()
    {
        if (feedbackSpawnPos1 != null) initialFb1Pos = feedbackSpawnPos1.position;
        if (feedbackSpawnPos2 != null) initialFb2Pos = feedbackSpawnPos2.position;
        
        if (scrollContent != null)
        {
            initialContentHeight = scrollContent.sizeDelta.y;
        }

        // 记录所有会被下移的物体的初始位置
        if (transformsToShiftDown != null)
        {
            foreach (var t in transformsToShiftDown)
            {
                if (t != null && !initialShiftPositions.ContainsKey(t))
                {
                    initialShiftPositions[t] = t.localPosition;
                }
            }
        }

        // --- 读取存档时的结算历史恢复 ---
        if (PlayerPrefs.HasKey("TargetLoadSlot"))
        {
            int slot = PlayerPrefs.GetInt("TargetLoadSlot");

            // 【核心逻辑优化】读档前先检查“存档是否有效”。如果没有该档位的时间记录，说明是空档或已删除，拒绝加载数据。
            if (PlayerPrefs.HasKey("SaveTime_" + slot))
            {
                RestoreHistory(slot);
            }
        }
    }

    // 【新增】关闭未放置满提示面板的按钮事件
    public void CloseEmptyPrompt()
    {
        if (emptyPromptPanel != null)
        {
            emptyPromptPanel.SetActive(false);
        }
    }

    public void CheckResult()
    {
        // 如果已经结束（胜利或失败），禁止继续判定
        if (currentAttempt >= maxAttempts || (successPanel != null && successPanel.activeSelf))
        {
            return; 
        }

        // 1. 获取全局正在被占据的所有的坑位和上面的物品
        var currentPlacements = dragright.GetCurrentPlacements();

        // 【新增 1】检查是否配置的所有 targetSpawnPoint 坑位都被放满了
        bool allFilled = true;
        foreach (var answer in correctAnswers)
        {
            if (answer.targetSpawnPoint != null && !currentPlacements.ContainsKey(answer.targetSpawnPoint))
            {
                allFilled = false;
                break;
            }
        }

        if (!allFilled)
        {
            Debug.LogWarning("存在未放置拼图的坑位，无法结算！已弹出提示面板。");
            if (emptyPromptPanel != null) emptyPromptPanel.SetActive(true);
            return;
        }

        currentAttempt++; // 增加一次挑战次数
        Debug.Log($"当前是第 {currentAttempt} 次挑战，剩余 {maxAttempts - currentAttempt} 次机会。");
        
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
            PlaySuccessSfx();
            // 生成最后的正确反馈图标
            int maxIdx1 = Mathf.Clamp(correctTypeCount, 0, feedbackPrefabs1 != null ? feedbackPrefabs1.Length - 1 : 0);
            int maxIdx2 = Mathf.Clamp(correctFormCount, 0, feedbackPrefabs2 != null ? feedbackPrefabs2.Length - 1 : 0);
            
            if (currentFb1 != null) Destroy(currentFb1);
            if (feedbackPrefabs1 != null && feedbackPrefabs1.Length > 0 && feedbackPrefabs1[maxIdx1] != null)
            {
                currentFb1 = Instantiate(feedbackPrefabs1[maxIdx1], feedbackParent);
                currentFb1.transform.position = feedbackSpawnPos1.position; // 保持在占位点的位置
                currentFb1.transform.localPosition = new Vector3(currentFb1.transform.localPosition.x, currentFb1.transform.localPosition.y, 0f); // 强制Z轴为0防止被背景遮挡
                currentFb1.transform.localScale = feedbackSpawnPos1.localScale; // 【新增】强制使用定位占位物体的自身缩放比例
            }

            if (currentFb2 != null) Destroy(currentFb2);
            if (feedbackPrefabs2 != null && feedbackPrefabs2.Length > 0 && feedbackPrefabs2[maxIdx2] != null)
            {
                currentFb2 = Instantiate(feedbackPrefabs2[maxIdx2], feedbackParent);
                currentFb2.transform.position = feedbackSpawnPos2.position;
                currentFb2.transform.localPosition = new Vector3(currentFb2.transform.localPosition.x, currentFb2.transform.localPosition.y, 0f);
                currentFb2.transform.localScale = feedbackSpawnPos2.localScale; // 【新增】强制使用定位占位物体的自身缩放比例
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
            currentFb1 = Instantiate(feedbackPrefabs1[feedbackIndex1], feedbackParent);
            currentFb1.transform.position = feedbackSpawnPos1.position; // 保持在占位点的位置
            currentFb1.transform.localPosition = new Vector3(currentFb1.transform.localPosition.x, currentFb1.transform.localPosition.y, 0f); // 强制Z轴为0防止被背景遮挡
            currentFb1.transform.localScale = feedbackSpawnPos1.localScale; // 【新增】强制使用定位占位物体的自身缩放比例
        }
                
        if (currentFb2 != null) Destroy(currentFb2);
        if (feedbackPrefabs2 != null && feedbackIndex2 < feedbackPrefabs2.Length && feedbackPrefabs2[feedbackIndex2] != null)
        {
            currentFb2 = Instantiate(feedbackPrefabs2[feedbackIndex2], feedbackParent);
            currentFb2.transform.position = feedbackSpawnPos2.position;
            currentFb2.transform.localPosition = new Vector3(currentFb2.transform.localPosition.x, currentFb2.transform.localPosition.y, 0f);
            currentFb2.transform.localScale = feedbackSpawnPos2.localScale; // 【新增】强制使用定位占位物体的自身缩放比例
        }

        SpawnTextFeedback(correctTypeCount, correctFormCount);

        // 【新增】在清空记录前，把这一台当前的状态存入历史存档记录里
        RoundRecord newRecord = new RoundRecord();
        foreach (var kvp in currentPlacements)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                newRecord.placements.Add(new PlacementRecord()
                {
                    spawnPointName = kvp.Key.name,
                    dragrightName = kvp.Value.gameObject.name,
                    successCount = kvp.Value.CurrentSuccessCount
                });
            }
        }
        historyData.rounds.Add(newRecord);

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

    // 【新增】胜利后点击确认按钮，结束事件恢复剧情
    public void OnSuccessConfirm()
    {
        if (successPanel != null) successPanel.SetActive(false);
        
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }

    // 【修改】重新开始游戏，不重新加载场景，而是清除记录并让位置复原
    public void RestartGame()
    {
        if (failPanel != null) failPanel.SetActive(false);

        // 1. 通知 dragright 清理和复位当前活动的拖拽物品
        dragright.StartNewRound(); // 把刚才拖出去的归位
        dragright.ResetAllToInitial(); // 重置所有的 successCount 等

        // 2. 清除场上曾经所有克隆出来的摆放图案(拼图结果) 和 反馈文字/图片
        List<Transform> parentsToClean = new List<Transform>();
        if (scrollContent != null) parentsToClean.Add(scrollContent);
        if (feedbackParent != null) parentsToClean.Add(feedbackParent);
        if (textSpawnPos1 != null && textSpawnPos1.parent != null) parentsToClean.Add(textSpawnPos1.parent);
        if (textSpawnPos2 != null && textSpawnPos2.parent != null) parentsToClean.Add(textSpawnPos2.parent);
        
        foreach (var ans in correctAnswers)
        {
            if (ans.targetSpawnPoint != null)
                parentsToClean.Add(ans.targetSpawnPoint);
        }

        // 把所有 "(Clone)" 物体全部删掉
        foreach (Transform p in parentsToClean)
        {
            if (p == null) continue;
            for (int i = p.childCount - 1; i >= 0; i--)
            {
                Transform child = p.GetChild(i);
                if (child.name.Contains("(Clone)"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // 3. 把被下移的工作区移回原位
        if (transformsToShiftDown != null)
        {
            foreach (var t in transformsToShiftDown)
            {
                if (t != null && initialShiftPositions.ContainsKey(t))
                {
                    t.localPosition = initialShiftPositions[t];
                }
            }
        }

        // 4. 重置文本滑动框的长度
        if (scrollContent != null)
        {
            scrollContent.sizeDelta = new Vector2(scrollContent.sizeDelta.x, initialContentHeight);
        }

        // 5. 清除现有的 currentFb1 和 2 引用
        if (currentFb1 != null) Destroy(currentFb1);
        if (currentFb2 != null) Destroy(currentFb2);

        // 6. 清理历史存档数据
        historyData.rounds.Clear();
        currentAttempt = 0;
        
        Debug.Log("已重置拼图记录，重新开始挑战！");
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

    // --- 【新增】供外部（如SaveManager）调用以持久化历史 ---
    public void SaveHistoryToPrefs(int slotIndex)
    {
        string json = JsonUtility.ToJson(historyData);
        PlayerPrefs.SetString("PuzzleHistory_" + slotIndex, json);
        PlayerPrefs.SetInt("PuzzleAttempt_" + slotIndex, currentAttempt);
    }

    // --- 【新增】恢复历史视觉状态 ---
    private void RestoreHistory(int slotIndex)
    {
        string key = "PuzzleHistory_" + slotIndex;
        if (!PlayerPrefs.HasKey(key)) return;

        string json = PlayerPrefs.GetString(key);
        JsonUtility.FromJsonOverwrite(json, historyData);
        int savedAttempt = PlayerPrefs.GetInt("PuzzleAttempt_" + slotIndex, 0);

        if (historyData.rounds == null) return;

        // 收集场景里现存的所有 dragright 和 spawnPoint 等节点供名字查找
        dragright[] allDrags = FindObjectsOfType<dragright>(true);
        Dictionary<string, dragright> dragDict = new Dictionary<string, dragright>();
        foreach (var d in allDrags) dragDict[d.gameObject.name] = d;

        Dictionary<string, Transform> pointDict = new Dictionary<string, Transform>();
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        foreach (var t in allTransforms) 
        {
            if (!pointDict.ContainsKey(t.name)) pointDict[t.name] = t;
        }

        // 逐回合重新计算和生成！
        for (int i = 0; i < savedAttempt; i++)
        {
            if (i >= historyData.rounds.Count) break;

            RoundRecord record = historyData.rounds[i];
            
            int correctTypeCount = 0;
            int correctFormCount = 0;

            // 1. 根据历史记录在当时的坑位上重新实例化拖放物体
            foreach (var r in record.placements)
            {
                if (dragDict.ContainsKey(r.dragrightName) && pointDict.ContainsKey(r.spawnPointName))
                {
                    dragright d = dragDict[r.dragrightName];
                    Transform spt = pointDict[r.spawnPointName];

                    // 因为坑位还没有在这轮下移，所以直接原地生成
                    Transform targetContent = (scrollContent != null) ? scrollContent : spt;

                    // 为了展现当时的状态，把这件物品对应的正确次数的最终预制体实例化出来
                    // (由于它是一个私有字段的复用，也可以借用 public 接口，这里我们直接找 prefab)
                    // 注意这里的逻辑必须尽量吻合 dragright 最后的 resultPrefabs
                    int finalCount = r.successCount;
                    if (finalCount > 0)
                    {
                        // 这边为了简单，直接通过获取对应的 prefab 予以构建
                        System.Reflection.FieldInfo field = typeof(dragright).GetField("resultPrefabs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            GameObject[] prefabs = field.GetValue(d) as GameObject[];
                            if (prefabs != null && finalCount <= prefabs.Length)
                            {
                                GameObject p = prefabs[finalCount - 1];
                                if (p != null)
                                {
                                    GameObject cloned = Instantiate(p, spt.position, spt.rotation, targetContent);
                                    Vector3 newPos = spt.position;
                                    newPos.z = d.transform.position.z;
                                    cloned.transform.position = newPos;

                                    if (targetContent != null)
                                    {
                                        Vector3 customScale = new Vector3(1, 1, 1); 
                                        System.Reflection.FieldInfo scaleField = typeof(dragright).GetField("customSpawnScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        if (scaleField != null) customScale = (Vector3)scaleField.GetValue(d);
                                        
                                        Vector3 pScale = targetContent.lossyScale;
                                        cloned.transform.localScale = new Vector3(
                                            customScale.x / pScale.x,
                                            customScale.y / pScale.y,
                                            customScale.z / pScale.z
                                        );
                                    }

                                    Canvas canvasComp = cloned.GetComponent<Canvas>();
                                    if (canvasComp != null)
                                    {
                                        canvasComp.overrideSorting = false;
                                    }
                                }
                            }
                        }
                    }

                    // 重新算下这格对不对
                    foreach (var ans in correctAnswers)
                    {
                        if (ans.targetSpawnPoint != null && ans.targetSpawnPoint.name == spt.name)
                        {
                            string expectedName = ans.expectedItem.gameObject.name.Split('(')[0].Trim();
                            string currName = d.gameObject.name.Split('(')[0].Trim();
                            
                            if (currName == expectedName)
                            {
                                correctTypeCount++;
                                if (finalCount == ans.expectedCount)
                                {
                                    correctFormCount++;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            // 2. 模拟当轮回合执行对应的反馈文本生成
            int savedAttemptOrigin = currentAttempt; // 暂存
            currentAttempt = i + 1; // 临时变更为生成时的当时次次回合数
            SpawnTextFeedback(correctTypeCount, correctFormCount);
            currentAttempt = savedAttemptOrigin; // 变回来

            // 3. 把坑位往下移一层
            if (transformsToShiftDown != null)
            {
                foreach (var t in transformsToShiftDown)
                {
                    if (t != null) t.position += roundOffset;
                }
            }
        }

        // 把游戏本身的尝试次数更新为存档数量
        currentAttempt = savedAttempt;

        // 【如果答对了最后一次或者失败了】需要重新弹出相关判定（简单处理即可）。
    }

    private void PlaySuccessSfx()
    {
        if (successSfx == null)
        {
            return;
        }

        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(successSfx, playPosition);
    }
}

