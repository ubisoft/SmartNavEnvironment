using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LaForge.MapGenerator;

[RequireComponent(typeof(SmartNav))]
public class GroundInteractable : MonoBehaviour
{
    private SmartNav smartNav;
    private bool inWater;
    [SerializeField]
    [Range(0, 20)]
    private float waterTime = 5;
    private float waterTimer;

    public float WaterTime { get => waterTime; set => waterTime = value; }

    private void OnEnable()
    {
        smartNav = GetComponent<SmartNav>();
        inWater = false;
        waterTimer = waterTime;
    }

    public void GroundEnter(Ground ground)
    {
        if (ground.GroundProperties.Type == GroundProperties.GroundType.Lava)
        {
            smartNav.Kill();
        }
        else if (ground.GroundProperties.Type == GroundProperties.GroundType.Water)
        {
            inWater = true;
        }
    }

    public void GroundExit(Ground ground)
    {
        if (ground.GroundProperties.Type == GroundProperties.GroundType.Water)
        {
            inWater = false;
            waterTimer = waterTime;
        }
    }

    private void Update()
    {
        if (inWater)
        {
            waterTimer -= Time.deltaTime;
            if (waterTimer < 0)
                smartNav.Kill();
        }
    }
}
