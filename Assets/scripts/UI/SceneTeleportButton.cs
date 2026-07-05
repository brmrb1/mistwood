using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTeleportButton : MonoBehaviour
{
    [Header("传送目标")]
    [Tooltip("要跳转的场景名，需已加入 Build Settings")]
    public string targetSceneName;

    [Tooltip("是否使用 Build Index 跳转")]
    public bool useBuildIndex = false;

    [Tooltip("当 useBuildIndex 为 true 时生效")]
    public int targetBuildIndex = 0;

    [Header("可选")]
    [Tooltip("切换场景前是否清理对话恢复相关临时键值")]
    public bool clearDialogueTempKeys = false;

    // 绑定到 UI Button 的 OnClick()
    public void Teleport()
    {
        if (clearDialogueTempKeys)
        {
            PlayerPrefs.DeleteKey("TargetLoadSlot");
            PlayerPrefs.DeleteKey("TargetLoadDialogID");
            PlayerPrefs.DeleteKey("ResumeCSVName");
            PlayerPrefs.DeleteKey("ResumeDialogueID");
            PlayerPrefs.Save();
        }

        if (useBuildIndex)
        {
            SceneManager.LoadScene(targetBuildIndex);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[SceneTeleportButton] targetSceneName 为空，无法传送。");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    // 给需要参数调用的场景提供一个入口
    public void TeleportTo(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneTeleportButton] 传入的 sceneName 为空，无法传送。");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
