using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Init : MonoBehaviour
{
    void Awake()
    {
        Globals.playerController = GameObject.Find("!! Player").GetComponent<PlayerController>();
        Globals.gameManager = GameObject.Find("!! Scripts").GetComponent<GameManager>();

        // Debug.Log(Globals.gameManager.gameObject.name.ToString());
        // Debug.Log("-----");
        // Debug.Log(Globals.playerController.gameObject.name.ToString());
    }

}
