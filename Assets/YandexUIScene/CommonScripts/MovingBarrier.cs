using UnityEngine;
using DG.Tweening;

/// <summary>
/// 控制道具往人物行进方向的反方向自动行走，并暴露行走参数于 Inspector。
/// </summary>
public class MovingBarrier : MonoBehaviour
{
    [Header("移动设置 (Movement Settings)")]
    [Tooltip("自动移动的方向。默认 (0, 0, -1) 即与人物前进方向相反。")]
    public Vector3 movementDirection = Vector3.back;

    [Tooltip("移动速度（单位/秒）。")]
    public float movementSpeed = 3f;

    [Tooltip("是否动态跟踪并采用人物前进方向的相反方向（勾选后将自动覆盖静态方向配置）。")]
    public bool useDynamicPlayerDirection = true;

    [Header("状态控制 (Status)")]
    [Tooltip("当前道具是否在移动。")]
    public bool isMoving = true;

    private Transform _playerTransform;

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        // 尝试通过名字寻找 player 对象
        var playerObj = GameObject.Find("player");
        if (playerObj != null && playerObj.activeInHierarchy)
        {
            _playerTransform = playerObj.transform;
            return;
        }

        // 尝试通过 PlayerIdentity 组件寻找
        var identity = FindObjectOfType<PlayerIdentity>();
        if (identity != null && identity.gameObject.activeInHierarchy)
        {
            _playerTransform = identity.transform;
            return;
        }

        // 尝试通过 SimplePlayerController 组件寻找
        var controller = FindObjectOfType<SimplePlayerController>();
        if (controller != null && controller.gameObject.activeInHierarchy)
        {
            _playerTransform = controller.transform;
        }
    }

    private void Update()
    {
        // 如果游戏暂停，或者不在 Gameplay 状态，则不进行移动
        if (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused)
            return;

        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay)
            return;

        if (!isMoving)
            return;

        if (_playerTransform == null || !_playerTransform.gameObject.activeInHierarchy)
        {
            FindPlayer();
        }

        // 计算最终行走方向
        Vector3 dir = movementDirection.normalized;
        if (useDynamicPlayerDirection && _playerTransform != null)
        {
            // 取人物当前朝向的反方向
            dir = -_playerTransform.forward;
            // 投影至水平面，避免高度方向移动
            dir.y = 0f;
            dir = dir.normalized;
        }

        // 平移移动物体
        transform.position += dir * movementSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndDestroyItem(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckAndDestroyItem(collision.gameObject);
    }

    private void CheckAndDestroyItem(GameObject otherObj)
    {
        CollectibleItem item = otherObj.GetComponent<CollectibleItem>();
        if (item == null)
        {
            item = otherObj.GetComponentInParent<CollectibleItem>();
        }

        // 避免销毁自身
        if (item != null && item.gameObject != this.gameObject)
        {
            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.OnEnemyDestroyed(item.gameObject);
            }
            item.transform.DOKill();
            Destroy(item.gameObject);
            Debug.Log($"[MovingBarrier] 碰到了其他道具 {item.gameObject.name}，令其瞬间消失！");
        }
    }
}
