using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
public class talk : MonoBehaviour
{
    //对话文本文件
    public TextAsset dialogDataFile;
    //左侧角色图像
    public SpriteRenderer SpriteLeft;
    //右侧角色图像
    public SpriteRenderer SpriteRight;
    //角色名字文本
    public TMP_Text NameText;
    //对话内容文本
    public TMP_Text DialogText;
    //角色图片列表
    public List<Sprite> sprites = new List<Sprite>();
    //角色名字对应图片的字典
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
    public List<Person> people = new List<Person>();
    private void Awake()
    {
        imageDic["舒悦杉"] = sprites[0];
        imageDic["塞塔"] = sprites[1];
        Person person = new Person();
        person.name = "舒悦杉";
        people.Add(person);
        Person girl = new Person();
        girl.name = "塞塔";
        people.Add(girl);
    
    }
    void Start()
    {
        ReadText(dialogDataFile);
        ShowDialogRow();
        //UpdateText("塞塔", "从前有座山");
        //UpdateImage("舒悦杉", false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    //更新对话文本和角色名字
    public void UpdateText(string _name,string _text)
    {
        NameText.text = _name;
        DialogText.text = _text;
    
    }
    //更新角色图像
    public void UpdateImage(string _name, string _position)
    {
       // 清理所有可能的隐藏字符：空格、换行、制表、还有最容易导致这个错误的 BOM 和 零宽空格 
       _name = _name.Trim(' ', '\r', '\n', '\t', '\uFEFF', '\u200B');
       _position = _position.Trim(' ', '\r', '\n', '\t', '\uFEFF', '\u200B');

       if (imageDic.TryGetValue(_name, out Sprite sprite))
       {
           if (_position == "左")        
           {
               SpriteLeft.sprite = sprite;
           }
           else if (_position == "右")        
           {   
               SpriteRight.sprite = sprite;
           }
       }
       else if (!string.IsNullOrEmpty(_name))
       {
           Debug.LogError($"找不到角色图像的字典键：[{_name}]，字符串长度：{_name.Length}。请检查CSV文件里该名字前后是否有特殊符号或错别字。");
       }
     }
     public void ReadText(TextAsset _textAsset)
     {
        dialogRows = _textAsset.text.Split('\n');
        //foreach (var row in rows)
       // {
        //    string[] cell = row.Split(',');
        //}
        Debug.Log("123");
     }
     public void ShowDialogRow()
     {
        for (int i = 0; i < dialogRows.Length; i++)
        {
            string[] cells = dialogRows[i].Split(',');
            if (cells[0] == "#" && int.Parse(cells[1]) == dialogIndex)
            {
                UpdateText(cells[2], cells[4]);
                UpdateImage(cells[2], cells[3]);

                dialogIndex = int.Parse(cells[5]);
                nextButton.gameObject.SetActive(true);
                break;
            }
            else if (cells[0] == "&" && int.Parse(cells[1]) == dialogIndex)
            {
                nextButton.gameObject.SetActive(false);
                GenerateOption(i);
            }
            else if (cells[0] == "END" && int.Parse(cells[1]) == dialogIndex)
            {
                Debug.Log("对话结束");
                //可以在这里添加对话结束的逻辑，比如关闭对话框等
            }
        } 

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
