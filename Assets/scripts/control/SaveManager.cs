using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveManager : MonoBehaviour
{
    [Header("存档界面设置")]
    public GameObject savePanel; // 存档的弹窗面板
    public Transform saveContentParent; // ScrollView 下的 Content 节点
    public GameObject saveSlotPrefab; // 每一条存档 UI 的预制体 (Prefab)
    public int maxSaveSlots = 10; // 最大存档数量

    [Header("存档提示界面")]
    public GameObject hasSavePromptPanel; // 面板带有: 读取、覆盖、取消
    public GameObject noSavePromptPanel;  // 面板带有: 是、否

    [Header("UI点击音效")]
    public AudioClip uiClickSfx;

    private int currentSelectedSlot = -1;
    private TMPro.TMP_Text currentSelectedTextComp = null;

    // --- 存档相关功能 ---

    // 绑定至 打开存档界面的按钮
    public void OpenSavePanel()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        if (savePanel != null)
        {
            savePanel.SetActive(!savePanel.activeSelf);
            
            if (savePanel.activeSelf)
            {
                RefreshSaveSlots(); // 当打开时刷新存档列表
            }
        }
    }

    // 刷新显示 Content 里的存档栏位
    private void RefreshSaveSlots()
    {
        if (saveContentParent == null || saveSlotPrefab == null) return;

        // 清空旧的 UI 节点
        foreach (Transform child in saveContentParent)
        {
            Destroy(child.gameObject);
        }

        // 动态生成存档条目
        for (int i = 1; i <= maxSaveSlots; i++)
        {
            GameObject newSlot = Instantiate(saveSlotPrefab, saveContentParent);
            
            // 查找上面的文本组件(假设用的是 TextMeshPro)
            TMPro.TMP_Text textComp = newSlot.GetComponentInChildren<TMPro.TMP_Text>();
            if (textComp != null)
            {
                // 判断该位置是否已有存档
                if (PlayerPrefs.HasKey("SaveTime_" + i))
                {
                    // 游玩的时间
                    string playTimeString = PlayerPrefs.GetString("PlayTimeString_" + i, "00:00:00");
                    textComp.text = "存档 " + i + "  |  游戏时长: " + playTimeString;
                }
                else
                {
                    textComp.text = "存档 " + i + "  |  空存档";
                }
            }
            
            // 查找并在预制体上的 Button 绑定点击事件
            Button btn = newSlot.GetComponent<Button>();
            if (btn != null)
            {
                int slotIndex = i; // 缓存变量给闭包使用
                btn.onClick.AddListener(() => OnSaveSlotClicked(slotIndex, textComp));
            }
        }
    }

    // 点击某个存档位的逻辑
    private void OnSaveSlotClicked(int slotIndex, TMPro.TMP_Text textComp)
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        Debug.Log("点击了存档按钮: " + slotIndex); // 打印日志以确认按钮确实被点击了

        currentSelectedSlot = slotIndex;
        currentSelectedTextComp = textComp;

        if (PlayerPrefs.HasKey("SaveTime_" + slotIndex))
        {
            // 有存档信息，弹出“读取或覆盖”界面
            if (hasSavePromptPanel != null) 
            {
                hasSavePromptPanel.SetActive(true);
            }
            else 
            {
                Debug.LogError("hasSavePromptPanel 没在 Inspector 里赋值！");
            }
        }
        else
        {
            // 无存档信息，弹出“是否储存”界面
            if (noSavePromptPanel != null) 
            {
                noSavePromptPanel.SetActive(true);
            }
            else 
            {
                Debug.LogError("noSavePromptPanel 没在 Inspector 里赋值！");
            }
        }
    }

    // --- 在带有存档的弹窗上绑定的按钮事件 ---

    public void ConfirmLoadGame()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        Debug.Log("进入了 ConfirmLoadGame 方法");
        if (currentSelectedSlot == -1) 
        {
            Debug.LogWarning("currentSelectedSlot 为 -1，强制返回，没往下跑。");
            return;
        }
        
        Debug.Log("读取存档进度位: " + currentSelectedSlot);
        
        // 1. 获取累加的游玩时长，作为此次进游戏的起始基础时间
        float savedPlayTime = PlayerPrefs.GetFloat("PlayTimeFloat_" + currentSelectedSlot, 0f);
        PlayerPrefs.SetFloat("LoadedPlayTime", savedPlayTime);
        
        // 2. 读取之前保存的场景名称，如果没有则默认读取第一关
        string savedScene = PlayerPrefs.GetString("SavedScene_" + currentSelectedSlot, "start"); 
        
        // 3. 读取对话进度存入过渡键值，供目标场景的 DialogueManager 脚本读取
        // 使用 String 类型存储 ID 以兼容非数字 ID
        string savedDialogID = PlayerPrefs.GetString("SavedDialogID_" + currentSelectedSlot, "0");
        PlayerPrefs.SetString("TargetLoadDialogID", savedDialogID);

        // 如果存了自定义剧本名，也传下去
        string savedCSV = PlayerPrefs.GetString("SavedCSVName_" + currentSelectedSlot, "");
        if (!string.IsNullOrEmpty(savedCSV))
        {
            PlayerPrefs.SetString("ResumeCSVName", savedCSV);
        }

        // 【新增】告诉下个场景系统：我们目前读取的是哪个槽位的数据
        PlayerPrefs.SetInt("TargetLoadSlot", currentSelectedSlot);

        if (hasSavePromptPanel != null) hasSavePromptPanel.SetActive(false);
        // 如果想让整个存档界面也一起消失，取消注释下面这行
        if (savePanel != null) savePanel.SetActive(false);

        // 3. 开始切换场景恢复进度
        UnityEngine.SceneManagement.SceneManager.LoadScene(savedScene);
    }

    public void ConfirmOverwriteGame()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        Debug.Log("进入了 ConfirmOverwriteGame 方法");
        if (currentSelectedSlot == -1) 
        {
            Debug.LogWarning("currentSelectedSlot 为 -1，强制返回，没往下跑。");
            return;
        }
        
        ExecuteSave(currentSelectedSlot, currentSelectedTextComp);
        if (hasSavePromptPanel != null) hasSavePromptPanel.SetActive(false);
    }

    public void DeleteSelectedSave()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        Debug.Log("进入了 DeleteSelectedSave 方法");
        if (currentSelectedSlot == -1) 
        {
            Debug.LogWarning("currentSelectedSlot 为 -1，强制返回，没往下跑。");
            return;
        }

        // 删除该档位的所有数据
        PlayerPrefs.DeleteKey("SaveTime_" + currentSelectedSlot);
        PlayerPrefs.DeleteKey("PlayTimeString_" + currentSelectedSlot);
        PlayerPrefs.DeleteKey("PlayTimeFloat_" + currentSelectedSlot);
        PlayerPrefs.DeleteKey("SavedScene_" + currentSelectedSlot);
        PlayerPrefs.DeleteKey("SavedDialogID_" + currentSelectedSlot);
        PlayerPrefs.DeleteKey("SavedCSVName_" + currentSelectedSlot);

        // 【新增修复】清除当前可能存在的加载标记，防止误读
        if (PlayerPrefs.GetInt("TargetLoadSlot", -1) == currentSelectedSlot)
        {
            PlayerPrefs.DeleteKey("TargetLoadSlot");
        }

        PlayerPrefs.Save();

        // 隐藏面板
        if (hasSavePromptPanel != null) hasSavePromptPanel.SetActive(false);

        // 刷新存档界面
        RefreshSaveSlots();

        currentSelectedSlot = -1;
        currentSelectedTextComp = null;
    }

    // --- 在无存档的弹窗上绑定的按钮事件 ---

    public void ConfirmSaveNewGame()
    {
        Debug.Log("进入了 ConfirmSaveNewGame 方法");
        if (currentSelectedSlot == -1) 
        {
            Debug.LogWarning("currentSelectedSlot 为 -1，强制返回，没往下跑。");
            return;
        }
        
        ExecuteSave(currentSelectedSlot, currentSelectedTextComp);
        if (noSavePromptPanel != null) noSavePromptPanel.SetActive(false);
    }
    
    // --- 共用的“取消/否”按钮事件 ---

    public void CancelSavePrompt()
    {
        Debug.Log("进入了 CancelSavePrompt 方法");
        if (hasSavePromptPanel != null) hasSavePromptPanel.SetActive(false);
        else Debug.LogError("hasSavePromptPanel 为 null!");
        
        if (noSavePromptPanel != null) noSavePromptPanel.SetActive(false);
        else Debug.LogError("noSavePromptPanel 为 null!");
        
        currentSelectedSlot = -1;
        currentSelectedTextComp = null;
    }

    // 实际执行存档逻辑的方法
    private void ExecuteSave(int slotIndex, TMPro.TMP_Text textComp)
    {
        float loadedPlayTime = PlayerPrefs.GetFloat("LoadedPlayTime", 0f);
        float sessionPlayTime = Time.timeSinceLevelLoad; 
        float totalPlayTime = loadedPlayTime + sessionPlayTime;
        
        System.TimeSpan ts = System.TimeSpan.FromSeconds(totalPlayTime);
        string playTimeString = string.Format("{0:D2}:{1:D2}:{2:D2}", ts.Hours, ts.Minutes, ts.Seconds);

        PlayerPrefs.SetString("SaveTime_" + slotIndex, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.SetFloat("PlayTimeFloat_" + slotIndex, totalPlayTime);
        PlayerPrefs.SetString("PlayTimeString_" + slotIndex, playTimeString);
        
        // 【核心】记录当前场景名字作为“进度”
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        PlayerPrefs.SetString("SavedScene_" + slotIndex, currentSceneName);

        // 如果场景中有对话系统，保存当前对话进度
        if (DialogueManager.Instance != null)
        {
            // 通过接口获取当前正在进行的 ID 和 CSV 文件名
            string currentID = DialogueManager.Instance.GetCurrentLineID();
            string currentCSV = DialogueManager.Instance.GetCurrentCSVName();

            // 如果当前是在挂起（小游戏中），DialogueManager.Instance.GetCurrentLineID() 拿到的
            // 就是触发该小游戏的 EVENT 所在的行 ID。读档时，会重新触发 TriggerGameEvent。
            PlayerPrefs.SetString("SavedDialogID_" + slotIndex, currentID);
            PlayerPrefs.SetString("SavedCSVName_" + slotIndex, currentCSV);
            
            Debug.Log($"【存档系统】已记录对话进度: ID={currentID}, CSV={currentCSV}");
        }
        else
        {
            PlayerPrefs.SetString("SavedDialogID_" + slotIndex, "0");
        }

        // 【新增】保存场景中所有能够拖拽生存预制体的物品进度状态
        dragright[] allDrags = FindObjectsOfType<dragright>();
        foreach (dragright d in allDrags)
        {
            // 通过物体的名字作为唯一标识符存储它们各自“成功生成的次数”
            PlayerPrefs.SetInt("DragProgress_" + slotIndex + "_" + d.gameObject.name, d.CurrentSuccessCount);
        }

        // 【新增】保存 CanMoveEvent 的位移状态
        CanMoveEvent[] allCanMoves = FindObjectsOfType<CanMoveEvent>();
        foreach (var cm in allCanMoves)
        {
            // 【修复】保存键时加入当前场景名，解决多关卡独立状态的问题
            PlayerPrefs.SetInt("CanMove_" + slotIndex + "_" + currentSceneName + "_" + cm.gameObject.name, cm.HasMoved ? 1 : 0);
        }

        // 【新增】如果是解谜场景，通知检验系统去把过去的各种历史结算快照序列化到 PlayerPrefs
        PuzzleChecker checker = FindObjectOfType<PuzzleChecker>();
        if (checker != null)
        {
            checker.SaveHistoryToPrefs(slotIndex);
        }
        
        PlayerPrefs.Save();
        
        if (textComp != null)
        {
            textComp.text = "存档 " + slotIndex + "  |  游戏时长: " + playTimeString;
        }
        
        Debug.Log("成功存入 存档位: " + slotIndex);
    }
}