import torch
import torch.nn as nn
import torch.optim as optim
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple  
# import torch.distributions as D
from torch.distributions import Categorical
import numpy as np

TRAINING_STEPS = 10000
OBSERVATION_SIZE = 6
ACTION_SIZE = 5
HIDDEN_SIZE = 128
LEARNING_RATE = .001
UPDATE_PERIOD = 100

AGENT_ID_A = 0
AGENT_ID_B = 1
NO_AGENTS = 2

Unity_Environment = "C:\\Users\\rmarr\\Documents\\ml-agents-dodgeball-env-ICT"
NO_SIMULATIONS = 1
TIME_SCALE = 2.0
# state
# 10 x 10 grid 
# 0 = nothing 1 = wall 2 = agent position

class ReplayBuffer():
    def __init__(self):
        self.buffer = []#{}
        self.total = 0
    def add(self, experience):
        # if experience in self.buffer:
        #     self.buffer[experience] = self.buffer[experience] + 1
        # else:
        #      self.buffer[experience] = 1
        self.buffer.append(experience)
    def done(self):
        return self.buffer[-1][4]

class SACAgent(nn.Module):
    def __init__(self, observation_size, action_size, hidden_size):
        super(SACAgent, self).__init__()
        self.observation_size = observation_size
        self.action_size = action_size
        self.hidden_size = hidden_size
        # Q1
        self.fc1 = nn.Linear(observation_size, hidden_size)
        self.fc2 = nn.Linear(hidden_size, 1)
        # Q2
        self.fc3 = nn.Linear(observation_size, hidden_size)
        self.fc4 = nn.Linear(hidden_size, 1)
        # Pi
        self.fc5 = nn.Linear(observation_size, hidden_size)
        self.fc6 = nn.Linear(hidden_size, action_size)

    def Q1(self, state, action):
        q_in = torch.cat((state, torch.one_hot(action)))
        x = self.fc1(q_in)
        x = torch.relu(x)
        x = self.fc2(x)
        x = torch.softmax(x, dim=0)
        return x 

    def q1_loss(self, state, action):
        q = self.Q1(state, action)
        
    def Q2(self, state, action):
        q_in = torch.cat((state, torch.one_hot(action)))
        x = self.fc1(q_in)
        x = torch.relu(x)
        x = self.fc2(x)
        x = torch.softmax(x, dim=0)
        return x 

    def q2_loss(self, state, action):
        q = self.Q2(state, action)

    # maybe switch to paper's implementation
    def Pi(self, state, no_simulations):
        x = self.fc5(state)
        x = torch.relu(x)
        x = self.fc6(x)
        x = torch.softmax(x, dim=0)
        categorical_dist = Categorical(logits=x[0])
        return categorical_dist.sample((no_simulations,))


class Driver():
    def __init__(self):
        self.agent_registry = []
        self.agent_registry.append(SACAgent(observation_size=OBSERVATION_SIZE, action_size=ACTION_SIZE, hidden_size=HIDDEN_SIZE))
        self.agent_registry.append(SACAgent(observation_size=OBSERVATION_SIZE, action_size=ACTION_SIZE, hidden_size=HIDDEN_SIZE))
        self.behavior_registry = []
        self.replay_buffer_resistry = []
        self.replay_buffer_resistry.append(ReplayBuffer())
        self.replay_buffer_resistry.append(ReplayBuffer())
        self.engine_channel = EngineConfigurationChannel()
        self.env = UnityEnvironment(file_name=Unity_Environment, side_channels=[self.engine_channel])
        self.env.reset()
        self.engine_channel.set_configuration_parameters(time_scale=TIME_SCALE)
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[AGENT_ID_A])
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[AGENT_ID_B])
        self.score_board = {key: 0 for key in range(NO_AGENTS)}

    def transition_tuple(self, agent_id):
        behavior_name = self.behavior_registry[agent_id]
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        print(f'decision_steps {decision_steps}')
        print(f'terminal_steps {terminal_steps}')
        state = decision_steps.obs[0]
        reward = terminal_steps.reward
        if reward > 0:
            print(f'agent: {agent_id} got reward: {reward}')
            self.score_board[agent_id] = self.score_board[agent_id] + 1
            print(self.score_board)
        done = len(terminal_steps) > 0
        if done:
            self.env.reset()
        s = torch.tensor(state, dtype=torch.float32)

        agent = self.agent_registry[agent_id]

        a = agent.Pi(s, NO_SIMULATIONS)

        action = ActionTuple()
        action.add_discrete(a.cpu().numpy().reshape(NO_SIMULATIONS, 1))
        self.env.set_actions(behavior_name, action)
        self.env.step()

        if not done:
            decision_steps, terminal_steps = self.env.get_steps(behavior_name)
            next_state = decision_steps.obs[0]
            s_p = next_state#.cpu().numpy()
        else:
            s_p = None

        return (s, a, reward, s_p, done)


    def main(self):
        
        #print(f'behavior_name {behavior_name}')
        #spec = env.behavior_specs[behavior_name]
        #print(f'spec {spec}')
        
        # TODO one optimizer or 2?
        params = list(self.agent_registry[AGENT_ID_A].parameters()) + list(self.agent_registry[AGENT_ID_B].parameters()) 
        opt = optim.Adam(params, lr=LEARNING_RATE)

        for episode in range(TRAINING_STEPS):
            # Agent actions
            [self.replay_buffer_resistry[AGENT_ID].add(self.transition_tuple(AGENT_ID)) for AGENT_ID in range(NO_AGENTS)]
            done = any([self.replay_buffer_resistry[AGENT_ID].done() for AGENT_ID in range(NO_AGENTS)])
            if done:
                self.env.reset()
            # Agent B action
            # decision_steps_a, terminal_steps_a = env.get_steps(behavior_name_a)
            # state = decision_steps_a.obs[0]
            # done = len(terminal_steps_a) > 0
            # if done:
            #     
            # state = torch.tensor(state, dtype=torch.float32)

            # action_a = agentA.Pi(state)

            # action = ActionTuple()
            # action.add_discrete(action_a.cpu().numpy())
            # env.set_actions(behavior_name_a, action)
            # env.step()

            # new_decision_steps, new_terminal_steps = env.get_steps(behavior_name)
            # new_state = new_decision_steps.obs[0] if not done else terminal_steps.obs[0]
            # reward = new_decision_steps.reward if not done else terminal_steps.reward
            # next_state = torch.tensor(new_state, dtype=torch.float32)

            # loss = 0
            # loss.backward()
            # opt.step()

        self.env.close()

if __name__ == "__main__":
    driver = Driver()
    driver.main()
