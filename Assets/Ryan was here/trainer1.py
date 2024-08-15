import numpy as np
import matplotlib.pyplot as plt
import torch
from Discrete_SAC_Agent import SACAgent

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple

from debug_side_channel import DebugSideChannel
from gym import spaces

import torch
import torch.nn.functional as F
from torch.distributions import Normal
import sys
import wandb
import numpy as np
import pandas as pd

TRAINING_EVALUATION_RATIO = 4
RUNS = 2
EPISODES_PER_RUN = 400
STEPS_PER_EPISODE = 200
WANDB = True

if WANDB:
    wandb.init(
        project="visibility-game",
    )

class Env():
    def __init__(self, config):
        self.observation_space = spaces.Tuple((spaces.Discrete(10), spaces.Discrete(1), spaces.Discrete(10))) 
        self.action_space = spaces.Discrete(5) 
        self.engine_channel = EngineConfigurationChannel()
        self.debug_channel = DebugSideChannel()
        self.env = UnityEnvironment(file_name=config['unity_environment'], 
                                    side_channels=[self.engine_channel, self.debug_channel])
        self.env.reset()
        self.engine_channel.set_configuration_parameters(time_scale=config['time_scale'])
        self.behavior_registry = []
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[0])

    def get_state(self):
        # need to figure out negative reward later
        behavior_name = self.behavior_registry[0]
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        state = decision_steps.obs[0][0]
        return state
        
    def step(self, action):
        behavior_name = self.behavior_registry[0]
        action_tuple = ActionTuple()
        action_tuple.add_discrete(action.reshape(1, 1))
        self.env.set_actions(behavior_name, action_tuple)
        self.env.step()
        reward = 0.
        done = False
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] > 0:
                print('win')
                reward = 1.
        if len(terminal_steps) > 0:
            done = True 
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        next_state = decision_steps.obs[0][0]
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] > 0:
                next_state = self.debug_channel.get_last_state()
        return reward, next_state, done

if __name__ == "__main__":
    agent_results = []
    driver_config = {'unity_environment': 'C:\\Users\\rmarr\\Documents\\ml-agents-dodgeball-env-ICT',
                    'time_scale': 1.0}
    environment = Env(driver_config)
    for run in range(RUNS):
        agent = SACAgent(environment)
        run_results = []
        run_reward = 0
        for episode_number in range(EPISODES_PER_RUN):
            #print('\r', f'Run: {run + 1}/{RUNS} | Episode: {episode_number + 1}/{EPISODES_PER_RUN}', end=' ')
            evaluation_episode = episode_number % TRAINING_EVALUATION_RATIO == 0
            episode_reward = 0
            
            environment.env.reset()
            state = environment.get_state()

            done = False
            i = 0
            while not done and i < STEPS_PER_EPISODE:
                i += 1
                action = agent.get_next_action(state, evaluation_episode=evaluation_episode)
                reward, next_state, done = environment.step(action)
                run_reward = run_reward + 1 
                if not evaluation_episode:
                    agent.train_on_transition(state, action, next_state, reward, done)
                else:
                    episode_reward += reward
                state = next_state

            if evaluation_episode:
                run_results.append(episode_reward)
        agent_results.append(run_results)
        wandb.log({"runs wins": run_reward})


