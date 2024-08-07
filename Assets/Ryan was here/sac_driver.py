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
import numpy as np
import pandas as pd

SEED = 6
torch.manual_seed(SEED)
np.random.seed(SEED)

class Driver():
    def __init__(self, config):
        self.config = config
        self.agent_registry = []
        for _ in range(config['no_agents']):
            self.agent_registry.append(SACAgent(
                                observation_size=config['observation_size'],
                                action_dim=config['action_dim'], 
                                hidden_size=config['hidden_size'],
                                learning_rate=config['learning_rate']))
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
        state = decision_steps.obs[0][0]
        done = len(terminal_steps) > 0
        done = torch.tensor([done])
        s = torch.tensor(state, dtype=torch.float32)
        # kinda bad fix
        agent = self.agent_registry[agent_id]
        action_probs = agent.actor.forward(s)
        a = agent.actor.get_action_nd(action_probs.detach().numpy())
        a_tens = torch.zeros(5, dtype=int)
        a_tens[a] = 1
        #a = agent.critic1.choose_max_q(s)
        action = ActionTuple()
        action.add_discrete(a.reshape(self.config['no_simulations'], 1))
        self.env.set_actions(behavior_name, action)
        self.env.step()

        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] > 0:
                print('win')
                reward = torch.tensor([1])
                self.wins = self.wins + 1
                self.score_board[agent_id] = self.score_board[agent_id] + 1
                sys.stdout.write("\r" + f"Agent A - Red: {self.score_board[0]} | Agent B - Blue: {self.score_board[1]}")
                sys.stdout.flush()
        if len(terminal_steps) > 0:
            done = torch.tensor([True])
        decision_steps, terminal_steps = self.env.get_steps(behavior_name)
        next_state = decision_steps.obs[0][0]
        s_p = torch.tensor(next_state)
        if len(terminal_steps.reward) > 0:
            if terminal_steps.reward[0] > 0:
                final_state = self.debug_channel.get_last_state()
                s_p = torch.tensor(final_state)
        print(f's {s}')
        print(f'action {a_tens}')
        print(f'sp {s_p}')
        return (s, a_tens, reward, s_p, done)

    def train(self, agent_id):
        print('train')
        if self.replay_buffer_resistry[agent_id].size() < self.config['batch_size']:
            return

        agent = self.agent_registry[agent_id]
        total_target = torch.tensor([0])
        total_q1 = torch.tensor([0])
        total_q2 = torch.tensor([0])
        total_actor_loss = torch.tensor([0]) 

        for epoch in range(1000): 
            with torch.no_grad():
                buffer = self.replay_buffer_resistry[agent_id]
                #states, actions, rewards, next_states, dones = buffer.sample(self.config['batch_size'])
                states, actions, rewards, next_states, dones = buffer.empty()
                states = torch.stack(states).squeeze()
                actions = torch.stack(actions).squeeze()
                rewards = torch.stack(rewards).squeeze() * 1000
                next_states = torch.stack(next_states)
                dones = torch.tensor(dones, dtype=torch.float32).squeeze()

                torch.save(states, 'tensors/states.pt')
                torch.save(actions, 'tensors/actions.pt')
                torch.save(rewards, 'tensors/rewards.pt')
                torch.save(next_states, 'tensors/next_states.pt')
                torch.save(dones, 'tensors/dones.pt')

                next_actions = agent.actor.forward(next_states)

                next_q1 = agent.target_critic1(next_states)
                next_q2 = agent.target_critic2(next_states)
                state_values = (
                    next_actions * (torch.min(next_q1, next_q2))
                ).sum(dim=1)
                target_q = rewards + (1 - dones) * self.config['gamma'] * state_values 

            idx = actions.argmax(dim=1)
            q1 = agent.critic1(states)#.gather(1, actions)
            q1 = torch.gather(q1, dim=1, index=idx.unsqueeze(-1)).squeeze(-1) 
            q2 = agent.critic2(states)#.gather(1, actions)
            q2 = torch.gather(q2, dim=1, index=idx.unsqueeze(-1)).squeeze(-1)

            total_q2 = total_q2 + q2
            total_q1 = total_q1 + q1
            total_target = total_target + target_q 
            if (epoch + 1) % 10 == 0:
                critic1_loss = F.mse_loss(total_q1, total_target)
                agent.critic1_optimizer.zero_grad()
                critic1_loss.backward(retain_graph=True)
                if self.config['wandb_log']:
                    wandb.log({"critic1_loss": critic1_loss})
                agent.critic1_optimizer.step()

                critic2_loss = F.mse_loss(total_q2, total_target)
                agent.critic2_optimizer.zero_grad()
                critic2_loss.backward(retain_graph=True)
                if self.config['wandb_log']:
                    wandb.log({"critic2_loss": critic2_loss})
                agent.critic2_optimizer.step()

                total_target = torch.tensor([0])
                total_q1 = torch.tensor([0])
                total_q2 = torch.tensor([0])

                tau = self.config['tau']
                for target_param, param in zip(agent.target_critic1.parameters(), agent.critic1.parameters()):
                    target_param.data.copy_(tau * param.data + (1 - tau) * target_param.data)

                for target_param, param in zip(agent.target_critic2.parameters(), agent.critic2.parameters()):
                    target_param.data.copy_(tau * param.data + (1 - tau) * target_param.data)

            new_actions = agent.actor.forward(states)
            min_q = torch.min(agent.critic1(states), agent.critic2(states))
            actor_loss = torch.tensor(-1) * (new_actions * min_q).sum(dim=1).mean() 
            total_actor_loss = total_actor_loss + actor_loss
            agent.actor_optimizer.zero_grad()

            if (epoch + 1) % 10 == 0:
                actor_loss.backward(retain_graph=True)
                if self.config['wandb_log']:
                    wandb.log({"actor_loss": actor_loss})
                agent.actor_optimizer.step()
                actor_loss = torch.tensor([0])


    def run(self):
        if self.config['wandb_log']:
            wandb.init(
                project="visibility-game",
            )
        for episode in range(self.config['training_steps']):
            #[self.replay_buffer_resistry[agent_id].add(self.transition_tuple(agent_id)) for agent_id in range(self.config['no_agents'])]
            self.replay_buffer_resistry[0].add(self.transition_tuple(0))
            if (episode + 1) % self.config['buffer_size'] == 0:
                self.train(0)
                #[self.train(agent_id) for agent_id in range(self.config['no_agents'])]
        self.env.close()