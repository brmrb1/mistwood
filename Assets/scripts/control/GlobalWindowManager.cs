using UnityEngine;

public class GlobalWindowManager : MonoBehaviour
{
    private static GlobalWindowManager instance;

    private void Awake()
    {
        // 保证此脚本跨场景不销毁且全局唯一
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // 监听 ESC 键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 切换全屏/窗口化状态
            Screen.fullScreen = !Screen.fullScreen;
            
            // 如果需要指定窗口化时的具体分辨率，可以使用下面的替代方案：
            // bool isGoingFullScreen = !Screen.fullScreen;
            // if (isGoingFullScreen)
            // {
            //     // 获取当前显示器的最大分辨率
            //     Resolution maxRes = Screen.resolutions[Screen.resolutions.Length - 1];
            //     Screen.SetResolution(maxRes.width, maxRes.height, true);
            // }
            // else
            // {
            //     // 窗口化时默认设置的分辨率（例如 1280 x 720）
            //     Screen.SetResolution(1280, 720, false);
            // }
        }
    }
}
