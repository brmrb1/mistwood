using UnityEngine;
using UnityEngine.Events;

// 通用交互事件处理器（可挂载在任何需要点击、查看后恢复剧情的物体上）
public class CommonInteractEvent : MonoBehaviour
{
    [Header("额外事件(可选)")]
    [Tooltip("例如你想在关掉界面时同时放个音效，可以在这里点+号拖个AudioSource进来")]
    public UnityEvent onInteractFinished;

    // 对话系统通过 SendMessage 自动寻找并调用的起始方法
    public void StartInteraction()
    {
        gameObject.SetActive(true); // 显示该物体的交互面板
    }

    // 玩家完成交互（例如点击调查、点右上角叉叉关闭等），请将 UI Button 的 OnClick() 绑定到这个方法上
    public void FinishInteraction()
    {
        Debug.Log($"【{gameObject.name}交互事件】玩家点击了完成/关闭，面板隐藏，准备恢复剧情...");
        
        // 隐藏自身
        gameObject.SetActive(false);

        // 如果 inspector 面板里拖拽了其他要触发的物体和方法，在这里激活（供后续拓展）
        onInteractFinished?.Invoke();

        // 告诉对话系统：事件结束，延迟一帧恢复之前的对话
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
}