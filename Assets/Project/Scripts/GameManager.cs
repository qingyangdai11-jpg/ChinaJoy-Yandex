using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{


    public bool GameReady = false;
    public GameObject PlayerGO;
    public int Score = 0;

    // -- debug ui

    public Text Debug_scoreText;


    // Update is called once per frame
    void Update()
    {
        // reload the game if R is pressed
        if(Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    public void GameStart()
    {
        GameReady = true;
        PlayerGO.GetComponent<PlayerController>().enabled = true;
    }


    public void CoinPickupHandler()
    {
        Score += 1;
        UpdateScoreUI();
    }

    public void LightningPickupHandler()
    {
        // do logic here...
        Score += 2;
        UpdateScoreUI();
    }

    public void BoostPickupHandler()
    {
        // do logic here...
        Score += 3;
        UpdateScoreUI();
    }

    public void UpdateScoreUI()
    {
        Debug_scoreText.text = "SCORE : " + Score.ToString();
        // update the score UI here...
        
    }



}
