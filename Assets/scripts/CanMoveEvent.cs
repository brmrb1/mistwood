using UnityEngine;

// 用于处理 csv6 中对应的 "canmove" 事件：将 canmove 物体下移 8
public class CanMoveEvent : MonoBehaviour
{
    [Header("移动配置")]
    [Tooltip("需要移动的 canmove 物体。如果为空，将自动查找场景中名为 canmove 的物体")]
    public Transform canMoveObject;
    
    [Tooltip("下移的距离 (单位)，根据需要可以设为 8")]
    public float moveDistance = 8f;

    // 当满足 csv6 触发条件时或由 DialogueManager 调用
    public void StartInteraction()
    {
        // 如果没有在面板上指定，尝试在场景中自动寻找
        if (canMoveObject == null)
        {
            GameObject obj = GameObject.Find("canmove");
            if (obj != null)
            {
                canMoveObject = obj.transform;
            }
            else
            {
                Debug.LogWarning("【CanMoveEvent】场景中未找到名为 'canmove' 的物体！");
                return;
            }
        }

        // 获取原来的位置
        Vector3 pos = canMoveObject.localPosition;
        // 将Y轴下移
        pos.y -= moveDistance;
        // 更新位置
        canMoveObject.localPosition = pos;
        
        Debug.Log($"【CanMoveEvent】已将 {canMoveObject.name} 下移 {moveDistance} 单位，当前 localPosition: {pos}");
        
        // 执行完事件继续主对话
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
        }
    }

    private System.Collections.IEnumerator ResumeDialogueRoutine()
    {
        yield return null;
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ResumeFromSuspended();
        }
    }
}
