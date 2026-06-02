using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("要跳转的目标场景名称")]
    public string sceneToLoad = "Scene01"; // 默认填入你的游戏场景名字

    // --- 游戏基础控制 ---

    // 这个方法用来绑定给“开始按钮”的 OnClick 事件
    public void StartGame()
    {
        // 清除任何可能遗留的存档读取状态，确保是全新游戏
        PlayerPrefs.DeleteKey("TargetLoadSlot");
        PlayerPrefs.DeleteKey("TargetLoadDialogIndex");
        PlayerPrefs.DeleteKey("LoadedPlayTime");
        PlayerPrefs.DeleteKey("ResumeCSVName");
        PlayerPrefs.DeleteKey("ResumeDialogueID");
        PlayerPrefs.Save();

        // 也可以直接传入数字索引：SceneManager.LoadScene(1);
        SceneManager.LoadScene(sceneToLoad);
    }

    // 退出游戏的方法（如果你的主界面也有退出按钮的话可以绑定这个）
    public void QuitGame()
    {
        Debug.Log("退出游戏");
        Application.Quit();
    }

    // 切换面板的显示/隐藏状态（点击打开，再点击关闭）
    public void TogglePanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
    }
}
