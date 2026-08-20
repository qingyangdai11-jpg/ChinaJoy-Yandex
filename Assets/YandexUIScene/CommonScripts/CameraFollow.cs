using UnityEngine;

/// <summary>
/// 使摄像机平滑跟随目标对象
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;

    [Header("跟随参数")]
    public float followSpeed = 5f;
    public Vector3 offset = new Vector3(0f, 4.56f, -4.22f);

    private Vector3 _desiredPosition;
    private SimplePlayerController _playerController;

    private void Start()
    {
        if (target == null)
        {
            FindPlayerTarget();
        }
        else
        {
            _playerController = target.GetComponent<SimplePlayerController>();
        }
    }

    private void FindPlayerTarget()
    {
        // Try to find active GameObject named "player" first
        var playerObj = GameObject.Find("player");
        if (playerObj != null && playerObj.activeInHierarchy)
        {
            target = playerObj.transform;
            return;
        }

        // Try to find active PlayerIdentity
        var identities = FindObjectsOfType<PlayerIdentity>();
        foreach (var identity in identities)
        {
            if (identity.gameObject.activeInHierarchy)
            {
                target = identity.transform;
                return;
            }
        }

        // Try to find active SimplePlayerController
        var controllers = FindObjectsOfType<SimplePlayerController>();
        foreach (var controller in controllers)
        {
            if (controller.gameObject.activeInHierarchy)
            {
                target = controller.transform;
                return;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            FindPlayerTarget();
            if (target == null || !target.gameObject.activeInHierarchy)
                return;
        }

        if (_playerController == null || _playerController.transform != target)
        {
            _playerController = target.GetComponent<SimplePlayerController>();
        }

        // 计算目标位置 = 角色位置 + 偏移量
        _desiredPosition = target.position + offset;

        // 平滑移动到目标位置
        float currentFollowSpeed = followSpeed;
        if (_playerController != null)
        {
            currentFollowSpeed *= _playerController.CurrentSpeedMultiplier;
        }
        transform.position = Vector3.Lerp(transform.position, _desiredPosition, currentFollowSpeed * Time.deltaTime);
    }
}
