using UnityEngine;

// 门交互事件
public class DoorEvent : MonoBehaviour
{
    // 对话系统用 SendMessage 自动调用的口子
    public void StartInteraction()
    {
        gameObject.SetActive(true); // 显示这扇门的互动面板
    }

    // 玩家点击了指定位置（比如在这个位置放一个透明的 Button，绑定这个方法）
    public void OnClickSpecificArea()
    {
        Debug.Log("【DoorEvent】玩家点击了指定区域，门事件面板即将隐藏，恢复对话...");
        
        gameObject.SetActive(false); // 隐藏互动面板本身

        // 告诉对话系统：事件结束，恢复对话！
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }
}
