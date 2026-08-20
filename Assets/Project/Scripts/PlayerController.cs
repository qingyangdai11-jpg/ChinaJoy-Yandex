using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathCreation.Examples;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class PlayerController : MonoBehaviour
{
    public Transform BGFollowPlayer;
    public Transform PlayerMesh;

    public float startingSpeed = 10;

    public float speed = 0;
    public float maxSpeed = 10;
    public float accelerationRate = 0.1f;
    public float decelerationRate = 0.1f;
    public float fov_multiplier = 2f;
    public Volume volume;
    private ChromaticAberration chromaticAberration;
    public Collider PlayerCollider;

    public PathFollower pathFollower;

    public PathFollower targetLookAtPathFollower;

    public GameManager gameManager;

    // changing lanes
    public float laneOffset = 2.5f;
    public LanePosition lanePosition = LanePosition.CENTER;
    // debug elements

    public Text speedText;

    public bool safe = false;

    public bool is_jumping = false;
    public bool on_hover_zone = false;
    public bool was_in_hover_zone_on_hit = false;
    public float last_hover_jump_height = 0f;
    public int last_hit_car_instance_id = 0;
    public bool has_avoided_obstacle = false;
    private bool prev_on_hover_zone = false;
    public bool is_switching_lanes = false;
    public bool is_stunned = false;
    public float jump_height = 0;

    public Ease jump_launch_ease = Ease.OutQuint;
    public Ease jump_land_ease = Ease.OutBounce;

    public float jump_launch_time = 0.5f;
    public float jump_land_time = 0.85f;


    public Animator animator;
    private int direction_hash;
    private int elevation_hash;

    void Start()
    {
        GetAnimatorParams();//通过 Animator.StringToHash 提前将动画参数名 "Elevation"（高度）和 "Direction"（左右方向偏转）缓存为整型 Hash，这在后续的 Update 中可以显著提升动画参数赋值性能。
        GetVolumeParams();//获取高画质渲染管线（HDRP）的 Volume，动态开启 ChromaticAberration（色像差/色彩抖动）滤镜以表达速度感。
        prev_on_hover_zone = on_hover_zone;
    }

    void GetAnimatorParams()
    {
        elevation_hash = Animator.StringToHash("Elevation");
        direction_hash = Animator.StringToHash("Direction");
    }

    void GetVolumeParams()
    {
        if (volume != null && volume.profile != null && volume.profile.TryGet<ChromaticAberration>(out var chromatic))
        {
            chromaticAberration = chromatic;
            chromaticAberration.intensity.overrideState = true;
        }
    }

    //水平变道机制 (LaneChangeCheck)
//    游戏拥有三个车道：LEFT(左)、CENTER(中)、RIGHT(右)，车道间距由 laneOffset 决定。
//平滑移动与物理回正：
//通过 DOTween 库的 DOLocalMoveX 实现角色的平滑侧移；
//在变道的同时，使用 DOTween 对 Animator 的 "Direction" 参数进行渐变插值（从 -1 到 1），配合模型表现出变道时左右倾斜滑行的物理动态。
//变道结束后，触发回调 reset_params_laneChange() 将偏转动画回正为 0。
    void LaneChangeCheck(string pos)
    {
        if(is_jumping)return;
        if (is_switching_lanes) return;
        if (PlayerMesh != null && PlayerMesh.localPosition.y > 0.05f) return;
        if (on_hover_zone)
        {
            has_avoided_obstacle = true;
        }
        if (lanePosition == LanePosition.CENTER)
        {
            if (pos == "LEFT")
            {
                lanePosition = LanePosition.LEFT;
                PlayerMesh.DOLocalMoveX(laneOffset * -1, 0.5f).SetEase(Ease.InOutSine).OnComplete(reset_params_laneChange);
                DOTween.To(() => animator.GetFloat(direction_hash), x => animator.SetFloat(direction_hash, x), -1, 0.5f).SetEase(Ease.OutQuint);

            }
            else if (pos == "RIGHT")
            {
                lanePosition = LanePosition.RIGHT;
                PlayerMesh.DOLocalMoveX(laneOffset, 0.5f).SetEase(Ease.InOutSine).OnComplete(reset_params_laneChange);
                DOTween.To(() => animator.GetFloat(direction_hash), x => animator.SetFloat(direction_hash, x), 1, 0.5f).SetEase(Ease.OutQuint);

            }
        }

        if (lanePosition == LanePosition.LEFT)
        {
            if (pos == "RIGHT")
            {
                lanePosition = LanePosition.CENTER;
                PlayerMesh.DOLocalMoveX(0, 0.5f).SetEase(Ease.InOutSine).OnComplete(reset_params_laneChange);
                DOTween.To(() => animator.GetFloat(direction_hash), x => animator.SetFloat(direction_hash, x), 1, 0.5f).SetEase(Ease.OutQuint);

            }
        }
        if (lanePosition == LanePosition.RIGHT)
        {
            if (pos == "LEFT")
            {
                lanePosition = LanePosition.CENTER;
                PlayerMesh.DOLocalMoveX(0, 0.5f).SetEase(Ease.InOutSine).OnComplete(reset_params_laneChange);
                DOTween.To(() => animator.GetFloat(direction_hash), x => animator.SetFloat(direction_hash, x), -1, 0.5f).SetEase(Ease.OutQuint);

            }
        }

    }

    void reset_params_laneChange()
    {
        is_switching_lanes = false;
        DOTween.To(() => animator.GetFloat(direction_hash), x => animator.SetFloat(direction_hash, x), 0, 0.5f).SetEase(Ease.OutSine);
    }

    //加速与画面拉伸表现 (Accelerate)
    // 在游戏就绪时驱动。使角色当前速度 speed 朝着设定的 maxSpeed 逐渐加速，并控制路程追踪组件 pathFollower.speed 的速度。
    //同步相机效果：
    //优先使用 CameraEffectsController 更新速度带来的相机特效；
//如果没有特效控制器，则会直接修改主相机的 Field of View(fieldOfView)（速度越快視野拉得越开），并调大后处理色像差的强度值（chromaticAberration.intensity.value），从而实现极强的冲击力和速度感。
    void Accelerate()
    {
        if (speedText != null)
        {
            speedText.text = "SPEED : " + speed.ToString();
        }

        if (speed < maxSpeed)
        {
            speed += accelerationRate * Time.deltaTime;
            if (pathFollower != null)
            {
                pathFollower.speed = speed;
            }
        }

        // 驱动基于速度的色像差与 FOV 动态拉伸表现
        if (CameraEffectsController.Instance != null)
        {
            CameraEffectsController.Instance.UpdateSpeedEffects(speed, maxSpeed);
        }
        else
        {
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = 65f + (speed * fov_multiplier);
            }

            if (chromaticAberration != null && maxSpeed > 0f)
            {
                float perc_speed = speed / maxSpeed;
                chromaticAberration.intensity.value = perc_speed * 3f;
            }
        }

        UpdatePathFollowerTargetObject();
    }

    //普通跳跃：使用一个 DOTween.Sequence 队列，首先将角色在 jump_launch_time 内拉升至空中（以 jump_launch_ease 缓动起跳），随后在 jump_land_time 内降回地面（以 jump_land_ease 带来弹动感降落）。
    //悬浮区（车顶）跳跃：如果玩家位于 on_hover_zone（即车顶/悬浮物上方），它只起跳上升到 jump_height（车顶高度）并在空中保持悬浮滑动。若要降回地面，则调用 JumpDown() 使用 jump_land_ease 将位置拉回 0。
//配合跳跃，将动画控制器里的高度参数 Elevation 设置为 1。
    public void Jump(bool force = false)
    {
        if (is_switching_lanes) return;
        if (has_avoided_obstacle) return;
        // 如果回弹前是在悬浮区受击，强制恢复悬浮状态与高度以允许跳上车顶滑行
        if (was_in_hover_zone_on_hit)
        {
            on_hover_zone = true;
            jump_height = last_hover_jump_height;
            was_in_hover_zone_on_hit = false;
        }

        Debug.Log($"[Jump Debug] on_hover_zone: {on_hover_zone}, jump_height: {jump_height}, lanePosition: {lanePosition}");
        if (!is_jumping)
        {
            // 终止减速和击退拉扯，恢复向前冲力
            var collisionHandler = GetComponentInChildren<PlayerCollisionHandler>();
            if (collisionHandler == null)
            {
                collisionHandler = GetComponentInParent<PlayerCollisionHandler>();
            }
            if (collisionHandler != null)
            {
                collisionHandler.CancelSlowdown();
            }

            if (speed < startingSpeed * 0.5f)
            {
                speed = startingSpeed * 0.6f;
                if (pathFollower != null)
                {
                    pathFollower.speed = speed;
                }
            }

            is_jumping = true;
            if (on_hover_zone)
            {
                DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 1, 0.5f).SetEase(Ease.OutQuint);
                PlayerMesh.DOLocalMoveY(jump_height, 0.5f).SetEase(jump_launch_ease).OnComplete(() =>
                {
                    OnJumpComplete();
                    DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 0, 0.5f).SetEase(Ease.OutQuint);
                });
                // PlayerMesh.DOLocalMoveY(0, 0.1f).SetEase(jump_land_ease);

            }
            else
            {

                float targetJumpHeight = jump_height > 0.05f ? jump_height : 2f;
                DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 1, 0.5f).SetEase(Ease.OutQuint);
                DOTween.Sequence().Append(PlayerMesh.DOLocalMoveY(targetJumpHeight, jump_launch_time).SetEase(jump_launch_ease))
                                .AppendCallback(() =>
                                {
                                    DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 0, 1f).SetEase(Ease.OutQuint);
                                })
                                .Append(PlayerMesh.DOLocalMoveY(0, jump_land_time).SetEase(jump_land_ease))
                                .AppendCallback(OnJumpComplete);
            }
        }
    }
    void OnJumpComplete()
    {
        is_jumping = false;
        is_switching_lanes = false;
    }

    //受击硬直与碰撞反馈 (StunPlayer & OnCollideWithObstacle)
    //当玩家碰到车辆等障碍物时调用此方法。
//状态归位与反弹：
//强制将所有的侧移、跳跃、避障状态清除，并将 PlayerMesh 瞬间重置回地面。
//将玩家当前的向前移动速度变为负值（speed = deflectAmount* -1），形成向后的反弹碰撞力。
//修改动画的 Elevation 为 -1，使角色播放类似“下蹲受击”的被击倒/硬直动画。
    public void StunPlayer(float deflectAmount)
    {
        // 如果受击时处于悬浮区，记录下来，以便回弹起跳时强制使用悬浮跳上车顶
        if (on_hover_zone)
        {
            was_in_hover_zone_on_hit = true;
            last_hover_jump_height = jump_height;
        }
        else if (!was_in_hover_zone_on_hit)
        {
            // 仅当未被碰撞处理器提前写入悬浮记忆时才清空，
            // 确保不管第几次撞击 CAR/TRAM，悬浮记忆都能保持
            was_in_hover_zone_on_hit = false;
            last_hover_jump_height = 0f;
        }

        // 重置跳跃/变道/避让状态与物理高度，允许在回弹时再次起跳
        is_jumping = false;
        is_switching_lanes = false;
        has_avoided_obstacle = false;
        if (PlayerMesh != null)
        {
            PlayerMesh.DOKill();
            PlayerMesh.localPosition = new Vector3(PlayerMesh.localPosition.x, 0f, PlayerMesh.localPosition.z);
        }

        // play skinned mesh animation transition to "STUN" state
        // bring the speed back to 0
        speed = deflectAmount * -1;
        DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), -1, 0.5f).SetEase(Ease.OutQuint).OnComplete(OnStunComplete);

        // 触发相机轻微震动与偏移
        if (CameraEffectsController.Instance != null)
        {
            CameraEffectsController.Instance.TriggerObstacleShake(0.35f, 0.25f);
        }
        else if (Camera.main != null)
        {
            Camera.main.DOShakePosition(0.5f, 0.25f, 10, 90, false, ShakeRandomnessMode.Full);
            Camera.main.DOShakeRotation(0.5f, 0.25f, 10, 90, false, ShakeRandomnessMode.Full);
        }
    }

    void OnStunComplete()
    {
        DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 0, 0.5f).SetEase(Ease.OutQuint);
    }

    public void JumpDown()
    {
        PlayerMesh.DOLocalMoveY(0, 0.85f).SetEase(jump_land_ease).OnComplete(OnJumpComplete);
        DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 1, 0.5f).SetEase(Ease.OutQuint).OnComplete(() =>
        {
            {
                DOTween.To(() => animator.GetFloat(elevation_hash), x => animator.SetFloat(elevation_hash, x), 0, 1f).SetEase(Ease.OutQuint);
            }
        });
    }



    public void OnCollideWithObstacle(float deflectAmount)
    {
        // bring the speed back to 0
        speed = 0;

        // 碰撞障碍物触发相机震动
        if (CameraEffectsController.Instance != null)
        {
            CameraEffectsController.Instance.TriggerObstacleShake(0.3f, 0.2f);
        }
    }

    void UpdatePathFollowerTargetObject()
    {
        targetLookAtPathFollower.distanceTravelled = pathFollower.distanceTravelled + 5;
    }

    public void ObstacleCollisionHandler()
    {
        // bring the speed back to 0
        Debug.Log("Collided with obstacle!");

    }

    public void LightningPickupHandler() { }

    [Header("Control Settings")]
    [Tooltip("是否允许玩家进行触控/按键移动与加速")]
    public bool isControlEnabled = false;

    public void EnablePlayerControl()
    {
        isControlEnabled = true;
    }

    public void DisablePlayerControl()
    {
        isControlEnabled = false;
        speed = 0f;
    }

    void Update()
    {
        if (prev_on_hover_zone && !on_hover_zone)
        {
            has_avoided_obstacle = false;
        }
        prev_on_hover_zone = on_hover_zone;

        if (!isControlEnabled) return;

        if (gameManager != null && gameManager.GameReady)
        {
            Accelerate();
            if (speedText != null)
            {
                speedText.text = "SPEED : " + speed.ToString();
            }
        }

        if (BGFollowPlayer != null)
        {
            BGFollowPlayer.position = transform.position;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {

            LaneChangeCheck("LEFT");
            is_switching_lanes = true;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            LaneChangeCheck("RIGHT");
            is_switching_lanes = true;
        }

        if (Input.GetKeyDown(KeyCode.Space) && is_jumping == false)
        {
            Debug.Log("JUMP");
            Jump();
        }

    }
}


public enum LanePosition
{
    LEFT,
    CENTER,
    RIGHT
}