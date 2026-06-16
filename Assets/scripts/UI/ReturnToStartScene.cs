using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToStartScene : MonoBehaviour
{
    [Header("确认返回主界面的面板")]
    public GameObject confirmationPanel;

    /// <summary>
    /// 点击返回主界面按钮时调用，用于弹出确认界面
    /// </summary>
    public void GoToStartScene()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
        }
        else
        {
            // 如果没有分配面板，则直接返回（保持原有逻辑作为兜底）
            ConfirmReturn();
        }
    }

    /// <summary>
    /// 在确认界面点击“确认”时调用
    /// </summary>
    public void ConfirmReturn()
    {
        SceneManager.LoadScene("start");
    }

    /// <summary>
    /// 在确认界面点击“取消”时调用
    /// </summary>
    public void CancelReturn()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
    }
}
