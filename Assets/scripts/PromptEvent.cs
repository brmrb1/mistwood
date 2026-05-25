using UnityEngine;
using TMPro; // 因为你使用了 TextMeshPro，所以得引入这个命名空间
using System.Text.RegularExpressions;
using System.Collections.Generic;

// 独立 CSV 控制的多段提示事件
public class PromptEvent : MonoBehaviour
{
    [Header("配置")]
    public TextAsset promptCSV;     // 这个事件专门的CSV文件
    public TMP_Text promptText;     // 显示提示文字的文本容器 (TextMeshPro)
    
    private List<string> linesData = new List<string>();
    private int currentIndex = 0;
    private float enableTime;

    // 被对话管理器自动呼叫的启动入口
    public void StartInteraction()
    {
        Debug.Log("【PromptEvent】多段提示事件开始！");
        gameObject.SetActive(true);
        enableTime = Time.time;
        
        // 每次启动时重新读取一遍 CSV
        LoadCSV();
        
        // 从第一句开始显示
        currentIndex = 0;
        ShowNextLine();
    }

    // 加载并解析专属的CSV
    private void LoadCSV()
    {
        linesData.Clear();
        if (promptCSV == null)
        {
            Debug.LogError("【PromptEvent】没有指定提示专用的CSV文件！");
            return;
        }

        // 按行分割
        string[] rows = Regex.Split(promptCSV.text, "\r\n|\r|\n");
        
        // 从第二行开始遍历（第一行通常是表头，如：ID,内容）
        for (int i = 1; i < rows.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i])) continue;
            
            // 按照逗号分割，但忽略双引号里的逗号
            string[] values = Regex.Split(rows[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            
            // 假设在这个专门的CSV里，第1列(索引0)是ID，第2列(索引1)是内容
            if (values.Length >= 2)
            {
                // 去除可能自带的双引号
                string content = values[1].Trim('\"', ' ');
                linesData.Add(content);
            }
        }
    }

    // 更新画面直接点击屏幕，或者点按钮，都可以调用这个方法跳下一句
    private void Update()
    {
        // 如果这个提示面板在显示，并且玩家按下了鼠标/点了屏幕，就下一句
        // 增加0.1秒的CD缓冲期，防止由于同帧/短时间内的连点导致新出来的面板被瞬间穿透点掉
        if (gameObject.activeInHierarchy && Input.GetMouseButtonDown(0) && Time.time - enableTime > 0.1f)
        {
            // 延迟一点点防止点出来的瞬间就被当成点屏幕了
            currentIndex++;
            ShowNextLine();
        }
    }

    // 显示当前索引对应的台词
    private void ShowNextLine()
    {
        // 如果文本已经放完了
        if (currentIndex >= linesData.Count)
        {
            EndInteraction();
            return;
        }

        // 显示当前行的文本
        if (promptText != null)
        {
            // 将文本中的转义换行符 \n 变回真正的环境换行
            string realText = linesData[currentIndex].Replace("\\n", "\n");
            promptText.text = realText;
        }
    }

    // 事件结束，清场并恢复主对话
    private void EndInteraction()
    {
        Debug.Log("【PromptEvent】多段提示放完了，恢复主线剧情。");
        gameObject.SetActive(false); // 隐藏提示面板
        
        // 恢复主干对话剧情
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
        }
    }

    // 延迟一帧恢复对话，防止当前帧的点击继续传导给下一个刚出现的物体
    private System.Collections.IEnumerator ResumeDialogueRoutine()
    {
        yield return null;
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }
}
