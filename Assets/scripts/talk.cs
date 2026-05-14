using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
public class talk : MonoBehaviour
{
    //对话文本文件
    public TextAsset dialogDataFile;
    //左侧角色图像 (恢复为SpriteRenderer类型)
    public SpriteRenderer SpriteLeft;
    //右侧角色图像 (恢复为SpriteRenderer类型)
    public SpriteRenderer SpriteRight;
    //角色名字文本
    public TMP_Text NameText;
    //对话内容文本
    public TMP_Text DialogText;
    //角色图片列表
    // 原本只能装两张图，建议把所有的立绘（不同角色的所有表情差分图）都拖到这个 List 里
    public List<Sprite> sprites = new List<Sprite>();
    // 原本用来存名字对应的单一图片，现在废弃
    // Dictionary<string, Sprite> imageDic = new Dictionary<string, Sprite>();
    
    // 【新增】根据差分名字存图片的字典。例如把 "舒悦杉_开心", "莉莉白_生气" 这些名字作为键
    Dictionary<string, Sprite> imageDic = new Dictionary<string, Sprite>();

    //当前对话索引
    public int dialogIndex;
    //对话文本，按行分割
    public string[] dialogRows;
    //继续按钮
    public Button nextButton;
    //选项按钮预制体
    public GameObject optionButton;
    //选项按钮父节点,用于自动排列
    public Transform buttonGroup;

    [Header("UI底图控制 (拖入场景里的图片节点)")]
    public GameObject nameBoxParent;   // 名字底图节点
    public GameObject dialogBoxParent; // 对话框底图节点

    public List<Person> people = new List<Person>();
    private void Awake()
    {
        // 自动用 sprite 的原本名称作为键存入字典，要求：面板中拖进去的 Sprite 名字就是差分名字。
        // 例如："舒悦杉", "舒悦杉_笑", "莉莉白", "莉莉白_哭"
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                imageDic[sprites[i].name] = sprites[i];
            }
        }
        
        Person person = new Person();
        person.name = "舒悦杉";
        people.Add(person);
        Person girl = new Person();
        girl.name = "莉莉白";
        people.Add(girl);
    
    }
    [Header("章节控制")]
    // 新增：当前执行的章节编号，0表示begin的对话，1表示guide的对话等
    public int currentChapter = 0;

    [Header("延迟开始对话的时间 (例如配合开场动画)")]
    public float startDelay = 3.1f;

    [Header("第一次对话结束演出效果")]
    public SpriteRenderer blackScreen; // 你的带帧动画的 Sprite 遮罩
    public Animator blackScreenAnimator; // 【新增】如果你的黑屏是用Animator播动画的，拖入这里
    public string endAnimationTrigger = "PlayEnd"; // 【新增】触发黑屏动画的 Trigger 名字
    
    [Header("最后一次大结局演出效果")]
    public GameObject finalOutScreen; // 【改成 GameObject，兼容你的 UI Image】
    public Animator finalOutAnimator;     // 第二个不同的动画控制器

    [Header("对话结束显示内容")]
    public float waitBeforeShowImage = 1f; // 播放黑屏动画后等待多久出现按钮
    public GameObject firstEndingImage;    // 【新增】图片1：和黑屏动画一起出现的图片
    public GameObject endingButton;        // 对话结束后出现用来点击的按钮
    public GameObject finalImageToShow;    // 【图片2】点开按钮后显示出来的图片节点
    public GameObject finalWordOrGroup;    // 点开按钮后显示出来的文字或图文组节点

    void Start()
    {
        ReadText(dialogDataFile);

        // 如果是通过读取存档进入这个场景的，恢复之前记录的对话进度
        if (PlayerPrefs.HasKey("TargetLoadDialogIndex"))
        {
            dialogIndex = PlayerPrefs.GetInt("TargetLoadDialogIndex");
            // 读取完成之后将以此标记清空，避免下次重新开始游戏时被影响
            PlayerPrefs.DeleteKey("TargetLoadDialogIndex");
        }

        // 【新增自动匹配】
        // 自动检索当前章节的起始ID。如果不加这个，在guide里章节是1，但dialogIndex默认是0，
        // 游戏就会去寻找ID为0的对话进行播放，从而全都被跳过，导致无UI和对话出现！
        if (dialogRows != null)
        {
            bool foundIndex = false;
            int firstIdInChapter = -1;
            for (int i = 0; i < dialogRows.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(dialogRows[i])) continue;
                string[] cells = dialogRows[i].Split(',');
                if (cells.Length < 2) continue;

                if (int.TryParse(cells[1].Trim(), out int rId))
                {
                    bool matchChapter = true; // 默认匹配，用于兼容旧的没有章节栏的行
                    // 章节ID挪到了更后面一列(原为cells[8], 现在是cells[9] 如果有的话)
                    if (cells.Length > 9 && !string.IsNullOrWhiteSpace(cells[9]))
                    {
                        if (int.TryParse(cells[9].Trim(), out int chapterId))
                        {
                            matchChapter = (chapterId == currentChapter);
                        }
                    }

                    if (matchChapter)
                    {
                        if (firstIdInChapter == -1) firstIdInChapter = rId;
                        if (rId == dialogIndex)
                        {
                            foundIndex = true; // 当前设定的 dialogIndex 确实属于当前章节
                            break;
                        }
                    }
                }
            }

            // 如果当前的 dialogIndex (如默认值0) 根本不在这个章节内，将其强制设为本章节的起始ID
            if (!foundIndex && firstIdInChapter != -1)
            {
                dialogIndex = firstIdInChapter;
            }
        }
        
        // 游戏刚开始时，先强制把所有对话UI、按钮和立绘隐藏起来
        SetDialogUIActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (SpriteLeft != null) SpriteLeft.gameObject.SetActive(false);
        if (SpriteRight != null) SpriteRight.gameObject.SetActive(false);
        
        // 【新增】：游戏刚开始时，强制把最终大结局的黑屏关掉，防止它开局乱播！
        if (finalOutScreen != null) finalOutScreen.SetActive(false);
        
        // 延迟 startDelay 秒（默认3.1秒，刚好让刚才的动画播完）之后，再显示第一句对话
        Invoke("ShowDialogRow", startDelay);
    }
    //更新对话文本和角色名字
    public void UpdateText(string _name,string _text)
    {
        NameText.text = _name;
        DialogText.text = _text;
    
    }
    //更新角色图像
    // _name 仍然用于名字框展示，_emotionName 用于在字典里查找差分立绘
    public void UpdateImage(string _name, string _position, string _emotionName = "")
    {
       // 清理所有可能的隐藏字符：空格、换行、制表、还有最容易导致这个错误的 BOM 和 零宽空格 
       _name = _name.Trim(' ', '\r', '\n', '\t', '\uFEFF', '\u200B');
       _position = _position.Trim(' ', '\r', '\n', '\t', '\uFEFF', '\u200B');
       _emotionName = _emotionName.Trim(' ', '\r', '\n', '\t', '\uFEFF', '\u200B');

       // 如果传入了差分名，优先使用差分名；如果没有，退回使用默认名字
       string keyToSearch = string.IsNullOrEmpty(_emotionName) ? _name : _emotionName;

       if (imageDic.TryGetValue(keyToSearch, out Sprite sprite))
       {
           if (_position == "左")        
           {
               if (SpriteLeft != null) { SpriteLeft.gameObject.SetActive(true); SpriteLeft.sprite = sprite; }
               // 显示左边时，强制隐藏右边
               if (SpriteRight != null) SpriteRight.gameObject.SetActive(false); 
           }
           else if (_position == "右")        
           {   
               // 显示右边时，强制隐藏左边
               if (SpriteLeft != null) SpriteLeft.gameObject.SetActive(false);
               if (SpriteRight != null) { SpriteRight.gameObject.SetActive(true); SpriteRight.sprite = sprite; }
           }
       }
      }
     public void ReadText(TextAsset _textAsset)
     {
        dialogRows = _textAsset.text.Split('\n');
     }
     public void ShowDialogRow()
     {
        for (int i = 0; i < dialogRows.Length; i++)
        {
            // 防报错：如果这行表格是空的，直接跳过
            if (string.IsNullOrWhiteSpace(dialogRows[i])) continue;
            string[] cells = dialogRows[i].Split(',');
            if (cells.Length < 2) continue;

            string mark = cells[0].Trim();
            int rowId;
            // 防报错：如果表格里的 ID 写了空格没法转化为数字，就跳过
            if (!int.TryParse(cells[1].Trim(), out rowId)) continue; 

            // 【新增】判断章节是否匹配（如果CSV中有第10列即 cells[9] 并且填了数字）
            if (cells.Length > 9 && !string.IsNullOrWhiteSpace(cells[9]))
            {
                if (int.TryParse(cells[9].Trim(), out int chapterId))
                {
                    // 如果这行的章节和当前设定的章节不同，则跳过不执行
                    if (chapterId != currentChapter) continue; 
                }
            }

            if (mark == "#" && rowId == dialogIndex)
            {
                SetDialogUIActive(true); // 说话时显示底线和文本

                // 【极其关键修复】：回到正常对话时，彻底关掉残留的黑屏！
                if (blackScreen != null) blackScreen.gameObject.SetActive(false);
                if (finalOutScreen != null) finalOutScreen.SetActive(false);

                UpdateText(cells[2], cells[4]);

            // 解析立绘差分名：因为在CSV中“立绘”列是第9列（也就是 cells[8]），所以读取 cells[8]
            string emotionName = "";
            // 原来判断章节的代码占用了 cells[8]，由于你修改了表结构，现在的立绘在 H 或 I 列
            // 从你的截图中看：
            // B列: 人物 (cells[2]因为 A列ID前可能有符号或空白，如果A列是cells[0], B列ID是cells[1], C列人物是cells[2], D列位置是cells[3], E列内容是cells[4], F列跳转是cells[5], G列效果是cells[6], H列目标是cells[7], I列立绘是cells[8])
            if (cells.Length > 8 && !string.IsNullOrWhiteSpace(cells[8]))
            {
                emotionName = cells[8].Trim();
            }
                
            UpdateImage(cells[2], cells[3], emotionName);

                // 安全读取下一句的索引 (如果在表格里空着没写，就会默认 + 1)
                int nextIndex = dialogIndex + 1;
                if (cells.Length > 5 && !string.IsNullOrWhiteSpace(cells[5]))
                {
                    int.TryParse(cells[5].Trim(), out nextIndex);
                }
                dialogIndex = nextIndex;

                if (nextButton != null) nextButton.gameObject.SetActive(true);
                else Debug.LogWarning("未绑定 nextButton，玩家无法继续对话！");
                break;
            }
            else if (mark == "&" && rowId == dialogIndex)
            {
                SetDialogUIActive(true);
                if (blackScreen != null) blackScreen.gameObject.SetActive(false);
                if (finalOutScreen != null) finalOutScreen.SetActive(false);
                if (nextButton != null) nextButton.gameObject.SetActive(false);
                GenerateOption(i);
                break; // 增加了 break，避免生成重复的选项按钮
            }
            else if (mark == "END" && rowId == dialogIndex)
            {
                Debug.Log("对话结束, 开始闭眼效果与镜头移动");
                SetDialogUIActive(false); // 结束时隐藏底图和文本
                if (nextButton != null) nextButton.gameObject.SetActive(false);
                
                // 对话结束时清理左右立绘
                if (SpriteLeft != null) SpriteLeft.gameObject.SetActive(false);
                if (SpriteRight != null) SpriteRight.gameObject.SetActive(false);

                int nextIndex = dialogIndex + 1; 
                if (cells.Length > 5 && !string.IsNullOrWhiteSpace(cells[5]))
                {
                    int.TryParse(cells[5].Trim(), out nextIndex);
                }
                dialogIndex = nextIndex;

                string endType = "";
                if (cells.Length > 2) endType = cells[2].Trim();

                StartCoroutine(EndingSequence(endType));
                break;
            }
            else if (mark == "PAUSE" && rowId == dialogIndex)
            {
                Debug.Log("遇到 PAUSE 符号，立刻暂停并隐藏对话...");
                SetDialogUIActive(false); 
                if (nextButton != null) nextButton.gameObject.SetActive(false);

                int nextIndex = dialogIndex + 1; 
                if (cells.Length > 5 && !string.IsNullOrWhiteSpace(cells[5]))
                {
                    int.TryParse(cells[5].Trim(), out nextIndex);
                }
                dialogIndex = nextIndex;

                string pauseType = "";
                if (cells.Length > 2) pauseType = cells[2].Trim();
                ExecutePause(pauseType);
                break;
            }
        } 
     }

     // 【新增】负责分配和执行不同种类 PAUSE 的功能
     public void ExecutePause(string pauseType)
     {
         switch (pauseType)
         {
             case "小游戏1":
                 Debug.Log("PAUSE功能：弹出第一个小游戏");
                 // 在这里写激活小游戏的防法，比如: puzzleManager.SetActive(true);
                 break;

             case "展示道具":
                 Debug.Log("PAUSE功能：展示某个道具特写");
                 // 在这里写展示道具的代码
                 break;

             default:
                 // 如果没填类型，就纯暂停，什么都不干
                 break;
         }
     }

     // 【新增方法】留给外部脚本、外部按钮、倒计时等调用的“恢复对话”接口
     public void ResumeDialog()
     {
         Debug.Log("外部触发！继续被挂起的对话！");
         SetDialogUIActive(true);
         ShowDialogRow();
     }

     // 【修改】结局演出协程支持判断类型
     private IEnumerator EndingSequence(string endType)
     {
         switch (endType)
         {
             case "黑屏":
                 // === 类型1：只发生黑屏和图片1，并在几秒后【自动】继续下一句 ===
                 if (blackScreen != null) blackScreen.gameObject.SetActive(true);
                 if (firstEndingImage != null) firstEndingImage.SetActive(true);
                 if (blackScreenAnimator != null) blackScreenAnimator.Play("llbblink", 0, 0f);

                 yield return new WaitForSeconds(waitBeforeShowImage);

                 // 自动进入后续对话 (52句及以后)
                 if (dialogIndex != -1)
                 {
                     SetDialogUIActive(true);
                     ShowDialogRow();
                 }
                 break;

             case "结局按钮":
                 // === 类型2：纯显示按钮，等待玩家点击 ===
                 if (endingButton != null) endingButton.SetActive(true);
                 break;

             case "大结局黑屏":
                 // === 类型3：使用独立的大结局黑屏物体和动画 ===
                 if (finalOutScreen != null) finalOutScreen.SetActive(true);
                 if (firstEndingImage != null) firstEndingImage.SetActive(true);
                 
                 // 播放指定的最终动画
                 if (finalOutAnimator != null) finalOutAnimator.Play("out", 0, 0f);

                 yield return new WaitForSeconds(waitBeforeShowImage);

                 SceneManager.LoadScene("guide");
                 break;

             default:
                 // === 默认：原来的逻辑（黑屏 + 随后出现结局按钮） ===
                 if (blackScreen != null)
                 {
                     blackScreen.gameObject.SetActive(true);
                     if (firstEndingImage != null) firstEndingImage.SetActive(true);
                     if (blackScreenAnimator != null) blackScreenAnimator.Play("llbblink", 0, 0f);
                 }

                 yield return new WaitForSeconds(waitBeforeShowImage);

                 if (endingButton != null)
                 {
                     endingButton.SetActive(true);
                 }
                 else if (finalImageToShow != null)
                 {
                     finalImageToShow.SetActive(true);
                     if (finalWordOrGroup != null) finalWordOrGroup.SetActive(true);
                 }
                 break;
         }
     }

     // 【新增】绑定给结局按钮的点击事件
     public void OnEndingButtonClick()
     {
         if (endingButton != null) endingButton.SetActive(false); // 点击后隐藏按钮 itself
         if (finalImageToShow != null) finalImageToShow.SetActive(true); // 显示结局图片
         if (finalWordOrGroup != null) finalWordOrGroup.SetActive(true); // 显示结局文字

         // 【新增】点完按钮后，如果还有下一句话，重新显示对话框
         if (dialogIndex != -1)
         {
             SetDialogUIActive(true);
             ShowDialogRow();
         }
     }

     // 【新增方法】统一控制底图和文本的显示/隐藏
     public void SetDialogUIActive(bool isActive)
     {
         if (nameBoxParent != null) nameBoxParent.SetActive(isActive);
         if (dialogBoxParent != null) dialogBoxParent.SetActive(isActive);
         
         // 为了保险起见，如果文本和底图是分开的，这里也顺便把文本开关一下
         if (NameText != null && NameText.gameObject != nameBoxParent) 
             NameText.gameObject.SetActive(isActive);
             
         if (DialogText != null && DialogText.gameObject != dialogBoxParent) 
             DialogText.gameObject.SetActive(isActive);
     }
     public void OnClickNext()
     {
        ShowDialogRow();
     }

     public void GenerateOption(int _index)
     {
        string[] cells = dialogRows[_index].Split(',');
        if (cells[0] =="&")
        {
            GameObject button = Instantiate(optionButton, buttonGroup);
            //绑定按钮事件
            button.GetComponentInChildren<TMP_Text>().text = cells[4];
            button.GetComponent<Button>().onClick.AddListener
            (
                delegate 
                { 
                    OnOptionClick(int.Parse(cells[5])); 
                    if (cells[6] != "")
                    {
                        Debug.Log("执行选项效果");
                        string[] effect = cells[6].Split('@');
                        cells[7] = Regex.Replace(cells[7], @"[\r\n\t ]", ""); //去除目标字符串中的所有空白字符
                        OptionEffect(effect[0], int.Parse(effect[1]), cells[7]);
                    }
                }
            );
            GenerateOption(_index + 1);
        }
     } 
     public void OnOptionClick(int _id)
     {
        dialogIndex = _id;
        ShowDialogRow();
        for (int i = 0; i < buttonGroup.childCount; i++)
        {
            Destroy(buttonGroup.GetChild(i).gameObject);
        }
     }
     public void OptionEffect(string _effect, int _param, string _target)
     {
        if (_effect == "好感度加")
        {
            foreach (var person in people)
            {
                if (person.name == _target)
                {
                    person.likeValue += _param;
                }
            }
        }
        //在这里添加选项效果的逻辑，比如改变角色好感度、触发事件等
     }
}
