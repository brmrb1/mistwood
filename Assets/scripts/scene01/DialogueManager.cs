using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems; // 新增 EventSystems 引用，用于判断点击是否在 UI 上
using TMPro;
using System.Text.RegularExpressions;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI 引用")]
    public GameObject dialoguePanel;     // 对话主面板
    public TMP_Text nameText;            // 名字文本
    public TMP_Text contentText;         // 对话内容文本

    [Header("场景表现引用")]
    public SpriteRenderer backgroundRenderer; // 背景图 (改为 SpriteRenderer)
    public SpriteRenderer leftRenderer;       // 左侧立绘站位
    public SpriteRenderer centerRenderer;     // 中间立绘站位
    public SpriteRenderer rightRenderer;      // 右侧立绘站位

    [Header("选项表现引用")]
    public GameObject optionsPanel;      // 包含选项的面板 (父节点)
    public GameObject optionButtonPrefab;// 单个选项按钮的预制体 (需带Button组件和TextMeshProUGUI子节点)
    private List<GameObject> activeOptions = new List<GameObject>(); // 记录当前生成的选项按钮

    [Header("素材映射库 (Inspector拖拽)")]
    public List<SpriteMapping> backgroundLibrary = new List<SpriteMapping>();
    public List<SpriteMapping> characterLibrary = new List<SpriteMapping>();
    public List<AudioMapping> audioLibrary = new List<AudioMapping>();
    public List<PrefabMapping> effectLibrary = new List<PrefabMapping>(); // 新增动画特效库
    
    private Dictionary<string, Sprite> bgDict = new Dictionary<string, Sprite>();
    private Dictionary<string, Sprite> charDict = new Dictionary<string, Sprite>();
    private Dictionary<string, AudioClip> audioDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, GameObject> effectDict = new Dictionary<string, GameObject>();

    [Header("音效组件")]
    public AudioSource audioSource; // 用于播放音效的组件
    public AudioSource bgmSource;   // 用于播放背景音乐的组件
    public UnityEngine.Audio.AudioMixerGroup bgmGroup; // 新增：BGM 混音组
    public UnityEngine.Audio.AudioMixerGroup sfxGroup; // 新增：音效混音组

    [Header("配置")]
    public TextAsset csvFile;            // CSV 文件
    public float typeSpeed = 0.05f;      // 打字机速度，每个字显示的时间间隔 (秒)

    [Header("自动/快进播放配置")]
    public GameObject speedDropdownObj;      // 速度控制下拉菜单的UI对象
    public float autoPlayWaitTime = 1.0f;    // 自动播放时，每句话读完后的停留时间
    private bool isAutoPlaying = false;      // 当前是否处于自动播放状态
    private float currentSpeedMultiplier = 1.0f; // 播放速度倍率 (用于快进，1.0为正常)

    [Header("多剧本支持库 (Inspector拖拽)")]
    public List<TextAsset> overrideCsvList = new List<TextAsset>(); // 把新的csv（如new 2）拖入这里

    private Dictionary<string, DialogueLine> dialogueDict = new Dictionary<string, DialogueLine>();
    private DialogueState currentState = DialogueState.Waiting;
    private DialogueLine currentLine;
    private Coroutine typingCoroutine;
    private bool skipTyping = false;     // 点击跳过打字效果

    // --- 【新增】供存档系统调用的接口 ---
    public string GetCurrentLineID() => currentLine != null ? currentLine.id : "0";
    public string GetCurrentCSVName() => csvFile != null ? csvFile.name : "";
    public DialogueState GetCurrentState() => currentState;

    private void Awake()
    {
        // 简化单例逻辑：进入新场景时直接覆盖实例，因为旧场景的实例会被 Unity 自动销毁
        Instance = this;

        // 初始化映射字典
        foreach (var item in backgroundLibrary)
        {
            string safeKey = (item.key ?? "").Trim().ToLower();
            if (!bgDict.ContainsKey(safeKey)) bgDict.Add(safeKey, item.sprite);
        }
            foreach (var item in characterLibrary)
            {
                string safeKey = (item.key ?? "").Replace(" ", "").Replace(" ", "").Replace("　", "").Trim();
                if (!charDict.ContainsKey(safeKey)) charDict.Add(safeKey, item.sprite);
            }
            foreach (var item in audioLibrary)
            {
                if (!audioDict.ContainsKey(item.key)) audioDict.Add(item.key, item.clip);
            }
            foreach (var item in effectLibrary)
            {
                if (!effectDict.ContainsKey(item.key)) effectDict.Add(item.key, item.prefab);
            }

            // 自动添加AudioSource并配置混音组
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            if (sfxGroup != null) audioSource.outputAudioMixerGroup = sfxGroup;

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true; // bgm 默认循环播放
            }
            if (bgmGroup != null) bgmSource.outputAudioMixerGroup = bgmGroup;
    }

    private void Start()
    {
        // 游戏开始时默认隐藏速度调节菜单
        if (speedDropdownObj != null) speedDropdownObj.SetActive(false);

        // 检查是否有跨场景/跨 CSV 跳转的指示
        string resumeCSV = PlayerPrefs.GetString("ResumeCSVName", "");
        if (!string.IsNullOrEmpty(resumeCSV))
        {
            Debug.Log($"【DialogueManager】检测到跳转剧本请求: {resumeCSV}");
            // 如果有指定且列表里存在同名 CSV，就覆盖当前 csvFile
            TextAsset targetCsv = overrideCsvList.Find(x => x.name == resumeCSV);
            if (targetCsv != null)
            {
                csvFile = targetCsv;
                Debug.Log($"【DialogueManager】成功在 overrideCsvList 中找到并应用剧本: {resumeCSV}");
            }
            else
            {
                Debug.LogError($"【DialogueManager】无法应用剧本 {resumeCSV}：在当前场景 DialogueManager 的 overrideCsvList 中找不到同名映射，请在 Inspector 面板检查配置！");
            }
            // 消费掉，防止影响以后正常的开始
            PlayerPrefs.DeleteKey("ResumeCSVName");
            PlayerPrefs.Save();
        }

        if (csvFile != null)
        {
            LoadCSV(csvFile.text);
        }

        // 【新增】优先检查是否有存档读取进度的指示
        string savedLoadID = PlayerPrefs.GetString("TargetLoadDialogID", "");
        if (!string.IsNullOrEmpty(savedLoadID))
        {
            Debug.Log($"【DialogueManager】执行存档读取，起始 ID: {savedLoadID}");
            PlayerPrefs.DeleteKey("TargetLoadDialogID");
            
            // 如果读取了存档进度，则清空临时的 ResumeDialogueID 防止冲突
            PlayerPrefs.DeleteKey("ResumeDialogueID");
            PlayerPrefs.Save();

            StartDialogue(savedLoadID);
        }
        // 检查之前是否因为小游戏挂起保存了待恢复的 ID
        else if (PlayerPrefs.HasKey("ResumeDialogueID"))
        {
            string resumeID = PlayerPrefs.GetString("ResumeDialogueID", "");
            PlayerPrefs.DeleteKey("ResumeDialogueID"); // 消费掉
            StartDialogue(resumeID);
        }
        else
        {
            // 默认测试：从第一行开始（请确保你的 CSV 中有这行，或自行修改默认起始 ID）
            StartDialogue("0");
        }
    }

    private void Update()
    {
        // 监听鼠标左键点击、手机触屏，或者按空格键
        bool userWantsNext = Input.GetMouseButtonDown(0) || 
                            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) ||
                            Input.GetKeyDown(KeyCode.Space);

        if (userWantsNext)
        {
            // 检查点击位置是否在 UI 按钮上，如果在 UI 按钮上，就交给按钮自身处理
            // (空格键通常不需要检查 UI 遮挡)
            if (EventSystem.current != null && !Input.GetKeyDown(KeyCode.Space))
            {
                bool isPointerOverUI = false;

                // 兼容电脑端鼠标事件
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    isPointerOverUI = true;
                }

                // 兼容手机端触摸事件 (包含 Unity 编辑器内的 Device Simulator)
                if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                {
                    isPointerOverUI = true;
                }

                if (isPointerOverUI)
                {
                    return;
                }
            }

            // 只允许点击屏幕快进正在播放的打字，不允许点击屏幕跳到下一句...
            if (currentState == DialogueState.Playing)
            {
                isAutoPlaying = false; 
                if (speedDropdownObj != null) speedDropdownObj.SetActive(false);
                skipTyping = true; // 玩家点击，跳过逐字打印
            }
            // ...除非当前对话框是被隐藏的（比如纯净背景演出中），此时允许点击屏幕继续剧情
            else if (currentState == DialogueState.Waiting)
            {
                if (dialoguePanel != null && !dialoguePanel.activeInHierarchy)
                {
                    isAutoPlaying = false;
                    if (speedDropdownObj != null) speedDropdownObj.SetActive(false);
                    ContinueDialogue();
                }
            }
        }
    }

    // --- 玩家主动点击与按钮绑定的核心方法 ---

    // 玩家在屏幕空白处点击（相当于手动触发一次下一步）
    private void ManualUserClick()
    {
        // 玩家手动操作了，取消自动播放，并隐藏调速菜单
        isAutoPlaying = false; 
        if (speedDropdownObj != null) speedDropdownObj.SetActive(false);

        if (currentState == DialogueState.Playing)
        {
            skipTyping = true; // 玩家点击，跳过逐字打印
        }
        else if (currentState == DialogueState.Waiting)
        {
            ContinueDialogue();
        }
    }

    // 绑定至：UI上的【下一句/跳过】按钮
    public void OnNextButtonClicked()
    {
        ManualUserClick();
    }

    // 绑定至：UI上的【自动播放】按钮（带开关功能）
    public void ToggleAutoPlay()
    {
        isAutoPlaying = !isAutoPlaying;
        
        // 根据自动播放状态，显示或隐藏调速下拉菜单
        if (speedDropdownObj != null)
        {
            speedDropdownObj.SetActive(isAutoPlaying);
        }

        Debug.Log("自动播放状态变为: " + isAutoPlaying);

        // 如果开启了自动播放，并且当前已经是在等待状态，就立刻执行一句“继续对话”起头
        if (isAutoPlaying && currentState == DialogueState.Waiting)
        {
            ContinueDialogue();
        }
    }

    // 绑定至：UI上下拉菜单 (Dropdown) 的 On Value Changed (Int) 事件
    // 假设你的下拉菜单 (Options) 顺序设置为： 0: 1x,  1: 2x,  2: 10x
    public void OnSpeedDropdownChanged(int index)
    {
        switch (index)
        {
            case 0:
                currentSpeedMultiplier = 1.0f;
                break;
            case 1:
                currentSpeedMultiplier = 2.0f;
                break;
            case 2:
                currentSpeedMultiplier = 10.0f;
                break;
            default:
                currentSpeedMultiplier = 1.0f;
                break;
        }
        Debug.Log("播放速度已通过下拉菜单切换为: " + currentSpeedMultiplier + "x");
    }

    // --- 核心方法 ---

    // 载入 CSV 数据
    private void LoadCSV(string csvData)
    {
        dialogueDict.Clear();

        // 简易正则分割逗号（兼容带引号的内容中有逗号的情况）
        string[] lines = Regex.Split(csvData, "\r\n|\r|\n");

        // 从 i=1 开始，跳过表头
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] values = Regex.Split(lines[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            
            // 去除双引号
            for (int v = 0; v < values.Length; v++)
            {
                values[v] = values[v].Trim('\"', ' ');
            }

            // 至少要保证基本列够获取以防报错
            if (values.Length < 15) continue;

            DialogueLine newLine = new DialogueLine
            {
                type = values[0],
                id = values[1],
                charName = values[2],
                expression = values[3],
                position = values[4],
                content = values[5],
                nextID = values[6],
                bgEffect = values[7],
                effect = values[8],
                sound = values[9],
                bgm = values[10],
                background = values[11],
                variable = values[12],
                chapter = values[13],
                eventParams = values[14]
            };

            if (!string.IsNullOrEmpty(newLine.id) && !dialogueDict.ContainsKey(newLine.id))
            {
                dialogueDict.Add(newLine.id, newLine);
            }
        }
        Debug.Log("CSV 成功加载，总行数: " + dialogueDict.Count);
    }

    // 从指定 ID 开启对话
    public void StartDialogue(string startID)
    {
        PlayLine(startID);
    }

    // 处理单行逻辑
    private void PlayLine(string currentId)
    {
        if (!dialogueDict.ContainsKey(currentId))
        {
            Debug.LogWarning("找不到对话 ID: " + currentId + "，对话结束。");
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            return;
        }

        currentLine = dialogueDict[currentId];

        if (currentLine.type == "#")
        {
            // 如果台词为空，则隐藏主对话框；如果不为空，则显示
            bool hasContent = !string.IsNullOrEmpty(currentLine.content);
            if (dialoguePanel != null) dialoguePanel.SetActive(hasContent);
            if (contentText != null) contentText.gameObject.SetActive(hasContent); // 同步文字内容的显隐
            
            // 如果名字为空，单独隐藏名字本体和名字底图（父物体）
            bool hasName = !string.IsNullOrEmpty(currentLine.charName);
            if (nameText != null)
            {
                nameText.gameObject.SetActive(hasName);
                if (nameText.transform.parent != null && nameText.transform.parent != dialoguePanel.transform)
                {
                    nameText.transform.parent.gameObject.SetActive(hasName);
                }
            }

            // 普通对话
            UpdateUI(currentLine);
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeContent(currentLine.content));
        }
        else if (currentLine.type == "EVENT")
        {
            // 特殊事件 (遇到事件必须自动停止自动播放)
            isAutoPlaying = false;
            if (speedDropdownObj != null) speedDropdownObj.SetActive(false);
            
            // 挂起对话进入小游戏或加载场景
            SuspendDialogue();
            TriggerGameEvent(currentLine.eventParams);
        }
        else if (currentLine.type == "COND")
        {
            // 解析条件变量，这里写死演示，实际请结合 Variable 系统
            // 例如 Variable填: HasKey=1，那么检测过不过
            bool conditionMet = CheckCondition(currentLine.variable);
            string targetId = conditionMet ? currentLine.nextID : currentLine.eventParams; // 依据条件抉择路线，这里逻辑视你需要而定
            
            // 简单处理：走 nextID
            PlayLine(currentLine.nextID);
        }
        else if (currentLine.type == "CHOICE")
        {
            // 选项逻辑：停止自动播放，等待玩家做选择
            isAutoPlaying = false;
            if (speedDropdownObj != null) speedDropdownObj.SetActive(false);
            
            currentState = DialogueState.Choosing;

            // 清理旧的选项按钮
            foreach (var btn in activeOptions)
            {
                Destroy(btn);
            }
            activeOptions.Clear();

            // 分割选项：使用 Content 存放选项文字，NextID 存放跳转 ID，用竖线 "|" 分割
            string[] choicesText = currentLine.content.Split('|');
            string[] choicesTarget = currentLine.nextID.Split('|');

            // 激活选项面板
            if (optionsPanel != null) optionsPanel.SetActive(true);

            for (int i = 0; i < choicesText.Length && i < choicesTarget.Length; i++)
            {
                string targetId = choicesTarget[i].Trim();
                string btnText = choicesText[i].Trim();

                if (optionButtonPrefab != null && optionsPanel != null)
                {
                    // 使用 worldPositionStays = false 确保 UI 坐标正确，不会变得巨大
                    GameObject btnObj = Instantiate(optionButtonPrefab);
                    btnObj.transform.SetParent(optionsPanel.transform, false);
                    
                    btnObj.SetActive(true);
                    activeOptions.Add(btnObj);

                    // 寻找并设置文本 (由于可能是 Button 或是子节点的 Text/TMP_Text)
                    TMP_Text t = btnObj.GetComponentInChildren<TMP_Text>();
                    if (t != null) t.text = btnText;
                    else
                    {
                        Text legacyText = btnObj.GetComponentInChildren<Text>();
                        if (legacyText != null) legacyText.text = btnText;
                    }

                    // 绑定按钮事件
                    Button btnInfo = btnObj.GetComponent<Button>();
                    if (btnInfo != null)
                    {
                        btnInfo.onClick.AddListener(() =>
                        {
                            OnChoiceSelected(targetId);
                        });
                    }
                }
                else
                {
                    Debug.Log($"生成选项：{btnText} -> 跳转：{targetId} (无UI预制体，仅打印)");
                }
            }
        }
    }

    // 处理玩家点击选项
    public void OnChoiceSelected(string targetId)
    {
        // 隐藏选项面板并销毁按钮
        if (optionsPanel != null) optionsPanel.SetActive(false);
        foreach (var btn in activeOptions)
        {
            Destroy(btn);
        }
        activeOptions.Clear();

        // 跳转到选择的剧情线
        PlayLine(targetId);
    }

    // 下一句
    private void ContinueDialogue()
    {
        // 查找下一句
        string targetNextId = currentLine.nextID;

        // 如果没有配置 NextID，理论上可以顺序读取，或者视为结束
        if (string.IsNullOrEmpty(targetNextId))
        {
            Debug.Log("对话结束。");
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            return;
        }

        PlayLine(targetNextId);
    }

        // 更新 UI 内容、背景、立绘等
    private void UpdateUI(DialogueLine line)
    {
        if (nameText != null) nameText.text = line.charName;
        
        // --- 播放音效 ---
        if (!string.IsNullOrEmpty(line.sound) && audioSource != null)
        {
            if (audioDict.TryGetValue(line.sound, out AudioClip clip))
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning("无法从素材库中找到音效映射: " + line.sound);
            }
        }

        // --- 播放 BGM ---
        if (!string.IsNullOrEmpty(line.bgm) && bgmSource != null)
        {
            // 如果填特殊指令停止播放
            if (line.bgm.ToLower() == "stop" || line.bgm.ToLower() == "none")
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
            else if (audioDict.TryGetValue(line.bgm, out AudioClip bgmClip))
            {
                // 如果当前播放的BGM不是这首，或者已经停止了，才进行播放
                if (bgmSource.clip != bgmClip || !bgmSource.isPlaying)
                {
                    bgmSource.clip = bgmClip;
                    bgmSource.Play();
                }
            }
            else
            {
                Debug.LogWarning("无法从素材库中找到BGM映射: " + line.bgm);
            }
        }

        // 解析当前行的特效配置
        string currentEffect = string.IsNullOrEmpty(line.effect) ? "" : line.effect.ToLower();
        string currentBgEffect = string.IsNullOrEmpty(line.bgEffect) ? "" : line.bgEffect;

        // 更换背景: 使用 SpriteRenderer
        if (backgroundRenderer != null)
        {
            if (!string.IsNullOrEmpty(line.background))
            {
                string safeBg = line.background.Trim().ToLower();
                if (bgDict.TryGetValue(safeBg, out Sprite bgSprite))
                {
                    // 确保背景物体是激活状态
                    if (!backgroundRenderer.gameObject.activeSelf)
                    {
                        backgroundRenderer.gameObject.SetActive(true);
                    }

                    if (backgroundRenderer.sprite != bgSprite)
                    {
                        backgroundRenderer.sprite = bgSprite;
                    }
                }
                else
                {
                    Debug.LogWarning("无法从素材库中找到背景图片映射: [" + line.background + "]");
                }
            }
            
            // 播放背景独立动画 (就算这行没有填新背景图，也能给留在场上的当前背景播放特效)
            if (!string.IsNullOrEmpty(currentBgEffect))
            {
                Animator bgAnim = backgroundRenderer.GetComponent<Animator>();
                if (bgAnim != null)
                {
                    bgAnim.Play(currentBgEffect, 0, 0f);
                }
            }
        }

        // 检查并生成你自己拖拽进来的【特效/动画预制体】
        if (effectDict.TryGetValue(currentEffect, out GameObject animPrefab))
        {
            // 如果找到了对应的预制体，就在画面中心生成它
            // (你可以在自己制作预制体时，在它身上挂个脚本让它播放完动画后自动销毁)
            Instantiate(animPrefab, Vector3.zero, Quaternion.identity);
        }

        // --- 处理人物立绘 ---
        // 先隐藏所有立绘，强制把颜色洗白，确保绝不会残留！
        if (leftRenderer != null) 
        {
            leftRenderer.gameObject.SetActive(false);
            leftRenderer.color = Color.white;
        }
        if (centerRenderer != null) 
        {
            centerRenderer.gameObject.SetActive(false);
            centerRenderer.color = Color.white;
        }
        if (rightRenderer != null) 
        {
            rightRenderer.gameObject.SetActive(false);
            rightRenderer.color = Color.white;
        }

        if (!string.IsNullOrEmpty(line.position) && !string.IsNullOrEmpty(line.expression))
        {
            // 支持使用 "|" 分隔符同时填写多个角色，例如 位置填 "左|右"，立绘填 "sys1|llb1"
            string[] positions = line.position.Split('|');
            string[] expressions = line.expression.Split('|');

            // Find the maximum length to ensure we process all provided positions or expressions
            int maxItems = Mathf.Max(positions.Length, expressions.Length);

            for (int i = 0; i < maxItems; i++)
            {
                // Fallback to the last available position/expression if the arrays have different lengths
                string currentPosition = i < positions.Length ? positions[i] : positions[positions.Length - 1];
                string currentExpression = i < expressions.Length ? expressions[i] : expressions[expressions.Length - 1];

                string spriteName = currentExpression.Trim('\uFEFF', '\u200B', '?');
                string safeSpriteName = (spriteName ?? "").Replace(" ", "").Replace(" ", "").Replace("　", "");
                
                if (charDict.TryGetValue(safeSpriteName, out Sprite charSprite))
                {
                    string pos = currentPosition.ToLower();
                    SpriteRenderer targetRenderer = null;
                    
                    if ((pos == "left" || pos == "左") && leftRenderer != null)
                    {
                        targetRenderer = leftRenderer;
                    }
                    else if ((pos == "center" || pos == "中") && centerRenderer != null)
                    {
                        targetRenderer = centerRenderer;
                    }
                    else if ((pos == "right" || pos == "右") && rightRenderer != null)
                    {
                        targetRenderer = rightRenderer;
                    }

                    if (targetRenderer != null)
                    {
                        targetRenderer.sprite = charSprite;
                        targetRenderer.gameObject.SetActive(true);

                        // 判读出场特效（播放 Animator 动画）
                        if (!string.IsNullOrEmpty(currentEffect))
                        {
                            Animator anim = targetRenderer.GetComponent<Animator>();
                            if (anim != null)
                            {
                                anim.Play(currentEffect, 0, 0f); // 直接播放与配置同名的动画片段
                            }
                            else
                            {
                                Debug.LogWarning(targetRenderer.gameObject.name + " 身上没有 Animator 组件，无法播放动画: " + currentEffect);
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("无法在素材库中找到立绘图片映射: " + spriteName);
                }
            }
        }
    }

    // 打字机协程
    private IEnumerator TypeContent(string textContent)
    {
        currentState = DialogueState.Playing;
        skipTyping = false;

        if (contentText != null)
        {
            // 使用 maxVisibleCharacters 实现打字机效果，可完美支持富文本（标签不会被逐字显示）
            contentText.text = textContent;
            contentText.maxVisibleCharacters = 0;
            
            // 强制更新以计算真实的字符数量（不计入富文本标签）
            contentText.ForceMeshUpdate();
            int totalVisibleCharacters = contentText.textInfo.characterCount;

            for (int i = 0; i <= totalVisibleCharacters; i++)
            {
                if (skipTyping)
                {
                    contentText.maxVisibleCharacters = totalVisibleCharacters;
                    break;
                }

                contentText.maxVisibleCharacters = i;

                // 真实等待时间 = 基础出字速度 / 速度倍率
                yield return new WaitForSeconds(typeSpeed / currentSpeedMultiplier);
            }
        }

        currentState = DialogueState.Waiting;

        // 如果处于自动播放状态，启动一个等待协程再翻到下一句
        if (isAutoPlaying)
        {
            StartCoroutine(AutoPlayWaitSequence());
        }
    }

    private IEnumerator AutoPlayWaitSequence()
    {
        // 自动播放的等待时间也会受快进倍率影响（如果快进是2x，看完后的停留时间也会减半）
        float waitTime = autoPlayWaitTime / currentSpeedMultiplier;
        yield return new WaitForSeconds(waitTime);

        // 如果在等待期间，玩家手点了屏幕（导致 isAutoPlaying 变为了 false），就不执行
        if (isAutoPlaying && currentState == DialogueState.Waiting)
        {
            ContinueDialogue();
        }
    }

    // --- 机制：挂起与恢复交互 ---

    // 遇到玩法或跳转，挂起当前对话系统
    private void SuspendDialogue()
    {
        currentState = DialogueState.Suspended;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (contentText != null) contentText.gameObject.SetActive(false); // 强制把文字也隐藏
        
        // 连同名字文本的框一起隐藏（如果它的父物体是名字框底图，也一起隐藏）
        if (nameText != null)
        {
            nameText.gameObject.SetActive(false);
            if (nameText.transform.parent != null && nameText.transform.parent != dialoguePanel.transform)
            {
                nameText.transform.parent.gameObject.SetActive(false);
            }
        }
        
        // 隐藏场上的立绘，保持交互事件时的画面纯净
        if (leftRenderer != null) leftRenderer.gameObject.SetActive(false);
        if (centerRenderer != null) centerRenderer.gameObject.SetActive(false);
        if (rightRenderer != null) rightRenderer.gameObject.SetActive(false);
        
        // 记录回来后需要从哪一句开始（如果没有填nextID，默认按当前ID+1接续）
        string nextToResume = string.IsNullOrEmpty(currentLine.nextID) 
            ? (int.Parse(currentLine.id) + 1).ToString() 
            : currentLine.nextID;

        PlayerPrefs.SetString("ResumeDialogueID", nextToResume);
        PlayerPrefs.Save();
    }

    // 解析并执行事件
    private void TriggerGameEvent(string eventParams)
    {
        Debug.Log("触发了特殊事件：" + eventParams);

        if (eventParams.StartsWith("LoadScene:"))
        {
            string sceneStr = eventParams.Substring(10); // 截取之后的字符串
            string sceneName = sceneStr;

            // 支持形如 "LoadScene:guide|new 2" 的格式，用来在跳转场景后强行指定要读取的新 CSV 文件名
            if (sceneStr.Contains("|"))
            {
                string[] parts = sceneStr.Split('|');
                sceneName = parts[0];
                string nextCsvName = parts[1];
                
                // 将要读取的新 CSV 文件名存下来
                PlayerPrefs.SetString("ResumeCSVName", nextCsvName);
                
                // 跳转新剧本后通常默认从 0 行开始
                PlayerPrefs.SetString("ResumeDialogueID", "0"); 
                PlayerPrefs.Save(); // 强制保存，确保跳转场景后能读取到
            }

            SceneManager.LoadScene(sceneName);
        }
        else 
        {
            GameObject eventObj = null;
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            
            // 【新功能】支持参数传递：如果事件填的是 TaskSystem:01
            string targetName = eventParams.Trim();
            string eventArg = "";
            if (targetName.Contains(":"))
            {
                int colonIndex = targetName.IndexOf(':');
                eventArg = targetName.Substring(colonIndex + 1).Trim();
                targetName = targetName.Substring(0, colonIndex).Trim();
            }

            foreach (GameObject obj in allObjects)
            {
                if (obj.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase) && obj.scene.IsValid())
                {
                    eventObj = obj;
                    break;
                }
            }

            if (eventObj != null)
            {
                Debug.Log($"成功找到交互物体 [{eventObj.name}]，正在唤醒...");
                eventObj.SetActive(true); 
                
                // 如果带了参数，就发送带参数的方法；如果没有，就照旧
                if (string.IsNullOrEmpty(eventArg))
                    eventObj.SendMessage("StartInteraction", SendMessageOptions.DontRequireReceiver);
                else
                    eventObj.SendMessage("StartInteraction", eventArg, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning("找不到名为 [" + targetName + "] 的游戏物体，无法执行对应事件！请检查物体名字是否拼写完全一致。");
            }
        }
    }

    // 从交互中恢复对话 (给你的交互/小游戏脚本调用的接口)
    public void ResumeFromSuspended()
    {
        if (currentState == DialogueState.Suspended)
        {
            // 恢复对话框和文本显示
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (contentText != null) contentText.gameObject.SetActive(true);

            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                if (nameText.transform.parent != null) nameText.transform.parent.gameObject.SetActive(true);
            }

            string resumeID = PlayerPrefs.GetString("ResumeDialogueID", "");
            if (!string.IsNullOrEmpty(resumeID))
            {
                PlayerPrefs.DeleteKey("ResumeDialogueID");
                PlayLine(resumeID);
            }
            else
            {
                // 如果实在没找到跳转标记，做个容错往下走
                ContinueDialogue();
            }
        }
    }

    // 简单条件判断，你后续可以拓展成读取 PlayerPrefs
    private bool CheckCondition(string conditionStr)
    {
        // 此处只做示例，默认返回 true
        return true;
    }
}

// 存放每行数据的类
public class DialogueLine
{
    public string type;         // 标志 (0)
    public string id;           // ID (1)
    public string charName;     // 人物 (2)
    public string expression;   // 立绘 (3)
    public string position;     // 位置 (4)
    public string content;      // 内容 (5)
    public string nextID;       // 跳转 (6)
    public string bgEffect;     // 背景效果 (7)
    public string effect;       // 效果 (8)
    public string sound;        // 音效 (9)
    public string bgm;          // 背景音 (10)
    public string background;   // 背景 (11)
    public string variable;     // 变量 (12)
    public string chapter;      // 章节 (13)
    public string eventParams;  // 事件 (14)
}

public enum DialogueState
{
    Playing,     // 正在逐字打印
    Waiting,     // 打印完毕，等待玩家点击继续
    Choosing,    // 正在等待选项
    Suspended    // 挂起状态（去了小游戏或交互）
}

// 定义可以显示在 Inspector 的素材映射结构体
[System.Serializable]
public struct AudioMapping
{
    public string key;     // CSV里填的音效名
    public AudioClip clip; // 对应的音效片段

    public AudioMapping(string key, AudioClip clip)
    {
        this.key = key;
        this.clip = clip;
    }
}

[System.Serializable]
public struct SpriteMapping
{
    public string key;     // CSV 里填的名字
    public Sprite sprite;  // 对应的图片素材

    public SpriteMapping(string key, Sprite sprite)
    {
        this.key = key;
        this.sprite = sprite;
    }
}

[System.Serializable]
public struct PrefabMapping
{
    public string key;       // CSV里填的特效名
    public GameObject prefab; // 对应的动画/特效预制体 (Prefab)
}