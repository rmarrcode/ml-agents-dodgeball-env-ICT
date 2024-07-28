import torch
import torch.nn as nn
import torch.optim as optim
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple
from torch.distributions import Normal
import torch.nn.functional as F
import numpy as np
import sys
from debug_side_channel import DebugSideChannel

TRAINING_STEPS = 10000
OBSERVATION_SIZE = 6
ACTION_SIZE = 5
HIDDEN_SIZE = 128
LEARNING_RATE = 0.001
UPDATE_PERIOD = 100
AGENT_ID_A = 0
AGENT_ID_B = 1
NO_AGENTS = 2
BUFFER_SIZE = 16
BATCH_SIZE = 8
GAMMA = 0.99
TAU = 0.005
ALPHA = 0.2

Unity_Environment = "C:\\Users\\rmarr\\Documents\\ml-agents-dodgeball-env-ICT"
NO_SIMULATIONS = 1
TIME_SCALE = 2.0

debug_side_channel = DebugSideChannel()

torch.autograd.set_detect_anomaly(True)

class Actor(nn.Module):
    def __init__(self, state_dim, action_dim, action_size, hidden_size, init_w=3e-3, log_std_min=-20, log_std_max=2):
        super().__init__()
        self.init_w = init_w
        self.log_std_min = log_std_min
        self.log_std_max = log_std_max
        self.fc1 = nn.Linear(state_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, hidden_size)
        self.mu = nn.Linear(hidden_size, action_dim)
        self.mu.weight.data.uniform_(-init_w, init_w)
        self.mu.bias.data.uniform_(-init_w, init_w)

        self.log_std = nn.Linear(hidden_size, action_dim)
        self.log_std.weight.data.uniform_(-init_w, init_w)
        self.log_std.bias.data.uniform_(-init_w, init_w)

    def forward(self, state):
        if isinstance(state, tuple) or isinstance(state, list):
            return [self.forward_single(s) for s in state]
        return self.forward_single(state)
    
    def forward_single(self, state):
        x = F.relu(self.fc1(state))
        x = F.relu(self.fc2(x))
        mu = self.mu(x)
        std = self.log_std(x)
        std = torch.clamp(std, min=self.log_std_min, max=self.log_std_max)
        return mu, std
    
    def select_action_batch(self, state):
        return torch.stack([self.select_action_single(s) for s in state])

    def select_action_single(self, state):
        #state = torch.FloatTensor(state).unsqueeze(0)
        mu, log_std = self.forward(state)
        print(f'mu {mu}')
        std = log_std.exp()
        
        normal = Normal(0, 1)
        z = normal.sample()
        action = torch.tanh(mu + std*z)

        action = (action + 1) * (ACTION_SIZE/2)
        action = torch.round(action)
        action = torch.clamp(action, min=0, max=ACTION_SIZE-1)
        return action

class Critic(nn.Module):
    def __init__(self, state_dim, action_dim, hidden_size):
        super().__init__()
        self.fc1 = nn.Linear(state_dim + action_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, hidden_size)
        self.q = nn.Linear(hidden_size, 1)

    def forward(self, state, action):
        if isinstance(state, tuple) or isinstance(state, list):
            return [self.forward_single(s, a)[0] for s, a in zip(state, action)]
        return self.forward_single(state, action)

    def forward_single(self, state, action):
        x = torch.cat([state, action], 1)
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        q = self.q(x)
        return q

class ReplayBuffer:
    def __init__(self):
        self.buffer = []

    def size(self):
        return len(self.buffer)

    def add(self, experience):
        if len(self.buffer) < BUFFER_SIZE:
            self.buffer.append(experience)
        else:
            self.buffer[len(self.buffer) % BUFFER_SIZE] = experience

    def sample(self, batch_size):
        indices = np.random.choice(len(self.buffer), batch_size)
        states, actions, rewards, next_states, dones = zip(*[self.buffer[i] for i in indices])
        return (states, actions, rewards, next_states, dones)

class SACAgent(nn.Module):
    def __init__(self, observation_size, action_size, hidden_size):
        super().__init__()
        self.critic1 = Critic(observation_size, action_size, 16)
        self.critic2 = Critic(observation_size, action_size, 16)
        self.target_critic1 = Critic(observation_size, action_size, 16)
        self.target_critic2 = Critic(observation_size, action_size, 16)
        self.actor = Actor(observation_size, 1, action_size, 32)

        self.target_critic1.load_state_dict(self.critic1.state_dict())
        self.target_critic2.load_state_dict(self.critic2.state_dict())

        self.critic1_optimizer = optim.Adam(self.critic1.parameters(), lr=LEARNING_RATE)
        self.critic2_optimizer = optim.Adam(self.critic2.parameters(), lr=LEARNING_RATE)
        self.actor_optimizer = optim.Adam(self.actor.parameters(), lr=LEARNING_RATE)

class Driver():
    def __init__(self):
        self.agent_registry = []
        self.agent_registry.append(SACAgent(observation_size=OBSERVATION_SIZE, action_size=1, hidden_size=HIDDEN_SIZE))
        self.agent_registry.append(SACAgent(observation_size=OBSERVATION_SIZE, action_size=1, hidden_size=HIDDEN_SIZE))
        self.behavior_registry = []
        self.replay_buffer_resistry = []
        self.replay_buffer_resistry.append(ReplayBuffer())
        self.replay_buffer_resistry.append(ReplayBuffer())
        self.engine_channel = EngineConfigurationChannel()
        self.env = UnityEnvironment(file_name=Unity_Environment, side_channels=[self.engine_channel, debug_side_channel])
        self.env.reset()
        self.engine_channel.set_configuration_parameters(time_scale=TIME_SCALE)
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[AGENT_ID_A])
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[AGENT_ID_B])
        self.score_board = {key: 0 for key in range(NO_AGENTS)}

    def transition_tuple(self, agent_id):
        behavior_name = self.behavior_registry[agent_id]
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        reward = torch.tensor([0])
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] < -1.0:
                reward = torch.tensor([-1])
        state = decision_steps.obs[0][0]

        done = len(terminal_steps) > 0
        done = torch.tensor([done])
        s = torch.tensor(state, dtype=torch.float32)
        agent = self.agent_registry[agent_id]
        a = agent.actor.select_action_single(s)
        action = ActionTuple()
        action.add_discrete(a.cpu().detach().numpy().reshape(NO_SIMULATIONS, 1))
        self.env.set_actions(behavior_name, action)
        self.env.step()

        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] > 0:
                self.score_board[agent_id] = self.score_board[agent_id] + 1
                sys.stdout.write("\r" + f"Agent A - Red: {self.score_board[0]} | Agent B - Blue: {self.score_board[1]}")
                sys.stdout.flush()

        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        next_state = decision_steps.obs[0]
        s_p = torch.tensor(next_state[0])

        return (s, a, reward, s_p, done)

    def train(self, agent_id):

        if self.replay_buffer_resistry[agent_id].size() < BATCH_SIZE:
            return

        # Not worrying about ENTROPY for now
        with torch.no_grad():
            agent = self.agent_registry[agent_id]
            buffer = self.replay_buffer_resistry[agent_id]

            states, actions, rewards, next_states, dones = buffer.sample(BATCH_SIZE)
            # why does squeeze not work
            states = torch.stack(states).squeeze()
            actions = torch.stack(actions).squeeze()
            rewards = torch.stack(rewards).squeeze()
            next_states = torch.stack(next_states)
            dones = torch.tensor(dones, dtype=torch.float32).squeeze()

            # new vals
            next_actions = agent.actor.select_action_batch(next_states)
            #next_mu, next_log_std = agent.actor(next_states)
            #next_std = next_log_std.exp()
            #next_normal = Normal(next_mu, next_std)
            #next_actions = torch.tanh(next_normal.rsample())
            #next_log_probs = next_normal.log_prob(next_actions).sum(-1, keepdim=True)
            next_q1 = agent.target_critic1(next_states, next_actions)
            next_q2 = agent.target_critic2(next_states, next_actions)
            #next_q = (torch.min(next_q1, next_q2) - ALPHA * next_log_probs).squeeze()
            next_q = torch.min(next_q1, next_q2).squeeze()
            target_q = rewards + (1 - dones) * GAMMA * next_q

        q1 = agent.critic1(states, actions.unsqueeze(1)).squeeze()
        q2 = agent.critic2(states, actions.unsqueeze(1)).squeeze()

        critic1_loss = F.mse_loss(q1, target_q)
        critic2_loss = F.mse_loss(q2, target_q)

        agent.critic1_optimizer.zero_grad()
        critic1_loss.backward(retain_graph=True)
        agent.critic1_optimizer.step()

        agent.critic2_optimizer.zero_grad()
        critic2_loss.backward(retain_graph=True)
        agent.critic2_optimizer.step()

        new_mu, new_log_std = agent.actor(states)
        new_std = new_log_std.exp()
        new_normal = Normal(new_mu, new_std)
        new_actions = torch.tanh(new_normal.rsample())
        log_probs = new_normal.log_prob(new_actions).sum(-1, keepdim=True)
        q1_new = agent.critic1(states, new_actions)
        q2_new = agent.critic2(states, new_actions)
        q_new = torch.min(q1_new, q2_new)

        actor_loss = (ALPHA * log_probs - q_new).mean()

        agent.actor_optimizer.zero_grad()
        actor_loss.backward(retain_graph=True)
        agent.actor_optimizer.step()

        for target_param, param in zip(agent.target_critic1.parameters(), agent.critic1.parameters()):
            target_param.data.copy_(TAU * param.data + (1 - TAU) * target_param.data)

        for target_param, param in zip(agent.target_critic2.parameters(), agent.critic2.parameters()):
            target_param.data.copy_(TAU * param.data + (1 - TAU) * target_param.data)

    def main(self):
        for episode in range(TRAINING_STEPS):
            [self.replay_buffer_resistry[agent_id].add(self.transition_tuple(agent_id)) for agent_id in range(NO_AGENTS)]
            if (episode + 1) % BUFFER_SIZE == 0:
                [self.train(agent_id) for agent_id in range(NO_AGENTS)]
        self.env.close()
        

if __name__ == "__main__":
    driver = Driver()
    driver.main()