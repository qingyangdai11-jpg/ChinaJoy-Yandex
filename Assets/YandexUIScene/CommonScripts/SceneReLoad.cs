using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneReLoad : MonoBehaviour
{
    public Image image_black;

    private void Awake()
    {
        EnsureImageBlackReference();
        EnsureTopCanvasSorting();

        if (image_black != null)
        {
            image_black.gameObject.SetActive(true);
            image_black.color = new Color(0f, 0f, 0f, 1f); // 场景重载 Awake 首帧预锁死纯黑，彻底防止画面闪现
        }
    }

    private void EnsureImageBlackReference()
    {
        if (image_black == null)
        {
            // Try to find Canvas-black or Canvas-balck (typo)
            var blackCanvas = GameObject.Find("Canvas-black");
            if (blackCanvas == null)
            {
                blackCanvas = GameObject.Find("Canvas-balck");
            }
            if (blackCanvas != null)
            {
                image_black = blackCanvas.GetComponentInChildren<Image>(true);
            }

            // Fallback: search for any Image component on a GameObject containing black or balck in its name
            if (image_black == null)
            {
                var blackImgGo = GameObject.Find("Image-black");
                if (blackImgGo == null) blackImgGo = GameObject.Find("image_black");
                if (blackImgGo == null) blackImgGo = GameObject.Find("Image_black");
                if (blackImgGo != null)
                {
                    image_black = blackImgGo.GetComponent<Image>();
                }
            }
        }
    }

    private void EnsureTopCanvasSorting()
    {
        if (image_black != null)
        {
            Canvas canvas = image_black.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 9999; // 最高层级覆盖，防止被其他 Panel 遮挡闪烁
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Loading();
    }

    public void Loading()
    {
        EnsureImageBlackReference();
        if (image_black == null)
        {
            Debug.LogWarning("[SceneReLoad] image_black is null! Cannot execute Loading fade transition.");
            return;
        }

        EnsureTopCanvasSorting();
        image_black.gameObject.SetActive(true);
        image_black.DOKill();
        image_black.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() => { image_black.gameObject.SetActive(false); });
    }

    public bool isReloading { get; private set; } = false;

    [ContextMenu("SceneReLoad")]
    public void Reload()
    {
        isReloading = true;
        EnsureImageBlackReference();
        if (image_black == null)
        {
            Debug.LogWarning("[SceneReLoad] image_black is null! Reloading scene immediately without fade.");
            Time.timeScale = 1f; // Reset timescale for the reloaded scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        EnsureTopCanvasSorting();
        image_black.gameObject.SetActive(true);
        image_black.DOKill();
        image_black.DOFade(1f, 0.4f).SetUpdate(true).OnComplete(() => { 
            Time.timeScale = 1f; // Reset timescale for the reloaded scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
        });
    }
    public void QuitGame()
    {
        Application.Quit();

    }
}
