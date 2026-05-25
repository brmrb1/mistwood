using UnityEngine;
using TMPro; 
using System.Text.RegularExpressions;
using System.Collections.Generic;

// 专门处理“弹窗提示 -> 转为常驻任务”的系统
public class TaskSystem : MonoBehaviour
{
    [Header("配置")]
    public TextAsset taskCSV;               // 任务系统的CSV文件

    [Header("弹窗提示UI (看完消失)")]
    public GameObject popupPanel;         // 画面中间的弹窗底图
    public TMP_Text popupText;              // 弹窗里显示的提示文字

    [Header("常驻任务UI (一直挂在屏幕上)")]
    public GameObject persistentPanel;      // 比如挂在右上角的任务底图
    public TMP_Text persistentText;         // 右上角的任务提示文字

    // 用于在内存里记录从CSV读取出来的任务信息
    private struct TaskData
    {
        public string popupMsg;      // 对应 CSV 第2列: 弹窗文字
        public string persistentMsg; // 对应 CSV 第3列: 常驻任务文字
    }
    private Dictionary<string, TaskData> taskDict = new Dictionary<string, TaskData>();

    private float enableTime;

    private void Awake()
    {
        LoadTaskCSV();
    }

    // 解析任务的CSV
    private void LoadTaskCSV()
    {
        taskDict.Clear();
        if (taskCSV == null) return;

        string[] rows = Regex.Split(taskCSV.text, "\r\n|\r|\n");
        // 从第二行开始 (第一行是表头: 任务ID, 弹窗提示, 常驻任务)
        for (int i = 1; i < rows.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i])) continue;
            
            string[] values = Regex.Split(rows[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            if (values.Length >= 3)
            {
                string tID = values[0].Trim('\"', ' ');
                TaskData tData = new TaskData
                {
                    popupMsg = values[1].Trim('\"', ' ').Replace("\\n", "\n"),
                    persistentMsg = values[2].Trim('\"', ' ').Replace("\\n", "\n")
                };
                taskDict[tID] = tData;
            }
        }
    }

    // 由 DialogueManager 唤醒时调用（带参数）
    public void StartInteraction(string taskID)
    {
        enableTime = Time.time;
        Debug.Log("【TaskSystem】接收到任务指令, ID: " + taskID);

        // 如果传过来的指令是 Clear，意思是玩家完成任务了，我们要清空常驻UI
        if (taskID.ToLower() == "clear")
        {
            persistentPanel.SetActive(false);
            gameObject.SetActive(false); 
            // 恢复剧情对话
            if (DialogueManager.Instance != null) 
            {
                DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
            }
            return;
        }

        // 如果是派发新任务
        if (taskDict.ContainsKey(taskID))
        {
            // 配置文字
            if (popupText != null) popupText.text = taskDict[taskID].popupMsg;
            if (persistentText != null) persistentText.text = taskDict[taskID].persistentMsg;

            // 显示正中央的弹窗，此时常驻任务UI先不显示（玩家点完弹窗才显示）
            if (popupPanel != null) popupPanel.SetActive(true);
            if (persistentPanel != null) persistentPanel.SetActive(false);
            
            gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("【TaskSystem】在任务CSV里找不到对应的任务ID: " + taskID);
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
            }
        }
    }

    // 当玩家点击屏幕 / 或者你绑定到弹窗中间的“确定按钮”上
    public void OnClickPopupComplete()
    {
        // 1. 关闭中间的弹窗
        if (popupPanel != null) popupPanel.SetActive(false);
        
        // 2. 显示右上角的常驻任务提示
        if (persistentPanel != null) persistentPanel.SetActive(true);

        // 3. 告诉对话系统：延迟一帧恢复剧情
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
        }
    }

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
        // 增设 0.1 秒前摇保护
        if (popupPanel != null && popupPanel.activeInHierarchy && Input.GetMouseButtonDown(0) && Time.time - enableTime > 0.1f)
        {
            OnClickPopupComplete();
        }
    }
}