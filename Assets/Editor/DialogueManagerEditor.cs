using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(DialogueManager))]
public class DialogueManagerEditor : Editor
{
    private bool audioLibraryFolded = true;
    private bool characterLibraryFolded = true;
    private bool backgroundLibraryFolded = true;
    private bool effectLibraryFolded = true;

    public override void OnInspectorGUI()
    {
        // 先绘制默认的 Inspector 视图（除了我们要自定义的部分）
        // 如果你想完全自定义，可以不调这个，但 DialogueManager 参数很多，建议保留
        DrawDefaultInspector();

        DialogueManager script = (DialogueManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("【快捷操作面板】", EditorStyles.boldLabel);

        // --- 批量导入音效 ---
        if (GUILayout.Button("批量添加音效 (自动读取文件名)"))
        {
            AddSelectedAudiosToLibrary(script);
        }

        // --- 批量导入背景 ---
        if (GUILayout.Button("批量添加背景图 (自动读取文件名)"))
        {
            AddSelectedSpritesToLibrary(script, true);
        }

        // --- 批量导入立绘 ---
        if (GUILayout.Button("批量添加角色立绘 (自动读取文件名)"))
        {
            AddSelectedSpritesToLibrary(script, false);
        }
    }

    private void AddSelectedAudiosToLibrary(DialogueManager script)
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(AudioClip), SelectionMode.DeepAssets);
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Project 面板选中至少一个音频文件！", "确定");
            return;
        }

        Undo.RecordObject(script, "Batch Add Audio Clips");
        foreach (var obj in selectedObjects)
        {
            AudioClip clip = (AudioClip)obj;
            // 如果库里已经有了同名的 Clip，就不重复添加
            if (script.audioLibrary.Exists(x => x.clip == clip)) continue;

            script.audioLibrary.Add(new AudioMapping(clip.name, clip));
        }
        EditorUtility.SetDirty(script);
        Debug.Log($"成功添加了 {selectedObjects.Length} 个音频到 Audio Library");
    }

    private void AddSelectedSpritesToLibrary(DialogueManager script, bool isBackground)
    {
        // 改进：不仅获取 Sprite，还尝试获取 Texture2D 并从中提取 Sprite
        Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        List<Sprite> foundSprites = new List<Sprite>();

        foreach (var obj in selectedObjects)
        {
            if (obj is Sprite s)
            {
                foundSprites.Add(s);
            }
            else if (obj is Texture2D tex)
            {
                // 如果选中的是贴图，尝试加载它对应的 Sprite 资源
                string path = AssetDatabase.GetAssetPath(tex);
                Sprite spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spriteAsset != null)
                {
                    foundSprites.Add(spriteAsset);
                }
            }
        }

        if (foundSprites.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到有效的 Sprite 资源！\n请确保图片导入设置中的 Texture Type 已设置为 'Sprite (2D and UI)'。", "确定");
            return;
        }

        Undo.RecordObject(script, "Batch Add Sprites");
        foreach (var sprite in foundSprites)
        {
            if (isBackground)
            {
                if (script.backgroundLibrary.Exists(x => x.sprite == sprite)) continue;
                script.backgroundLibrary.Add(new SpriteMapping(sprite.name, sprite));
            }
            else
            {
                if (script.characterLibrary.Exists(x => x.sprite == sprite)) continue;
                script.characterLibrary.Add(new SpriteMapping(sprite.name, sprite));
            }
        }
        EditorUtility.SetDirty(script);
        string typeStr = isBackground ? "背景图" : "角色立绘";
        Debug.Log($"成功添加了 {foundSprites.Count} 个{typeStr}到库中");
    }
}
