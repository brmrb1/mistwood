using UnityEngine;

public class DisableAfterAwake : MonoBehaviour
{
    public GameObject blurVision; // 把你的模糊图片拖进来

    void Start()
    {
        // 假设你的动画长度是3秒，我们等3.1秒后把这两个图层关掉以免挡住鼠标点击
        Invoke("TurnOff", 3.1f);
    }

    void TurnOff()
    {
        if (blurVision != null) blurVision.SetActive(false);
        gameObject.SetActive(false);
    }
}