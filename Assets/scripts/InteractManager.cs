using UnityEngine;
using UnityEngine.UI;

// 交互/小游戏管理器：用来接管对话暂停时的专门玩法
public class InteractManager : MonoBehaviour
{
    public static InteractManager Instance;

    [Header("UI引用")]
    public GameObject interactUI; // 你的整个交互面板容器（比如一个全屏背景+一些道具按钮）
    public Text interactTitleText; // 临时用来显示当前在玩什么交互（测试用）

    [Header("音效")]
    public AudioClip interactSfx;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // 兼容有无参数的调用
    public void StartInteraction()
    {
        StartInteraction("默认交互");
    }

    // 从 DialogueManager 接收到事件时开启交互界面
    public void StartInteraction(string interactName)
    {
        PlayInteractSfx();
        Debug.Log("开始交互玩法：" + interactName);

        // 1. 打开交互专属的UI面板
        if (interactUI != null)
        {
            interactUI.SetActive(true);
        }

        // 2. 根据表格填的名字，做不同的初始化逻辑
        if (interactTitleText != null)
        {
            interactTitleText.text = "正在调查/交互: " + interactName;
        }

        // 这里你可以写 if (interactName == "Pintu") { 打开拼图代码... } 等等
    }

    // 交互完成（比如拼图成功，或者玩家点击了“退出调查”按钮）
    public void FinishInteraction()
    {
        PlayInteractSfx();
        Debug.Log("交互结束，恢复对话！");

        // 1. 关闭交互UI
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }

        // 2. 告诉对话系统：继续往下播剧情！
        DialogueManager.Instance.ResumeFromSuspended();
    }

    private void PlayInteractSfx()
    {
        if (interactSfx == null) return;
        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(interactSfx, playPosition);
    }
}
