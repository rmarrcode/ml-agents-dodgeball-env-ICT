
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/60f3e8fe-e72a-4e4c-8e03-b8e36d229843
# ML-Agents DodgeBall Extended Battle Scenario 

## Overview

The [ML-Agents](https://github.com/Unity-Technologies/ml-agents) DodgeBall environment is a third-person cooperative shooter where players try to pick up as many balls as they can, then throw them at their opponents. It comprises two game modes: Elimination and Capture the Flag. In Elimination, each group tries to eliminate all members of the other group by hitting them with balls. In Capture the Flag, players try to steal the other team’s flag and bring it back to their base. In both modes, players can hold up to four balls, and dash to dodge incoming balls and go through hedges. You can find more information about the environment at the corresponding [blog post](https://blog.unity.com/technology/ml-agents-plays-dodgeball).

In this project, we used the Elimination game-mode to explore modifying the DodgeBall environment to serve as a proxy for high-fidelity military simulations. We modified both the dodgeball agents' functionality and the arenas they were tested on in order to better approximate a real battle scenario. This document will detail the most significant changes we made and discuss the method we developed for reducing the number of training steps needed to learn an intelligent cooperative policy. 

## Installation and Play

To open this repository, you will need to install the [Unity editor version 2020.2.6](https://unity3d.com/get-unity/download).

Clone this repository by running:
```
git clone https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT
```

Open the root folder in Unity. Then, navigate to `Assets/Dodgeball/Scenes/TitleScreen.unity`, open it, and hit the play button to play against pretrained agents. You can also build this scene (along with the `Elimination.unity` and `CaptureTheFlag.unity` scenes) into a game build and play from there.

## Scenes

In `Assets/Dodgeball/Scenes/` eight scenes are provided from this project. They are:
* `Large_Obs.unity`
  
![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Small_Sparse_Arena.png)
* `Large_Obs_Dense.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Small_Dense_Arena.png)
* `XL_Obs.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Large_Sparse_Arena.png)
* `XL_Obs_Dense.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Large_Dense_Arena.png)
* `Large_WPM_Obs.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Small_Sparse_WP_image.png)
* `Large_WPM_Obs_Dense.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Large_Dense_WP_image.png)
* `XL_WPM_Obs.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Large_Sparse_WP_image.png)
* `XL_WPM_Obs_Dense.unity`

![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Large_Dense_WP_image.png)

Obs differentiates between the scenarios which include the modified observation space and those that do not. WPM stands for waypoint manual, which was the final iteration of our waypoint movement system and will be discussed in the Waypoint Movement section of this document.  Large and XL refers to the two different sizes of arena in which we tested our waypoint movement system against the original continuous implementation. 

### Elimination

In the elimination scenes, four players face off against another team of four. Balls are dropped throughout the stage, and players must pick up balls and throw them at opponents. If a player is hit twice by an opponent, they are "out", and sent to the penalty podium in the top-center of the stage.

![EliminationVideo](/doc_images/ShorterElimination.gif)

The original dodgeball environment includes the option for capture the flag, but we did not use it during the course of this project. All results and scenes take place in the Elimination gamemode. 

## Training

ML-Agents DodgeBall was built using *ML-Agents Release 18* (Unity package 2.1.0-exp.1). We recommend the matching version of the Python trainers (Version 0.27.0) though newer trainers should work. See the [Releases Page](https://github.com/Unity-Technologies/ml-agents#releases--documentation) on the ML-Agents Github for more version information.

To train DodgeBall, in addition to downloading and opening this environment, you will need to [install the ML-Agents Python package](https://github.com/Unity-Technologies/ml-agents/blob/release_18_docs/docs/Installation.md#install-the-mlagents-python-package). Follow the [getting started guide](https://github.com/Unity-Technologies/ml-agents/blob/release_18_docs/docs/Getting-Started.md) for more information on how to use the ML-Agents trainers.

You will need to use either the official Unity scenes or the eight additional scenes provided for training. Since training takes a *long* time, we recommend building these scenes into a Unity build.

Two configuration YAML (`DodgeBall.yaml` and `DodgeBall_seperate_policies.yaml`) for ML-Agents is provided. The seperate policies YAML is used to train the two different types of agents discussed in this project; long and short-range. You can uncomment and increase the number of environments (`num_envs`) depending on your computer's capabilities.

After tens of millions of steps (this will take many, many hours!) your agents will start to improve. As with any self-play run, you should observe your [ELO increase over time](https://github.com/Unity-Technologies/ml-agents/blob/release_18_docs/docs/Using-Tensorboard.md#self-play). Check out these videos ([Elimination](https://www.youtube.com/watch?v=Q9cIYfGA1GQ), [Capture the Flag](https://www.youtube.com/watch?v=SyxVayp01S4)) for an example of what kind of behaviors to expect at different stages of training. In our experiments, we trained agents for 20M steps to get a good understanding of learning capabilities, but this is not nearly enough to reach convergence. Unity trained the original (simpler) models for 160M steps. These extreme training times are the inspiration for this project, as new methods are needed to reduce the computational requirements of reinforcement learning projects. 

### Environment Parameters

To produce the results in the blog post, we used the default environment as it is in this repo. However, we also provide [environment parameters](https://github.com/Unity-Technologies/ml-agents/blob/release_18_docs/docs/Training-ML-Agents.md#environment-parameters) to adjust reward functions and control the environment from the trainer. You may find it useful, for instance, to experiment with curriculums.

| **Parameter**              | **Description**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| :----------------------- | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `is_capture_the_flag`| Set this parameter to 1 to override the scene's game mode setting, and change it to Capture the Flag. Set to 0 for Elimination.|
| `time_bonus_scale`| (default = `1.0` for Elimination, and `0.0` for CTF) Multiplier for negative reward given for taking too long to finish the game. Set to 1.0 for a -1.0 reward if it takes the maximum number of steps to finish the match.|
| `elimination_hit_reward`| (default = `0.1`) In Elimination, a reward given to an agent when it hits an opponent with a ball.|


# Extending DodgeBall to Emulate Military Training Scenarios 

## Infinite Ammunition 
The first change that was needed to convert the original dodgeball scenario into a military-esque scenario was infinite ammunition. We implemented a system that destroys projectiles on impact and returns them into the possession of the agent. This removes the need to go and recover balls, which distracts from tactical movement and adds an unnecessary layer of complexity for the agents to learn. 


![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/infinite_ammo_video_AdobeExpress.gif))

## 3D Terrains 
![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Screenshot%20(27).png)


The next step was developing more realistic terrain. Battle scenarios will seldom occur on flat ground, so we imported data from the Razish Army Training Facility which allowed us to train our agents on a low-fidelity version of real-world training terrain. All the scenarios we tested included hills, which can be distinguished by the areas with different lighting and contour.  

This new setup requires additional raycasts, so that the agents can detect opponents or walls that are not at the same altitude as them. This is crucial for developing intelligent policies when uneven terrain is introduced. 


![Screenshot (26)](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/67abac7c-67e6-4560-b91d-2a39d9e7d5c3)

## Modified Observation and Action Spaces 
Some modifications were made to the agents observation and action spaces were made to better fit our needs. The observation spaces are smaller due to the removal of unnecessary observations that only apply to the Capture the Flag gamemode. Additionally, the dash action was removed as it was a bit awkward in our scenario, especially when moving along waypoints. 
## Shooting Vertically 
Another obvious additon to our scenario was the ability to shoot vertically. Opponents should be able to fire at angles other than parallel to the ground so that they can target opponents at various different altitudes. 


![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/Autoshoot_vertical_gif_AdobeExpress.gif)

## Aim-assist 
We attempted to train some models which were able to choose the angle of their shots, but this drastically increases the complexity of the environment. Our solution was to implement aim-assist, which targets the opponent closest to the shooter's forward direction and automatically fires directly at it. This removes the need for fine tuning aim and encourages learning intelligent positioning and movement over high-precision skills. This method achieved far better results, so it was used in most of our simulations and all the experiments in this repository. 


![](https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/blob/develop/Media/autoshoot_demo_gif_AdobeExpress.gif)


## Introducing Roles 
We also investigated the ability to introduce different roles within the same team. We hoped to see whether the agents could learn a more complicated strategy to cooperate and utilize each individuals strengths. This was studied using short and long range units with different capabilities. The short-range units have half the aim-assist range but twice the fire-rate. We found that the agents did in fact learn their role. Short-range units learned more aggressive policies and the long-range units tended to remain in the rear. The long-range units can be distinguished by their darker color. 


https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/e9222e3b-f3ae-4f1b-8e15-41123a8eb155


## Waypoint Movement 
Due to the large computational requirements of reinforcement learning, we were not able to run our simulations for the same 160 million training steps that the original project did. This fact combined with the increased complexity of our environments led us to develop a method to reduce training time. We developed a waypoint movement system which aims to reduce the complexity of our environments and reduce the frequency of reinforcement learning steps while retaining the core positional strategy. This system limits agents to walking along the waypoints we generate onto the terrain, allowing us to automate shooting and only utilize reinforcement learning for the agents' movement. We only request a decision from the learned policy at each waypoint which is translated to the direction of travel to the next waypoint. This increases the time between decisions by 700% while maintaining and sometimes improving upon tactical performance. We also developed a system to automatically generate these waypoints so the system can be quickly implemented on any unity terrain. The code for waypoint generation is found under Assets/ScoutMission/WaypointGeneration/


https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/685daa70-9410-440a-a83f-79c7f4b3b641


# Trained Policies
Below is video of the final trained policies for each of the scenarios we created, along with their corresponding ELO scores from self-play. We used ELO score as our metric for learning, but due to the differences between continuous and waypoint scenarios it is not a perfect metric. Our solution was to test the policies for each movement system directly against each other to see which performed better. This requires removing the waypoint restraints and thus creates a disadvantage for agents which were not trained in these conditions. Nevertheless, the waypoint-based agents outcompete the continuous movement agents consistently. They also score achieve higher ELO scores in most scenarios. 

Note: Some of the videos had to be cropped to meet GitHub's file size limitations.

### Small Continuous
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/d7ea3ae4-781f-4ec5-a383-02830fa262b1

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/796b1819-0587-43fc-8ab9-11c33118b2b3" width="500" height="500">

### Small Waypoint
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/956d2396-ec65-4893-8f69-c9fb94bfff3d

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/636bba0a-55a0-42d7-9f58-3b32e3f52ea7" width="500" height="500">

### Small Continuous with Dense Obstacles
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/186e3624-8afd-4562-a32d-b98450e4fe85

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/5547c0fe-061f-41ab-bd8a-8e9a09699614" width="500" height="500">

### Small Waypoint with Dense Obstacles
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/a35b58a9-b102-499d-a995-93c3fc962896

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/7c9c5fec-07e5-4477-a1cb-09cdde1117d5" width="500" height="500">

### Large Continuous
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/04b85e5d-5193-44f4-8f2f-2eff749d0b12

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/9c1c6ebe-d348-4734-bcc3-d5b9a69d950c" width="500" height="500">

### Large Waypoint
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/2f694291-fb34-4fab-928d-4885d9640a87

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/93cbc25c-0fa0-4a10-a66d-460409ee980c" width="500" height="500">

### Large Continuous with Dense Obstacles
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/5c50ed13-315d-4f1f-ab51-067dfea37513

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/daeb57da-80de-486c-8b29-d6af6482048c" width="500" height="500">

### Large Waypoint with Dense Obstacles
https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/4ad9c17e-230d-4fd4-bb79-50d21b8db8e0

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/dedc466e-c53f-4b78-8cf5-11528a1f1a4b" width="500" height="500">

## Continuous VS. Waypoint ELO Scores 

### Small Arena 

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/3b8b248f-03e4-4d0a-b0af-211b7db2b240" width="500" height="500">

### Small Arena with Dense Obstacles

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/4b6138c9-cfd0-479e-b2df-1927eac2ce97" width="500" height="500">

### Large Arena 

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/5a1878aa-fba0-459d-835e-d077ac9994f4" width="500" height="500">

### Large Arena with Dense Obstacles

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/4cb73dea-3a5e-43f3-8896-b985e1d8e521" width="500" height="500">

## Verification 
In addition to tracking ELO as an indicator of learning, we tested the waypoint-based agents directly against a team of agents that were trained using the original continuous movement. This was accomplished by removing the waypoints and retaining the longer time between decisions and discretized movement. In other words, the waypoint-based team picks one of 8 directions or to stand still and then continuous that course of action for 40 fixed updates. On the other hand, the continuous movement team retains its normal movement and makes decisions every 5 fixed updates. Despite the fact that the continuous movement team have home court advantage, the policies learned by our waypoint movement method were able to consistently outperform the continuous movement team. 

Each scenario was ran 100 times with scores and video provided below. 

### Small Arena

https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/a5f10e6e-35f0-4085-b903-f1bf20dfb0f9

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/6ebf5141-8d44-4984-86f2-99bc480635ae" width="600" height="350">

### Small Arena with Dense Obstacles

https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/cf9e279c-b86e-459c-8049-ee1b9ba29b68

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/a494b5cd-4559-4585-abad-0a20184f6895" width="600" height="350">

### Large Arena

https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/6636e9d4-027e-488c-be2f-c7a570ab034c

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/9c2c7a5e-34c9-419d-844b-92148128c8a5" width="600" height="350">

### Large Arena with Dense Obstacles

https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/23629cf6-2931-477f-8f4c-3d03a910de43

<img src="https://github.com/calebkoresh/ml-agents-dodgeball-env-ICT/assets/80787784/5bca9b11-10b4-4d45-ae33-8a5e1256d680" width="600" height="350">

## Conclusion
Automatically generating waypoints is an efficient way to discretize the state space in military training scenarios so that reinforcement learning agents can learn intelligent policies more efficiently. Our results show that agents trained on a waypoint system can transfer their knowledge back into a continuous space and outperform agents who were not trained using waypoints. Furthermore, the advantage seems to grow as the complexity of the terrain increases. 

In general, it seems most effective to use reinforcement learning to decide higher level behavior and strategy while hard-coding fine skills like aiming a projectile. Furthermore, state spaces should be discretized whenever possible to increase learning speed. 
