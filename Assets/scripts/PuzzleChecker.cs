using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PuzzleAnswer
{
    [Header("需要检查的生成点(Spawn Point)")]
    public Transform targetSpawnPoint;      
    
    [Header("期望这个点上放什么(把你希望的图片直接拖进来)")]
    public dragright expectedItem;    

    [Header("期望它是第几次形态(1 2 或 3)")]
    [Range(1, 3)]
    public int expectedCount;         
}

public class PuzzleChecker : MonoBehaviour
{
    [Header("在下面配置你的标准答案")]
    public List<PuzzleAnswer> correctAnswers;

    [Header("答题反馈位置")]
    public Transform feedbackSpawnPos1;
    public Transform feedbackSpawnPos2;

    [Header("位置1的4种反馈图片(例如：对0个、对1个、对2个、对3个)")]
    public GameObject[] feedbackPrefabs1;

    [Header("位置2的4种反馈图片(同上对应)")]
    public GameObject[] feedbackPrefabs2;

    [Header("统一控制反馈图片的生成大小")]
    public Vector3 uniformFeedbackScale = new Vector3(1f, 1f, 1f);

    [Header("多轮机制：每轮下移设置")]
    [Tooltip("每按一次判定按钮，整体往下偏移多少？(例如Y:-2)")]
    public Vector3 roundOffset = new Vector3(0, -2f, 0); 
    [Tooltip("把需要跟着下移的“空节点”拖进来（包含：所有的 Spawn Point、反馈位置、以及拖拽接收的目标点）")]
    public List<Transform> transformsToShiftDown;

    // 这个方法用来在 Unity 面板中绑定给 Button (OnClick) 事件
    public void CheckResult()
    {
        // 1. 获取全局正在被占据的所有的坑位和上面的物品
        var currentPlacements = dragright.GetCurrentPlacements();
        
        int correctCount = 0;

        // 2. 遍历所有的标准答案进行核对
        foreach (var answer in correctAnswers)
        {
            // 如果这个坑位上根本没放东西
            if (!currentPlacements.ContainsKey(answer.targetSpawnPoint))
            {
                continue; // 没放东西算错，直接跳过看下一个
            }

            // 获取目前放在这个坑位上的物体
            dragright itemOnPoint = currentPlacements[answer.targetSpawnPoint];

            // 核对物体对不对，以及它变身的次数形态对不对
            if (itemOnPoint == answer.expectedItem && itemOnPoint.CurrentSuccessCount == answer.expectedCount)
            {
                correctCount++; // 完全匹配，答对数量+1
            }
        }

        // 3. 【已移除销毁旧反馈的代码】：让历史上生成过的图片永远保留在原处！

        // 4. 根据答对的数量（0、1、2、3），决定生成哪一张反馈图片
        // 限制防错：确保不会超出数组范围 (假设最多只有4种形态对应下标0~3)
        int feedbackIndex = Mathf.Clamp(correctCount, 0, 3);

        Debug.Log($"结算完成！玩家共答对了 {correctCount} 个，现在生成第 {feedbackIndex + 1} 种反馈形态。");

        if (feedbackPrefabs1 != null && feedbackIndex < feedbackPrefabs1.Length && feedbackPrefabs1[feedbackIndex] != null)
        {
            GameObject fb1 = Instantiate(feedbackPrefabs1[feedbackIndex], feedbackSpawnPos1.position, Quaternion.identity);
            fb1.transform.localScale = uniformFeedbackScale;
        }
                
        if (feedbackPrefabs2 != null && feedbackIndex < feedbackPrefabs2.Length && feedbackPrefabs2[feedbackIndex] != null)
        {
            GameObject fb2 = Instantiate(feedbackPrefabs2[feedbackIndex], feedbackSpawnPos2.position, Quaternion.identity);
            fb2.transform.localScale = uniformFeedbackScale;
        }

        // 5. 准备开启新的一轮！
        
        // (1) 通知所有拖拽物品退回原位，且代码“失忆”（不再记录上一轮的生成图，让上一轮的图留作历史）
        dragright.StartNewRound();

        // (2) 把所有指定的工作区标志点，一口气整体往下平移
        if (transformsToShiftDown != null)
        {
            foreach (var t in transformsToShiftDown)
            {
                if (t != null)
                {
                    t.position += roundOffset;
                }
            }
        }
    }
}
