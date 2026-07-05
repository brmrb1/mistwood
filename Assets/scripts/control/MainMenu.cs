using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("要跳转的目标场景名称")]
    public string sceneToLoad = "Scene01"; // 默认填入你的游戏场景名字

    [Header("UI点击音效")]
    public AudioClip uiClickSfx;

    // --- 游戏基础控制 ---

    public void StartGame()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        // 【逻辑重构】直接清理所有可能的加载标记，确保开启全新游戏
        PlayerPrefs.DeleteKey("TargetLoadSlot");
        PlayerPrefs.DeleteKey("TargetLoadDialogID");
        PlayerPrefs.DeleteKey("ResumeCSVName");
        PlayerPrefs.DeleteKey("ResumeDialogueID");
        
        // 重置游戏时长统计
        PlayerPrefs.SetFloat("LoadedPlayTime", 0f);
        
        PlayerPrefs.Save();

        // 也可以直接传入数字索引：SceneManager.LoadScene(1);
        SceneManager.LoadScene(sceneToLoad);
    }

    // 退出游戏的方法（如果你的主界面也有退出按钮的话可以绑定这个）
    public void QuitGame()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        Debug.Log("退出游戏");
        Application.Quit();
    }

    // 切换面板的显示/隐藏状态（点击打开，再点击关闭）
    public void TogglePanel(GameObject panel)
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
    }

    // --- 设置重置功能 ---
    // 为了兼容 Unity 中已经绑定的按钮事件
    public void ResetSettings()
    {
        UIAudioHelper.PlayClickSfx(uiClickSfx, transform);

        SettingsManager settings = FindObjectOfType<SettingsManager>();
        if (settings != null)
        {
            settings.ResetSettings();
        }
        else
        {
            Debug.LogWarning("找不到 SettingsManager，无法重置设置。");
        }
    }

    // --- 音频设置转发 (为了修复 Missing MainMenu.SetBGMVolume) ---
    public void SetBGMVolume(float value)
    {
        SettingsManager settings = FindObjectOfType<SettingsManager>();
        if (settings != null) settings.SetBGMVolume(value);
    }

    public void SetSFXVolume(float value)
    {
        SettingsManager settings = FindObjectOfType<SettingsManager>();
        if (settings != null) settings.SetSFXVolume(value);
    }

    public void SetCharacterSFXVolume(float value)
    {
        SettingsManager settings = FindObjectOfType<SettingsManager>();
        if (settings != null) settings.SetCharacterSFXVolume(value);
    }
}
