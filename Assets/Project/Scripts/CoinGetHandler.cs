using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CoinGetHandler : MonoBehaviour
{

    public GameObject EffectGO;
    public GameObject MainAssetGO;


    void Start()
    {
        EffectGO.SetActive(false);
        MainAssetGO.SetActive(true);
    }

    // when collided with, call OnEnterHandler
    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        OnEnterHandler();
        Invoke("DestroySelf", 2f);
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
