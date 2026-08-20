using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PickupGetHandler : MonoBehaviour
{

    public GameObject EffectGO;
    public GameObject MainAssetGO;

    public GameManager gameManager;


    void Start()
    {   

        gameManager = Globals.gameManager;
        
        EffectGO.SetActive(false);
        MainAssetGO.SetActive(true);


    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            switch (gameObject.tag)
            {
                case "PICKUP_COIN":
                    gameManager.CoinPickupHandler();
                    break;
                case "PICKUP_BOOST":
                    gameManager.BoostPickupHandler();
                    break;
                case "PICKUP_LIGHTNING":
                    gameManager.LightningPickupHandler();
                    break;

            }
            OnEnterHandler();
            Invoke("DestroySelf", 2f);
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

    void DestroySelf()
    {
        Destroy(gameObject);
    }


    public void OnEnterHandler()
    {
        EffectGO.SetActive(true);
        MainAssetGO.SetActive(false);
    }

    void Update()
    {

    }


}


