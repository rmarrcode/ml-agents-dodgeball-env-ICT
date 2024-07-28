import torch.optim as optim
import torch
import torch.nn as nn
from torch.distributions import Normal
import torch.nn.functional as F
import numpy as np

class Actor(nn.Module):
    def __init__(self, state_dim, action_size, action_dim, hidden_size, init_w=3e-3, log_std_min=-20, log_std_max=2):
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

        self.action_size = action_size

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
        mu, log_std = self.forward(state)
        std = log_std.exp()
        
        normal = Normal(0, 1)
        z = normal.sample()
        action = torch.tanh(mu + std*z)

        action = (action + 1) * (self.action_size/2)
        action = torch.round(action)
        action = torch.clamp(action, min=0, max=self.action_size-1)
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
    def __init__(self, buffer_size):
        self.buffer_size = buffer_size
        self.buffer = []

    def size(self):
        return len(self.buffer)

    def add(self, experience):
        if len(self.buffer) < self.buffer_size:
            self.buffer.append(experience)
        else:
            self.buffer[len(self.buffer) % self.buffer_size] = experience

    def sample(self, batch_size):
        indices = np.random.choice(len(self.buffer), batch_size)
        states, actions, rewards, next_states, dones = zip(*[self.buffer[i] for i in indices])
        return (states, actions, rewards, next_states, dones)

class SACAgent(nn.Module):
    def __init__(self, observation_size, action_size, action_dim, hidden_size, learning_rate):
        super().__init__()
        self.critic1 = Critic(observation_size, action_dim, hidden_size)
        self.critic2 = Critic(observation_size, action_dim, hidden_size)
        self.target_critic1 = Critic(observation_size, action_dim, hidden_size)
        self.target_critic2 = Critic(observation_size, action_dim, hidden_size)
        self.actor = Actor(observation_size, action_size, action_dim, 32)

        self.target_critic1.load_state_dict(self.critic1.state_dict()) 
        self.target_critic2.load_state_dict(self.critic2.state_dict())

        self.critic1_optimizer = optim.Adam(self.critic1.parameters(), lr=learning_rate)
        self.critic2_optimizer = optim.Adam(self.critic2.parameters(), lr=learning_rate)
        self.actor_optimizer = optim.Adam(self.actor.parameters(), lr=learning_rate)