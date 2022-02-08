using Mono.Options;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;

public class TrainingArgs
{
    public int seed = 0;
    public string map_folder = "../Maps/Training/";
}

public class TrainingCLI
{
    private OptionSet options;

    public TrainingCLI()
    {
        options = new OptionSet();
    }

    public TrainingArgs Parse()
    {
        List<string> argsStr = new List<string>(System.Environment.GetCommandLineArgs());
        int indexGenerationArgs = argsStr.FindIndex(0, (string arg) => arg == "--");
        if (indexGenerationArgs == -1)
            return new TrainingArgs();
        argsStr.RemoveRange(0, indexGenerationArgs + 1);
        TrainingArgs args;
        if ((args = Parse(argsStr)) == null)
            Application.Quit();
        return args;
    }

    public TrainingArgs Parse(List<string> argsList)
    {
        TrainingArgs args = new TrainingArgs();
        bool parsedWithoutError = true;
        options.Add("seed=", "Map order is randomized based on the seed.", (int seedArg) => args.seed = seedArg);

        options.Add("map-folder=", "Path to the folder containing the maps for the training.", (string path) => args.map_folder = path);

        options.Add("h|help|?", "Show this message.", v =>
        {
            if (argsList.Count <= 1)
            {
                System.IO.TextWriter writer = new System.IO.StringWriter();
                options.WriteOptionDescriptions(writer);
                Debug.Log(writer.ToString());
                parsedWithoutError = false;
            }
        });

        options.Parse(argsList);

        if (!parsedWithoutError)
            return null;
        return args;
    }
}
