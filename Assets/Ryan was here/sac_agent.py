import torch.optim as optim
import torch
import torch.nn as nn
from torch.distributions import Normal
import torch.nn.functional as F
import numpy as np
import torch.nn.init as init

class Actor(nn.Module):
    def __init__(self, state_dim, action_dim, hidden_size):
        super().__init__()
        self.fc1 = nn.Linear(state_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, hidden_size)
        self.fc3 = nn.Linear(hidden_size, action_dim)
        self.action_dim = action_dim

    def forward(self, state):
        x = F.relu(self.fc1(state))
        x = F.relu(self.fc2(x))
        x = self.fc3(x)
        x = F.softmax(x, dim=-1)
        return x

    def get_action_nd(self, action_probs):
        discrete_action = np.random.choice(range(self.action_dim), p=action_probs)
        return discrete_action

    def get_action_d(self, action_probs):
        return np.argmax(action_probs)

    
class Critic(nn.Module):
    def __init__(self, state_dim, action_dim, hidden_size):
        super().__init__()
        self.fc1 = nn.Linear(state_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, hidden_size)
        self.fc3 = nn.Linear(hidden_size, action_dim)

    def forward(self, state):
        x = F.relu(self.fc1(state))
        x = F.relu(self.fc2(x))
        x = self.fc3(x)
        return x

    def choose_max_q(self, state):
        max_q = -99999
        max_q_a = 0
        for a in np.linspace(-1, 1, 100):
            action = torch.tensor(a, dtype=torch.float)
            q = self.forward_single(state.unsqueeze(0), action.unsqueeze(0).unsqueeze(0))
            if q > max_q:
                max_q = q
                max_q_a = action
        return max_q_a

    def draw_graph(self):
        positions = torch.tensor([ [[4.5, .5, 1.5]],
                                   [[5.5, .5, 1.5]],
                                   [[6.5, .5, 1.5]],
                                   [[4.5, .5, 0.5]],
                                   [[5.5, .5, 0.5]],
                                   [[6.5, .5, 0.5]],
                                   [[6.5, .5, 2.5]],
                                   [[6.5, .5, 3.5]],
                                   ])
        graph = {}
        for i in range(positions.shape[0]):
            graph[tuple(positions[i].squeeze(0).tolist())] = []
        for p in range(positions.shape[0]):
            for i in range(5):
                a = torch.tensor([[i]])
                graph[tuple(positions[p].squeeze(0).tolist())].append(self.forward(positions[p], a).squeeze(0).tolist())
        return graph

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

    def empty(self):
        return zip(*[self.buffer[i] for i in range(len(self.buffer))])
    

class SACAgent(nn.Module):
    def __init__(self, observation_size, action_dim, hidden_size, learning_rate):
        super().__init__()
        self.critic1 = Critic(observation_size, action_dim, hidden_size)
        self.critic2 = Critic(observation_size, action_dim, hidden_size)
        self.target_critic1 = Critic(observation_size, action_dim, hidden_size)
        self.target_critic2 = Critic(observation_size, action_dim, hidden_size)
        self.actor = Actor(observation_size, action_dim, hidden_size)

        self.target_critic1.load_state_dict(self.critic1.state_dict()) 
        self.target_critic2.load_state_dict(self.critic2.state_dict())

        self.critic1_optimizer = optim.Adam(self.critic1.parameters(), lr=learning_rate)
        self.critic2_optimizer = optim.Adam(self.critic2.parameters(), lr=learning_rate)
        self.actor_optimizer = optim.Adam(self.actor.parameters(), lr=learning_rate)