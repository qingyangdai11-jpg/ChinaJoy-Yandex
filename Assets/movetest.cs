using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class movetest : MonoBehaviour
{
    // Start is called before the first frame update

    bool jump_locked = false;
    bool on_hover_zone = false;

    void Start()
    {

    }

    public void Jump(bool force = false)
    {
        if (!jump_locked)
        {
            if (on_hover_zone)
            {
                transform.DOLocalMoveY(1.5f, 1f).SetEase(Ease.OutBounce).OnComplete(OnJumpOnComplete);
            }
            else
            {
                // normal jump, jump to 1 unit to 0 with yoyo
                transform.DOLocalMoveY(1.5f, 1f).SetEase(Ease.OutBounce).OnComplete(JumpDown);
            }
        }
    }
    void OnJumpOnComplete()
    {
        jump_locked = false;
    }
    public void JumpDown()
    {
        transform.DOLocalMoveY(0, 1f).SetEase(Ease.InBounce).OnComplete(OnJumpOnComplete);
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += new Vector3(0, 0, 2 * Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.Space) && jump_locked == false)
        {
            // jump or hover
            Jump();
        }
    }
}
