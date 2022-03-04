using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

public class TrainingInitializer : MonoBehaviour
{
    [Header("Game object handles")]
    [SerializeField]
    public GameObject agentPrefab;
    public int nbAgents = 1;
    
    private MapCycler MapCycler = null;

    void Awake()
    {
        TrainingCLI parser = new TrainingCLI();
        var args = parser.Parse();
        nbAgents = args.nbAgents;
        MapCycler = new MapCycler(args.map_folder, nbAgents, seed:args.seed);
        CircularCamera circularCamera = GetComponentInChildren<CircularCamera>();

        for (int agentIndex = 0; agentIndex < nbAgents; agentIndex++)
        {
            SmartNav.OnEpisodeBeginCallback = MapCycler.OnEpisodeBegin;
            GameObject currentAgent = Instantiate(agentPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            SmartNav currentAgentScript = currentAgent.GetComponent<SmartNav>();
            currentAgentScript.agentId = agentIndex;

            if (agentIndex == 0)
            {
                circularCamera.targets.Add(currentAgent);
                circularCamera.targets.Add(currentAgentScript.goal);
            }
        }

        // We deactivate stuff in batch mode to save cost.
        if (System.Environment.GetCommandLineArgs().Contains("-nographics"))
        {
            Debug.Log("Deactivating all cameras because in nographics mode");
            StopDebugging();
        }
    }

    void StopDebugging()
    {
        GetComponentInChildren<CamSwitch>().ShutDown();
    }

    void StartDebugging()
    {
        GetComponentInChildren<CamSwitch>().Start();
    }
}
