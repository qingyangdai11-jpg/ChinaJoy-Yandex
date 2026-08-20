using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.UI;

public class CountdownTimer_OneNumber : MonoBehaviour
{
    [Header("倒计时设置")]
    public int startCount = 5;          // 从5开始倒计时
    public float interval = 1f;         // 每秒更新一次
    public bool autoStart = false;      // 是否自动开始
    public Text text;


    [Header("事件")]
    public UnityEvent OnCountdownFinished; // 倒计时结束事件
    public UnityEvent<int> OnCountdownUpdate; // 倒计时更新事件（带当前数字）

    private Coroutine _countdownCoroutine;

    private void Start()
    {
        if (autoStart)
        {
            StartCountdown();
        }
    }


    [ContextMenu("StartCountdown")]
    /// <summary>
    /// 开始倒计时
    /// </summary>
    public void StartCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
        }
        _countdownCoroutine = StartCoroutine(IE_Countdown());
    }

    /// <summary>
    /// 停止倒计时
    /// </summary>
    public void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    // 倒计时协程
    private IEnumerator IE_Countdown()
    {
        int currentCount = startCount;
        //if(text!=null)  text.text = currentCount.ToString();

        while (currentCount > 0)
        {
            // 触发更新事件（显示当前数字）
            OnCountdownUpdate?.Invoke(currentCount);
            Debug.Log($"倒计时: {currentCount}");

            if (text != null) text.text = currentCount.ToString();

            yield return new WaitForSeconds(interval);
            currentCount--;

        }

        // 倒计时结束
        OnCountdownUpdate?.Invoke(0);
        OnCountdownFinished?.Invoke();
        Debug.Log("倒计时结束！触发事件！");
    }
}
