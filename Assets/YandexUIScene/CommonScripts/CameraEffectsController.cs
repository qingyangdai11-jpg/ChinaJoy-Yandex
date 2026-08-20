using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DG.Tweening;

/// <summary>
/// CameraEffectsController 脚本
/// 作用：
/// 1. 负责管理基于玩家移动速度的色像差 (Chromatic Aberration) 动态表现与视场 (FOV) 平滑拉伸效果。
/// 2. 自动检索或创建全局 Volume Profile，确保场景无需手动挂载组件即可开箱即用。
/// 3. 提供障碍物碰撞时的相机轻微震动与偏移接口 (TriggerObstacleShake)，并在震动完毕后精确平滑还原机位。
/// </summary>
public class CameraEffectsController : MonoBehaviour
{
    private static CameraEffectsController _instance;
    public static CameraEffectsController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CameraEffectsController>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CameraEffectsController");
                    _instance = go.AddComponent<CameraEffectsController>();
                }
            }
            return _instance;
        }
    }

    [Header("Camera Settings")]
    [Tooltip("目标摄像机，留空则自动获取 MainCamera")]
    public Camera targetCamera;

    [Tooltip("基础视场角度 (默认 60°)")]
    public float baseFOV = 60f;

    [Tooltip("速度达到最大时的 FOV 最大拉伸增量")]
    public float maxFOVOffset = 18f;

    [Tooltip("FOV 缓动平滑速度")]
    public float fovLerpSpeed = 4f;

    [Header("Chromatic Aberration Settings")]
    [Tooltip("后处理 Volume，留空则自动检索或创建")]
    public Volume targetVolume;

    [Tooltip("速度达到最大时的色像差最大强度")]
    public float maxChromaticIntensity = 0.75f;

    [Tooltip("色像差缓动平滑速度")]
    public float chromaticLerpSpeed = 5f;

    [Header("Obstacle Shake Settings")]
    [Tooltip("默认震动持续时间（秒）")]
    public float defaultShakeDuration = 0.35f;

    [Tooltip("默认震动强度")]
    public float defaultShakeStrength = 0.25f;

    [Tooltip("震动频率")]
    public int shakeVibrato = 14;

    [Tooltip("震动随机度")]
    public float shakeRandomness = 90f;

    private ChromaticAberration chromaticAberration;
    private float targetFOV;
    private float targetChromaticIntensity;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRotation;
    private bool isCameraCached = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureCameraReference();
        EnsureVolumeSetup();
    }

    private void Start()
    {
        EnsureCameraReference();
        EnsureVolumeSetup();
    }

    public void EnsureCameraReference()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        if (targetCamera == null)
        {
            targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null && !isCameraCached)
        {
            baseFOV = targetCamera.fieldOfView;
            if (baseFOV <= 0f || baseFOV > 120f) baseFOV = 60f;
            originalLocalPos = targetCamera.transform.localPosition;
            originalLocalRotation = targetCamera.transform.localRotation;
            isCameraCached = true;
        }
    }

    public void EnsureVolumeSetup()
    {
        if (targetVolume == null)
        {
            targetVolume = FindObjectOfType<Volume>();
        }

        if (targetVolume == null)
        {
            GameObject volGo = new GameObject("RuntimePostProcessingVolume");
            volGo.transform.SetParent(transform);
            targetVolume = volGo.AddComponent<Volume>();
            targetVolume.isGlobal = true;
            targetVolume.priority = 10;
        }

        if (targetVolume != null)
        {
            if (targetVolume.profile == null)
            {
                targetVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            if (!targetVolume.profile.TryGet<ChromaticAberration>(out chromaticAberration))
            {
                chromaticAberration = targetVolume.profile.Add<ChromaticAberration>(true);
            }

            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.overrideState = true;
            }
        }
    }

    /// <summary>
    /// 根据玩家当前速度与最大速度，动态更新 FOV 拉伸与色像差强度
    /// </summary>
    public void UpdateSpeedEffects(float currentSpeed, float maxSpeed)
    {
        EnsureCameraReference();
        EnsureVolumeSetup();

        if (targetCamera == null) return;

        float ratio = 0f;
        if (maxSpeed > 0f)
        {
            ratio = Mathf.Clamp01(currentSpeed / maxSpeed);
        }
        else if (currentSpeed > 0f)
        {
            ratio = Mathf.Clamp01(currentSpeed / 15f);
        }

        // 计算目标 FOV 与色像差强度
        targetFOV = baseFOV + (ratio * maxFOVOffset);
        targetChromaticIntensity = ratio * maxChromaticIntensity;

        // 平滑拉伸 FOV
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);

        // 平滑过渡色像差
        if (chromaticAberration != null)
        {
            float currentIntensity = chromaticAberration.intensity.value;
            chromaticAberration.intensity.value = Mathf.Lerp(currentIntensity, targetChromaticIntensity, Time.deltaTime * chromaticLerpSpeed);
        }
    }

    /// <summary>
    /// 触发碰撞障碍物时的轻微相机震动与偏移
    /// </summary>
    public void TriggerObstacleShake(float duration = -1f, float strength = -1f)
    {
        EnsureCameraReference();
        if (targetCamera == null) return;

        float dur = duration > 0f ? duration : defaultShakeDuration;
        float str = strength > 0f ? strength : defaultShakeStrength;

        // 强行停止该 Camera 上的旧震动动画，防止多次连续震动累加导致的位移偏差
        targetCamera.DOKill();

        // 无论上一次震动是否完毕，开始新一轮震动前均强行复位至基准状态，确保绝对不发生位置/旋转偏移累加
        if (isCameraCached)
        {
            targetCamera.transform.localPosition = originalLocalPos;
            targetCamera.transform.localRotation = originalLocalRotation;
        }

        // 震动本地坐标与旋转
        targetCamera.DOShakePosition(dur, str, shakeVibrato, shakeRandomness, false, ShakeRandomnessMode.Full)
            .OnComplete(() => {
                if (targetCamera != null && isCameraCached)
                {
                    targetCamera.transform.localPosition = originalLocalPos;
                }
            });

        targetCamera.DOShakeRotation(dur, str * 15f, shakeVibrato, shakeRandomness, false, ShakeRandomnessMode.Full)
            .OnComplete(() => {
                if (targetCamera != null && isCameraCached)
                {
                    targetCamera.transform.localRotation = originalLocalRotation;
                }
            });
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            EnsureCameraReference();
        }
    }
}
