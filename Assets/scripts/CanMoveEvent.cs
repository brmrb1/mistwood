using UnityEngine;

// 用于处理 csv6 中对应的 "canmove" 事件：将 canmove 物体下移 8
public class CanMoveEvent : MonoBehaviour
{
    [Header("移动配置")]
    [Tooltip("需要移动的 canmove 物体。如果为空，将自动查找场景中名为 canmove 的物体")]
    public Transform canMoveObject;
    
    [Tooltip("下移的距离 (单位)，根据需要可以设为 8")]
    public float moveDistance = 8f;

    [Header("音效")]
    public AudioClip interactSfx;

    private bool hasMoved = false;
    public bool HasMoved => hasMoved;

    private void Start()
    {
        // 尝试自动填充引用
        if (canMoveObject == null)
        {
            GameObject obj = GameObject.Find("canmove");
            if (obj != null) canMoveObject = obj.transform;
        }

        // 读取存档
        if (PlayerPrefs.HasKey("TargetLoadSlot"))
        {
            int slot = PlayerPrefs.GetInt("TargetLoadSlot");

            // 【核心逻辑优化】读档前先检查“存档是否有效”。如果没有该档位的时间记录，说明是空档或已删除，拒绝加载数据。
            if (!PlayerPrefs.HasKey("SaveTime_" + slot))
            {
                return;
            }

            // 【修复】存档键值应包含场景名称，以区分不同关卡的独立 canmove 状态
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string key = "CanMove_" + slot + "_" + sceneName + "_" + gameObject.name;
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                ApplyMove();
            }
        }
    }

    private void ApplyMove()
    {
        if (canMoveObject == null) return;
        if (hasMoved) return;

        Vector3 pos = canMoveObject.localPosition;
        pos.y -= moveDistance;
        canMoveObject.localPosition = pos;
        hasMoved = true;
        
        Debug.Log($"【CanMoveEvent】已恢复 {canMoveObject.name} 位移状态，当前 localPosition: {pos}");
    }

    // 当满足 csv6 触发条件时或由 DialogueManager 调用
    public void StartInteraction()
    {
        PlayInteractSfx();
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

        if (!hasMoved)
        {
            ApplyMove();
        }
        else
        {
            Debug.Log("【CanMoveEvent】物体已经处于位移后的位置，跳过。");
        }
        
        // 执行完事件继续主对话
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartCoroutine(ResumeDialogueRoutine());
        }
    }

    private void PlayInteractSfx()
    {
        if (interactSfx == null) return;
        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(interactSfx, playPosition);
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
