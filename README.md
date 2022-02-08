# SmartnavEnvironment (c) Ubisoft 2022

This environment is an example of how to use the maps from the [SmartnavMapGenerator](https://github.com/ubisoft/SmartNavMapGenerator). You can provide it multiple maps and it will cycle through each maps during the training.

## License

Please read the [license](./LICENSE.txt).
Here's a [summary](https://creativecommons.org/licenses/by-nc-nd/4.0/).

## How to use

The environment is meant to be used with the [ml-agent library](https://unity.com/products/machine-learning-agents) as any other ml-agent environment. Simply open the SmartnavEnvironment scene and build it (ctrl+b in unity editor) to get an executable. 

To cover the python side of a training, we suggest using the rllib's [unity3d_env](https://docs.ray.io/en/releases-1.0.0/_modules/ray/rllib/env/unity3d_env.html) which has the additional bonus of making it possible to do multi-agent trainings.

By default, maps should be located beside the data folder `smartnavenvironment_Data` at the following path `./Maps/Training`. You can also change the path by specifying it through command line.
Example:
```sh
./smartnavenvironment -batchmode -nographics -logfile - -- --map-folder /path/to/map/folder
```

If using the UnityEnvironment class, you can specify command line args in the [additional_args](https://github.com/Unity-Technologies/ml-agents/blob/main/ml-agents-envs/mlagents_envs/environment.py#L153) arguments

## Reference
```
@article{DBLP:journals/corr/abs-2112-11731,
  author    = {Edward Beeching and
               Maxim Peter and
               Philippe Marcotte and
               Jilles Dibangoye and
               Olivier Simonin and
               Joshua Romoff and
               Christian Wolf},
  title     = {Graph augmented Deep Reinforcement Learning in the GameRLand3D environment},
  journal   = {CoRR},
  volume    = {abs/2112.11731},
  year      = {2021},
  url       = {https://arxiv.org/abs/2112.11731},
  eprinttype = {arXiv},
  eprint    = {2112.11731},
  timestamp = {Tue, 04 Jan 2022 15:59:27 +0100},
  biburl    = {https://dblp.org/rec/journals/corr/abs-2112-11731.bib},
  bibsource = {dblp computer science bibliography, https://dblp.org}
}
```
