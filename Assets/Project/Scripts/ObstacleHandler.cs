using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleHandler : MonoBehaviour
{
    // Start is called before the first frame update



    public ObstacleType obstacleType;
    
    float deflect_amount = 1;


    PlayerController playerController;
    GameManager gameManager;

    float jump_height = 0;

    bool safe = false;


    void Start()
    {
        playerController = Globals.playerController;
        gameManager = Globals.gameManager;

        
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null) pc = other.GetComponent<PlayerController>();

            if (pc != null && pc.PlayerMesh != null)
            {
                // 如果是在地面上离开触发器（例如变道避让或正常跑过去），重置悬浮状态
                if (pc.PlayerMesh.localPosition.y < 0.5f)
                {
                    pc.on_hover_zone = false;
                    pc.jump_height = 0f;
                }
            }

            if (safe && obstacleType == ObstacleType.OBSTACLE_EXIT_HOVER)
            {
                safe = false;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            safe = true;
            Debug.Log(obstacleType.ToString());
            switch (obstacleType)
            {
                case ObstacleType.OBSTACLE_CAR:
                    playerController.on_hover_zone = true;
                    playerController.jump_height = 1.8f;
                    break;
                case ObstacleType.OBSTACLE_TRAM:
                    playerController.on_hover_zone = true;
                    playerController.jump_height = 2.5f;
                    break;
                case ObstacleType.OBSTACLE_CUBE:
                    if (playerController != null && !playerController.on_hover_zone)
                    {
                        playerController.jump_height = 2f;
                    }
                    break;
                case ObstacleType.OBSTACLE_FENCE:
                    if (playerController != null && !playerController.on_hover_zone)
                    {
                        playerController.jump_height = 2.5f;
                    }
                    break;
                case ObstacleType.OBSTACLE_EXIT_HOVER:
                    // force to jump down
                    if (playerController.on_hover_zone)
                    {
                        playerController.on_hover_zone = false;
                        playerController.jump_height = 0;
                        playerController.JumpDown();
                    }
                    
                    break;
                case ObstacleType.OBSTACLE_EXIT_HOVER2:
                    // force to jump down
              playerController.on_hover_zone = false;
                        playerController.jump_height = 0;
                        playerController.JumpDown();

                    break;
                case ObstacleType.OBSTACLE_STUN_ZONE:
                    playerController.StunPlayer(4);
                    break;
            }

        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.transform.root != null && other.transform.root.CompareTag("Player")) return true;
        if (other.GetComponentInParent<PlayerController>() != null) return true;
        string nameLower = other.name.ToLower();
        if (nameLower.Contains("player")) return true;
        if (other.transform.parent != null && other.transform.parent.name.ToLower().Contains("player")) return true;
        return false;
    }


    void Update()
    {

    }

}


public enum ObstacleType
{
    OBSTACLE_CAR,
    OBSTACLE_TRAM,
    OBSTACLE_CUBE,
    OBSTACLE_FENCE,
    OBSTACLE_EXIT_HOVER,

    OBSTACLE_STUN_ZONE,
    OBSTACLE_EXIT_HOVER2
}