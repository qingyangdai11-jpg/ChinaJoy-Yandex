using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;


public class Hover : MonoBehaviour
{
    public float hoverHeight = 0.3f;
    // Start is called before the first frame update
    void Start()
    {
        transform.DOLocalMoveY(hoverHeight, 1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    

    // Update is called once per frame
    void Update()
    {
        
    }
}
