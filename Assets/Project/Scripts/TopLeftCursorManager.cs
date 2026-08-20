using UnityEngine;
using System.Runtime.InteropServices;

public class TopLeftCursorManager : MonoBehaviour
{
    // 调用 Windows 系统的鼠标定位功能
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    private bool isLocked = true;

    void Start()
    {
        // 1. 隐藏鼠标
        Cursor.visible = false;

        // 2. 将光标限制在游戏窗口内
        Cursor.lockState = CursorLockMode.Confined;
    }

    void Update()
    {
        // 如果处于锁定状态，每一帧都把鼠标死死按在左上角 (0, 0) 的位置
        if (isLocked)
        {
            SetCursorPos(0, 0);
        }

        // 按下 Esc 键时解锁并显示鼠标
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isLocked = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}