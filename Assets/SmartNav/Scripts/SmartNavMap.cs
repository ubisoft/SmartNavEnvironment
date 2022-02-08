using System.Collections.Generic;
using UnityEngine;
using System.IO;
using LaForge.MapGenerator;

public class SmartNavMap
{
    // A data class representing a map.

    public GameObject Map = null;
    private string path;
    public string Path { get { return path; } }
    private List<Vector3[]> SpawnGoalList;
    public int NbrSpawnGoals {  get { return SpawnGoalList.Count; } }
    private int index;
    public int GetIndex() { return index; }
    private int SpawnGoalIndex = 0;
    private string PathToJson;

    private Vector3[] jumpPadPositions;
    
    public SmartNavMap(string path, int index)
    {
        this.index = index;
        PathToJson = path;
        // Load spawn goals in the beginning
        SpawnGoalList = MapSerializer.LoadSpawnGoals(path);
        this.path = path;
        if ((SpawnGoalList == null) || (SpawnGoalList.Count == 0))
        {
            throw new InvalidDataException("The spawnGoalList wasn't initialized correctly for map " + path);
        }
    }

    public bool IsFinished()
    {
        return (SpawnGoalIndex == SpawnGoalList.Count);
    }

    public void RestartMap()
    {
        SpawnGoalIndex = 0;
    }

    public Vector3[] GetNextSpawnGoal()
    {
        return SpawnGoalList[SpawnGoalIndex++];
    }

    public void LoadMap(Vector3 position)
    {
        if (!string.IsNullOrEmpty(PathToJson))
        {
            Map = MapSerializer.LoadMap(PathToJson);
            MeshFilter meshFilter = Map.GetComponentInChildren<TerrainLoader>().GetComponent<MeshFilter>();
            Map.transform.position = position;
        }
        Debug.Assert(Map != null);  // If path empty.
        // find the jumppad positions
        jumpPadPositions = FindJumpPadPositions();
    }

    private Vector3[] FindJumpPadPositions()
    {
        var jumpPads = Map.GetComponentsInChildren<JumpPad>();
        Vector3[] positions = new Vector3[jumpPads.Length];
        Debug.Assert(jumpPads.Length > 0);
        for (int i = 0; i < jumpPads.Length; i++)
        {
            positions[i] = jumpPads[i].transform.position - Map.transform.position;
        }
        return positions;
    }

    public Vector3 FindNearestJumpPad(Vector3 position)
    {
        Vector3 jumpPadPosition = new Vector3(0f, 0f, 0f);
        float closestDistance = float.MaxValue;
        foreach (var pos in jumpPadPositions)
        {
            float distance = Vector3.Distance(position, pos);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                jumpPadPosition = pos;
            }
        }
        return jumpPadPosition;
    }

    public void UnloadMap()
    {
        UnityEngine.Object.DestroyImmediate(Map);
        Map = null;
        Debug.Assert(Map == null);
        SpawnGoalIndex = 0;
    }



}
