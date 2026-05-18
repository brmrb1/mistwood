using UnityEngine;

// 信件交互事件
public class LetterEvent : MonoBehaviour
{
    [Header("UI分配")]
    public GameObject itemImage1; // 第一张图片（物品）
    public GameObject itemImage2; // 第二张图片（信件内容）
    public GameObject closeButton; // 关闭按钮

    // 对话系统用 SendMessage 自动调用的口子
    public void StartInteraction()
    {
        Debug.Log("【LetterEvent】成功接收到唤醒指令！信件事件正式开始！");

        gameObject.SetActive(true); // 显示整体事件面板

        // 初始化状态：显示图1，隐藏图2和关闭按钮
        // 增加防空报错机制（如果没拖拽，会给你提示而不是直接卡死）
        if (itemImage1 != null) itemImage1.SetActive(true);
        else Debug.LogError("【LetterEvent】错误：itemImage1(图1) 没有在 Inspector 中拖拽赋值！");

        if (itemImage2 != null) itemImage2.SetActive(false);
        else Debug.LogError("【LetterEvent】错误：itemImage2(图2) 没有在 Inspector 中拖拽赋值！");

        if (closeButton != null) closeButton.SetActive(false);
        else Debug.LogError("【LetterEvent】错误：closeButton(关闭按钮) 没有在 Inspector 中拖拽赋值！");
    }

    // 玩家点击了第一张图片（你需要给图1加个Button组件并绑定这个方法）
    public void OnClickItem1()
    {
        itemImage1.SetActive(false); // 隐藏图1
        itemImage2.SetActive(true);  // 显示图2
        closeButton.SetActive(true); // 显示关闭按钮
    }

    // 玩家点击关闭按钮
    public void OnClickCloseButton()
    {
        gameObject.SetActive(false); // 隐藏整个信件面板

        // 告诉对话系统：事件结束，恢复对话！
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }
}
