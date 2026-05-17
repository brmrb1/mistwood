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

    [Header("配置")]
    public TextAsset csvFile;            // CSV 文件
    public float typeSpeed = 0.05f;      // 打字机速度，每个字显示的时间间隔 (秒)

    [Header("自动/快进播放配置")]
    public GameObject speedDropdownObj;      // 速度控制下拉菜单的UI对象
    public float autoPlayWaitTime = 1.0f;    // 自动播放时，每句话读完后的停留时间
    private bool isAutoPlaying = false;      // 当前是否处于自动播放状态
    private float currentSpeedMultiplier = 1.0f; // 播放速度倍率 (用于快进，1.0为正常)

    private Dictionary<string, DialogueLine> dialogueDict = new Dictionary<string, DialogueLine>();
    private DialogueState currentState = DialogueState.Waiting;
    private DialogueLine currentLine;
    private Coroutine typingCoroutine;
    private bool skipTyping = false;     // 点击跳过打字效果

    private void Awake()
    {
        if (Instance == null)
        {
            // 如果你希望对话管理器跨场景存在，可以取消注释下面这行
            // DontDestroyOnLoad(gameObject);

            // 初始化映射字典
            foreach (var item in backgroundLibrary)
            {
                if (!bgDict.ContainsKey(item.key)) bgDict.Add(item.key, item.sprite);
            }
            foreach (var item in characterLibrary)
            {
                if (!charDict.ContainsKey(item.key)) charDict.Add(item.key, item.sprite);
            }
            foreach (var item in audioLibrary)
            {
                if (!audioDict.ContainsKey(item.key)) audioDict.Add(item.key, item.clip);
            }
            foreach (var item in effectLibrary)
            {
                if (!effectDict.ContainsKey(item.key)) effectDict.Add(item.key, item.prefab);
            }

            // 自动添加AudioSource
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 游戏开始时默认隐藏速度调节菜单
        if (speedDropdownObj != null) speedDropdownObj.SetActive(false);

        if (csvFile != null)
        {
            LoadCSV(csvFile.text);
        }

        // 检查之前是否因为小游戏挂起保存了待恢复的 ID
        string resumeID = PlayerPrefs.GetString("ResumeDialogueID", "");
        if (!string.IsNullOrEmpty(resumeID))
        {
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
        // 监听鼠标左键点击，或者手机触屏
        if (Input.GetMouseButtonDown(0))
        {
            // 检查点击位置是否在 UI 按钮上，如果在 UI 按钮上，就不要触发挥屏的继续效果，交给按钮自身处理
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            ManualUserClick();
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
    // 假设你的下拉菜单 (Options) 顺序设置为： 0: 1x,  1: 2x,  2: 4x
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
                currentSpeedMultiplier = 4.0f;
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
            if (values.Length < 14) continue;

            DialogueLine newLine = new DialogueLine
            {
                type = values[0],
                id = values[1],
                charName = values[2],
                position = values[3],
                content = values[4],
                nextID = values[5],
                effect = values[6],
                sound = values[7],
                background = values[8],
                promptUI = values[9],
                variable = values[10],
                expression = values[11],
                chapter = values[12],
                eventParams = values[13],
                bgEffect = values.Length > 14 ? values[14] : "" // 兼容旧表格，如果有第15列则读取为背景特效
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
            // 如果台词为空，则隐藏对话框；如果不为空，则显示对话框
            if (dialoguePanel != null) 
            {
                dialoguePanel.SetActive(!string.IsNullOrEmpty(currentLine.content));
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
            // 选项逻辑：遇到选项同样需要停止自动播放，等待玩家做选择
            isAutoPlaying = false;
            if (speedDropdownObj != null) speedDropdownObj.SetActive(false);
            
            currentState = DialogueState.Choosing;
            Debug.Log("生成选项：" + currentLine.content + " -> 点击跳转：" + currentLine.nextID);
            // TODO: 生成 UI Button 并绑定 OnClick 逻辑 ->  PlayLine(currentLine.nextID);
        }
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

        // 解析当前行的特效配置
        string currentEffect = string.IsNullOrEmpty(line.effect) ? "" : line.effect.ToLower();
        string currentBgEffect = string.IsNullOrEmpty(line.bgEffect) ? "" : line.bgEffect;

        // 更换背景: 使用 SpriteRenderer
        if (backgroundRenderer != null && !string.IsNullOrEmpty(line.background))
        {
            if (bgDict.TryGetValue(line.background, out Sprite bgSprite))
            {
                // 如果背景图片换了，或者有背景相关的特效，可以通过特效字段触发
                if (backgroundRenderer.sprite != bgSprite)
                {
                    backgroundRenderer.sprite = bgSprite;
                }
                
                // 播放背景独立动画
                if (!string.IsNullOrEmpty(currentBgEffect))
                {
                    Animator bgAnim = backgroundRenderer.GetComponent<Animator>();
                    if (bgAnim != null)
                    {
                        bgAnim.Play(currentBgEffect, 0, 0f);
                    }
                }
            }
            else
            {
                Debug.LogWarning("无法从素材库中找到背景图片映射: " + line.background);
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

            for (int i = 0; i < positions.Length; i++)
            {
                if (i >= expressions.Length) break;

                string spriteName = expressions[i]; 
                
                if (charDict.TryGetValue(spriteName, out Sprite charSprite))
                {
                    string pos = positions[i].ToLower();
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
        
        if (contentText != null) contentText.text = "";
        
        // 简单打字效果，支持富文本需额外处理
        foreach (char c in textContent.ToCharArray())
        {
            if (skipTyping)
            {
                // 如果跳过，直接显示全部
                if (contentText != null) contentText.text = textContent;
                break;
            }

            if (contentText != null) contentText.text += c;
            
            // 真实等待时间 = 基础出字速度 / 速度倍率
            yield return new WaitForSeconds(typeSpeed / currentSpeedMultiplier);
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
        
        // 记录回来后需要从哪一句开始
        if (!string.IsNullOrEmpty(currentLine.nextID))
        {
            PlayerPrefs.SetString("ResumeDialogueID", currentLine.nextID);
            PlayerPrefs.Save();
        }
    }

    // 解析并执行事件
    private void TriggerGameEvent(string eventParams)
    {
        Debug.Log("触发了特殊事件：" + eventParams);

        if (eventParams.StartsWith("LoadScene:"))
        {
            string sceneName = eventParams.Substring(10); // 截取之后的字符串
            SceneManager.LoadScene(sceneName);
        }
        else 
        {
            // 按照你的思路：如果在事件栏填的是脚本/物体名称，我们直接在场景里找同名的游戏物体
            GameObject eventObj = GameObject.Find(eventParams);
            if (eventObj != null)
            {
                // 发送信号，自动调用该物体身上任意脚本里的 "StartInteraction" 方法
                eventObj.SendMessage("StartInteraction", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning("找不到名为 [" + eventParams + "] 的游戏物体，无法执行对应事件！");
            }
        }
    }

    // 从交互中恢复对话 (给你的交互/小游戏脚本调用的接口)
    public void ResumeFromSuspended()
    {
        if (currentState == DialogueState.Suspended)
        {
            string resumeID = PlayerPrefs.GetString("ResumeDialogueID", "");
            if (!string.IsNullOrEmpty(resumeID))
            {
                PlayerPrefs.DeleteKey("ResumeDialogueID");
                PlayLine(resumeID);
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
    public string type;         // 类型：#, CHOICE, EVENT, COND
    public string id;           // 唯一标识
    public string charName;     // 角色名称
    public string position;     // 立绘位置
    public string content;      // 对话内容/选项文案
    public string nextID;       // 跳转目标
    public string effect;       // 特效/表现 (人物)
    public string sound;        // 音效
    public string background;   // 背景图
    public string promptUI;     // 提示界面预制体名
    public string variable;     // 影响的变量/条件
    public string expression;   // 立绘差分
    public string chapter;      // 章节序号
    public string eventParams;  // 事件参数 (如 LoadScene:Puzzle1)
    public string bgEffect;     // 背景动画特效
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
public struct SpriteMapping
{
    public string key;     // CSV 里填的名字
    public Sprite sprite;  // 对应的图片素材
}

[System.Serializable]
public struct AudioMapping
{
    public string key;     // CSV里填的音效名
    public AudioClip clip; // 对应的音效片段
}

[System.Serializable]
public struct PrefabMapping
{
    public string key;       // CSV里填的特效名
    public GameObject prefab; // 对应的动画/特效预制体 (Prefab)
}