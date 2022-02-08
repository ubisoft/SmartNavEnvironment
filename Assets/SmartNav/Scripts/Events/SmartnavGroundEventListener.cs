using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LaForge.MapGenerator;

public class SmartnavGroundEventListener : GroundEventListener
{
    protected override void OnGroundEnter(Ground ground, Collision collision)
    {
        collision.gameObject.GetComponent<GroundInteractable>().GroundEnter(ground);
    }

    protected override void OnGroundExit(Ground ground, Collision collision)
    {
        collision.gameObject.GetComponent<GroundInteractable>().GroundExit(ground);
    }

    protected override void OnGroundStay(Ground ground, Collision collision)
    {
        
    }
}
