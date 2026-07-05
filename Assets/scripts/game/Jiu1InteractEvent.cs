using UnityEngine;

// 挂载到名为 "jiu1" 的空物体上。
// 当 CSV 对话表里填了 "jiu1" 事件时，DialogueManager 会唤醒并调用这个 StartInteraction。
public class Jiu1InteractEvent : MonoBehaviour
{
    [Header("音效")]
    public AudioClip interactSfx;

    // 对话管理器会调用此方法
    public void StartInteraction()
    {
        PlayInteractSfx();
        // 寻找到项目中所有的 dragright 脚本对象
        dragright[] allDrags = Resources.FindObjectsOfTypeAll<dragright>();
        
        int count = 0;
        foreach (var drag in allDrags)
        {
            // 确保只处理当前加载在场景里的物体，而不是你的硬盘预制体(Prefab)
            if (drag.gameObject.scene.IsValid())
            {
                drag.enabled = true; // 激活 dragright 脚本
                
                BoxCollider2D box = drag.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    box.enabled = true; // 激活 BoxCollider2D
                }

                // 也确保物体本身是 active 的，如果是被隐藏的这里可以顺带激活它
                // 【改进】如果该物体已经在存档里记录为已完成（>=3次），则不要在这个事件启动时强行把它重新激活显示出来
                if (!drag.gameObject.activeSelf && drag.CurrentSuccessCount < 3)
                {
                    drag.gameObject.SetActive(true);
                }
                
                count++;
            }
        }
        
        Debug.Log($"【jiu1事件】已成功激活场景中的 {count} 个 dragright 及其 BoxCollider2D。");
    }

    private void PlayInteractSfx()
    {
        if (interactSfx == null) return;
        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(interactSfx, playPosition);
    }
}
