using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LazyRotate : MonoBehaviour
{


    public float rotateY;
    bool rotateReady = false;
    // Start is called before the first frame update
    void Start()
    {
        // Roll a random delay
        float delay = Random.Range(0,2);

        // transform.DORotate(_rotation,2, RotateMode.FastBeyond360);

        Invoke("SetRotate", delay);
    }

    void SetRotate()
    {
        rotateReady = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (rotateReady)
        {
            transform.Rotate(Vector3.up * (rotateY* Time.deltaTime));
        }
    }
}
