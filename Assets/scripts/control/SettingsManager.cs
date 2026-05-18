using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    [Header("音频设置 (需要配置AudioMixer)")]
    public AudioMixer audioMixer;
    public Slider bgmSlider;
    public Slider characterSFXSlider;
    public Slider sfxSlider;

    [Header("显示与语言设置")]
    // 若你用的是TextMeshPro的下拉框，把这里的 Dropdown 改成 TMPro.TMP_Dropdown
    public TMPro.TMP_Dropdown screenModeDropdown; 
    public TMPro.TMP_Dropdown languageDropdown; // 控制语言的下拉框

    private void Start()
    {
        // 如果有 Dropdown，初始化它的值并且添加监听
        if (screenModeDropdown != null)
        {
            // 假设选项0是"全屏"，选项1是"窗口化"
            screenModeDropdown.value = Screen.fullScreen ? 0 : 1;
            screenModeDropdown.onValueChanged.AddListener(SetScreenMode);
        }
    }

    // --- 设置相关功能 ---

    // 绑定至 bgmSlider 的 OnValueChanged 事件
    public void SetBGMVolume(float value)
    {
        // 将0~1转换为分贝(-80至0)
        float db = value > 0.001f ? Mathf.Log10(value) * 20 : -80f;
        if (audioMixer != null) audioMixer.SetFloat("BGM", db); 
    }

    // 绑定至 characterSFXSlider 的 OnValueChanged 事件
    public void SetCharacterSFXVolume(float value)
    {
        float db = value > 0.001f ? Mathf.Log10(value) * 20 : -80f;
        if (audioMixer != null) audioMixer.SetFloat("CharacterSFX", db);
    }

    // 绑定至 sfxSlider 的 OnValueChanged 事件
    public void SetSFXVolume(float value)
    {
        float db = value > 0.001f ? Mathf.Log10(value) * 20 : -80f;
        if (audioMixer != null) audioMixer.SetFloat("SFX", db);
    }

    // 绑定至 下拉框(Dropdown) 的 OnValueChanged 事件
    public void SetScreenMode(int index)
    {
        // 0为全屏，1为窗口化
        Screen.fullScreen = (index == 0);
    }

    // 绑定至 重置按钮 的 OnClick 事件
    public void ResetSettings()
    {
        // 将声音UI恢复到最大值 1 
        if (bgmSlider != null) bgmSlider.value = 1f;
        if (characterSFXSlider != null) characterSFXSlider.value = 1f;
        if (sfxSlider != null) sfxSlider.value = 1f;

        // 恢复全屏默认状态
        if (screenModeDropdown != null) screenModeDropdown.value = 0;
        Screen.fullScreen = true;

        // 恢复默认语言 (通常 0 代表默认语言，比如中文)
        if (languageDropdown != null) languageDropdown.value = 0;
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