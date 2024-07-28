from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple

from debug_side_channel import DebugSideChannel
from sac_agent import SACAgent, ReplayBuffer

import torch
import torch.nn.functional as F
from torch.distributions import Normal
import sys
import wandb

class Driver():
    def __init__(self, config):
        self.config = config
        self.agent_registry = []
        self.agent_registry.append(SACAgent(observation_size=config['observation_size'], action_size=config['action_size'], action_dim=config['no_simulations'], 
                                            hidden_size=config['hidden_size'], learning_rate=config['learning_rate']))
        self.agent_registry.append(SACAgent(observation_size=config['observation_size'], action_size=config['action_size'], action_dim=config['no_simulations'], 
                                            hidden_size=config['hidden_size'], learning_rate=config['learning_rate']))
        self.behavior_registry = []
        self.replay_buffer_resistry = []
        self.replay_buffer_resistry.append(ReplayBuffer(config['buffer_size']))
        self.replay_buffer_resistry.append(ReplayBuffer(config['buffer_size']))
        self.engine_channel = EngineConfigurationChannel()
        self.debug_channel = DebugSideChannel()
        self.env = UnityEnvironment(file_name=config['unity_environment'], 
                                    side_channels=[self.engine_channel, self.debug_channel])
        self.env.reset()
        self.engine_channel.set_configuration_parameters(time_scale=config['time_scale'])
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[config['agent_id_a']])
        self.behavior_registry.append(list(self.env.behavior_specs.keys())[config['agent_id_b']])
        self.score_board = {key: 0 for key in range(config['no_agents'])}
        self.wins = 0

    def transition_tuple(self, agent_id):
        behavior_name = self.behavior_registry[agent_id]
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        reward = torch.tensor([0])
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] < -1.0:
                reward = torch.tensor([-1])
        state = decision_steps.obs[0][0][:-1]
        done = len(terminal_steps) > 0
        done = torch.tensor([done])
        s = torch.tensor(state, dtype=torch.float32)
        agent = self.agent_registry[agent_id]
        a = agent.actor.select_action_single(s)
        action = ActionTuple()
        action.add_discrete(a.cpu().detach().numpy().reshape(self.config['no_simulations'], 1))
        self.env.set_actions(behavior_name, action)
        self.env.step()

        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] > 0:
                reward = torch.tensor([1])
                self.wins = self.wins + 1
                self.score_board[agent_id] = self.score_board[agent_id] + 1
                sys.stdout.write("\r" + f"Agent A - Red: {self.score_board[0]} | Agent B - Blue: {self.score_board[1]}")
                sys.stdout.flush()
        if len(terminal_steps) > 0:
            done = torch.tensor([True])
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        next_state = decision_steps.obs[0][0][:-1]
        s_p = torch.tensor(next_state)
        if done:
            final_state = self.debug_channel.get_last_state()
            s_p = torch.tensor(final_state)
        
        return (s, a, reward, s_p, done)

    def train(self, agent_id):
        if self.replay_buffer_resistry[agent_id].size() < self.config['batch_size']:
            return
        if self.config['wandb_log']:
            wandb.init(
                project="visibility-game",
            )

        # Not worrying about ENTROPY for now
        with torch.no_grad():
            agent = self.agent_registry[agent_id]
            buffer = self.replay_buffer_resistry[agent_id]
            import json 
            with open('replay_buffer.json', 'w') as file:
                json.dump([[e.tolist() for e in t] for t in buffer.buffer], file)
            states, actions, rewards, next_states, dones = buffer.sample(self.config['batch_size'])
        
            # why does squeeze not work
            states = torch.stack(states).squeeze()
            actions = torch.stack(actions).squeeze()
            rewards = torch.stack(rewards).squeeze()
            next_states = torch.stack(next_states)
            dones = torch.tensor(dones, dtype=torch.float32).squeeze()

            # new vals
            next_actions = agent.actor.select_action_batch(next_states)
            next_mu, next_log_std = agent.actor(next_states)
            next_std = next_log_std.exp()
            next_normal = Normal(next_mu, next_std)
            next_actions = torch.tanh(next_normal.sample())
            next_log_probs = next_normal.log_prob(next_actions).sum(-1, keepdim=True)
            next_q1 = agent.target_critic1(next_states, next_actions)
            next_q2 = agent.target_critic2(next_states, next_actions)
            #next_q = (torch.min(next_q1, next_q2) - self.config['alpha'] * next_log_probs).squeeze()
            next_q = torch.min(next_q1, next_q2).squeeze()
            target_q = rewards + (1 - dones) * self.config['gamma'] * next_q 

            take_action = (next_actions + 1) * (5/2)
            take_action = torch.round(take_action)
            take_action = torch.clamp(take_action, min=0, max=self.action_size-1)
            with open('debug.json', 'w') as file:
                json.dump(list(zip(next_states, take_action, next_q)))

        q1 = agent.critic1(states, actions.unsqueeze(1)).squeeze()
        q2 = agent.critic2(states, actions.unsqueeze(1)).squeeze()

        critic1_loss = F.mse_loss(q1, target_q)
        critic2_loss = F.mse_loss(q2, target_q)

        agent.critic1_optimizer.zero_grad()
        critic1_loss.backward(retain_graph=True)
        if self.config['wandb_log']:
            wandb.log({f"critic1_loss_{agent_id}": critic1_loss})
        agent.critic1_optimizer.step()

        agent.critic2_optimizer.zero_grad()
        critic2_loss.backward(retain_graph=True)
        if self.config['wandb_log']:
            wandb.log({f"critic2_loss_{agent_id}": critic2_loss})
        agent.critic2_optimizer.step()

        new_mu, new_log_std = agent.actor(states)
        new_std = new_log_std.exp()
        new_normal = Normal(new_mu, new_std)
        new_actions = torch.tanh(new_normal.sample())
        log_probs = new_normal.log_prob(new_actions).sum(-1, keepdim=True)
        q1_new = agent.critic1(states, new_actions)
        q2_new = agent.critic2(states, new_actions)
        q_new = torch.min(q1_new, q2_new)

        #actor_loss = (self.config['alpha'] * log_probs - q_new).mean()
        actor_loss = -1 * (q_new).mean()
        
        agent.actor_optimizer.zero_grad()
        actor_loss.backward(retain_graph=True)
        if self.config['wandb_log']:
            wandb.log({f"actor_loss_{agent_id}": actor_loss})
        agent.actor_optimizer.step()
        
        print(f'wins {self.wins}')
        if self.config['wandb_log']:
            wandb.log({'wins': self.wins})
            self.wins = 0

        tau = self.config['tau']
        for target_param, param in zip(agent.target_critic1.parameters(), agent.critic1.parameters()):
            target_param.data.copy_(tau * param.data + (1 - tau) * target_param.data)

        for target_param, param in zip(agent.target_critic2.parameters(), agent.critic2.parameters()):
            target_param.data.copy_(tau * param.data + (1 - tau) * target_param.data)

    def run(self):
        for episode in range(self.config['training_steps']):
            [self.replay_buffer_resistry[agent_id].add(self.transition_tuple(agent_id)) for agent_id in range(self.config['no_agents'])]
            if (episode + 1) % self.config['buffer_size'] == 0:
                self.train(0)
                #[self.train(agent_id) for agent_id in range(self.config['no_agents'])]
        self.env.close()