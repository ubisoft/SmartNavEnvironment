using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public class MapSlot
{
    public SmartNavMap SmartNavMap = null;
    public Vector3 Position;
    public int AgentCounter = 0;

    public MapSlot(SmartNavMap smartNavMap, Vector3 position)
    {
        SmartNavMap = smartNavMap;
        Position = position;
    }

    public void AddAgent(SmartNav agent)
    {
        if (AgentCounter == 0)
        {
            SmartNavMap.LoadMap(Position);
            SmartNavMap.Map.transform.position = Position;
        }

        // Change parent of agent to be the map, and change map position to be the slot position
        agent.currentMapSlot = this;
        agent.transform.parent = SmartNavMap.Map.transform;
        agent.goal.transform.parent = SmartNavMap.Map.transform;

        AgentCounter += 1;
    }

    public void RemoveAgent(SmartNav agent)
    {
        AgentCounter -= 1;

        agent.transform.parent = null;
        agent.goal.transform.parent = null;
        agent.currentMapSlot = null;

        if (AgentCounter == 0)
        {
            SmartNavMap.UnloadMap();
            SmartNavMap = null;
        }
    }
    public bool IsEmpty()
    {
        return (AgentCounter == 0);
    }
}
