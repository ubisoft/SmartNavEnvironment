using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using LaForge.MapGenerator;

static class RandomExtensions
{
    public static void Shuffle<T>(this System.Random rng, T[] array)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = rng.Next(n--);
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }
}

public class MapCycler
{
    private SmartNavMap[] SmartNavMaps;
    private List<MapSlot> MapSlots = new List<MapSlot>();
    private string PathToMapsFolder;

    private int SmartNavMapIndex = 0;
    private float MapScale;
    private float MapSizeX;

    private int nbMaps;

    public MapCycler(string map_folder, int nbAgents, bool shuffle_maps = true, int seed = 0)
    {
        // Read the MapWrappers from the files on the disk.
        // TODO: Read the path from cmdline, tbd with Philippe
        PathToMapsFolder = Path.Combine(Path.Combine(Application.dataPath, ".."), map_folder);
        Debug.Log("MapCycler is using map directory: " + PathToMapsFolder);
        string[] mapNames = Directory.GetDirectories(PathToMapsFolder);

        if (shuffle_maps)
        {
            System.Random random = new System.Random(seed);
            random.Shuffle(mapNames);
        }

        nbMaps = mapNames.Length;
        Debug.Log($"Number of maps: {nbMaps}");
        SmartNavMaps = new SmartNavMap[nbMaps];
        for (int i = 0; i < nbMaps; i++)
        {
            SmartNavMap map = new SmartNavMap(mapNames[i], i);
            if (nbAgents > map.NbrSpawnGoals)
            {
                throw new ArgumentException($"Map {map.Path} has {map.NbrSpawnGoals} spawn-goals but there are {nbAgents} agents. Either reduce the number of agents or regenerate more spawn-goals");
            }
            SmartNavMaps[i] = map;
        }

        if (SmartNavMaps.Length > 0)
        {
            SmartNavMaps[0].LoadMap(Vector3.zero);
            MeshFilter meshFilter = SmartNavMaps[0].Map.GetComponentInChildren<TerrainLoader>().GetComponent<MeshFilter>();
            MapScale = meshFilter.transform.localScale.x;
            MapSizeX = Mathf.Ceil(meshFilter.sharedMesh.bounds.center.x + meshFilter.sharedMesh.bounds.extents.x);
            SmartNavMaps[0].UnloadMap();
        }
    }

    public void OnEpisodeBegin(SmartNav agent)
    {
        // If the agent already has a slot and it is finished, remove the agent from the slot
        if ((agent.currentMapSlot != null) && (agent.currentMapSlot.SmartNavMap != null) && (agent.currentMapSlot.SmartNavMap.IsFinished()))
        {
            agent.currentMapSlot.RemoveAgent(agent);
        }

        // If no slot, assign new one
        if (agent.currentMapSlot == null)
        {
            MapSlot currentMapSlot = GetNextSlot();
            currentMapSlot.AddAgent(agent);
        }
    }

    public MapSlot GetNextSlot()
    {
        MapSlot firstEmptySlot = null;
        foreach (var currentSlot in MapSlots)
        {
            if (currentSlot.SmartNavMap == null)
            {
                firstEmptySlot ??= currentSlot;
            }
            else
            {
                if (!currentSlot.SmartNavMap.IsFinished())
                {
                    return currentSlot;
                }
                else if (nbMaps == 1)
                {
                    // The only map is finished => reset it and keep going
                    Debug.Log($"Restarting map {currentSlot.SmartNavMap.Map.name} in slot {currentSlot.Position}");
                    currentSlot.SmartNavMap.RestartMap();
                    return currentSlot;
                }
            }
        }

        // No room in any of the existing slot
        // If found empty slot, assign slot to a new map, else create new slot and assign new map
        if (firstEmptySlot == null)
        {
            MapSlot newSlot = new MapSlot(SmartNavMaps[SmartNavMapIndex], new Vector3(MapSlots.Count * (MapSizeX + 5.0f) * MapScale, 0.0f, 0.0f));
            Debug.Log($"Creating map slot with map {SmartNavMapIndex} at {newSlot.Position}.");
            IterateIndex();
            MapSlots.Add(newSlot);
            return newSlot;
        }
        else
        {
            firstEmptySlot.SmartNavMap = SmartNavMaps[SmartNavMapIndex];
            Debug.Log($"Loading map {SmartNavMapIndex} at {firstEmptySlot.Position}.");
            IterateIndex();
            return firstEmptySlot;
        }
    }

    private void IterateIndex()
    {
        SmartNavMapIndex = (SmartNavMapIndex + 1) % SmartNavMaps.Length;
    }
}
