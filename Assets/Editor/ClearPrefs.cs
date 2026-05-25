using UnityEngine;
using UnityEditor;

public class ClearPrefs
{
    [MenuItem("Tools/Clear PlayerPrefs")]
    public static void ClearAllPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("所有 PlayerPrefs (存档数据) 已清除！");
    }
}