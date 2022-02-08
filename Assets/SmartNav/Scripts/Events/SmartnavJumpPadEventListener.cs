using UnityEngine;
using LaForge.MapGenerator;

public class SmartnavJumpPadEventListener : JumpPadEventListener
{
    protected override void OnJumpPadEnter(JumpPad jumpPad, Collision collision)
    {
        collision.gameObject.GetComponent<ISmartNavMotion>().OnJumpPadEnter(jumpPad);
    }
}   
