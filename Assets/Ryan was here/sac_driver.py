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
        
        return (s, a_tens, reward, s_p, done)

    def train(self, agent_id):
        
        if self.replay_buffer_resistry[agent_id].size() < self.config['batch_size']:
            return

        # Not worrying about ENTROPY for now
        with torch.no_grad():
            agent = self.agent_registry[agent_id]
            buffer = self.replay_buffer_resistry[agent_id]

            states, actions, rewards, next_states, dones = buffer.sample(self.config['batch_size'])
            
            #states = torch.cat((torch.load('states.pt'), torch.tensor([6.5000, 0.5000, 0.5000]).repeat(256, 1)), dim=0)
            #actions = torch.cat((torch.load('actions.pt'), torch.tensor(1.).repeat(256)))
            #rewards = torch.cat((torch.load('rewards.pt'), torch.tensor(1.).repeat(256)))
            #next_states = torch.cat((torch.load('next_states.pt'), torch.tensor([7.5000, 0.5000, 0.5000]).repeat(256, 1)), dim=0)
            #dones = torch.cat((torch.load('dones.pt'), torch.tensor(1.).repeat(256)))

            # why does squeeze not work
            states = torch.stack(states).squeeze()
            actions = torch.stack(actions).squeeze()
            rewards = torch.stack(rewards).squeeze()
            next_states = torch.stack(next_states)
            dones = torch.tensor(dones, dtype=torch.float32).squeeze()

            root = 'tensors'
            torch.save(states, f'{root}/states.pt')
            torch.save(actions, f'{root}/actions.pt')
            torch.save(rewards, f'{root}/rewards.pt')
            torch.save(next_states, f'{root}/next_states.pt')
            torch.save(dones, f'{root}/dones.pt')
            #pandas_data = {
                #'rewards': rewards.tolist(),
                #'states': states.tolist(),
                #'actions': actions.tolist(),
                #'next_states': next_states.tolist(),
                #'dones': dones.tolist(),
            #}
            #df = pd.DataFrame(pandas_data)
            #print('buffer buffer ')
            #df.to_csv('buffer_data.csv', index=False)

            # new vals
            next_actions = agent.actor.select_action_tanh_batch(next_states)
            next_q1 = agent.target_critic1(next_states, next_actions)
            next_q2 = agent.target_critic2(next_states, next_actions)
            #next_q = (torch.min(next_q1, next_q2) - self.config['alpha'] * next_log_probs).squeeze()
            next_q = torch.min(next_q1, next_q2).squeeze()
            print(f'rewards {rewards}')
            print(f'dones {dones}')
            print(f'next_q {next_q}')
            target_q = rewards + (1 - dones) * self.config['gamma'] * next_q 
           
        q1 = agent.critic1(states, actions.unsqueeze(1)).squeeze()
        q2 = agent.critic2(states, actions.unsqueeze(1)).squeeze()

        pandas_data = {
            'target': target_q.tolist(),
            'states': states.tolist(),
            'actions': actions.tolist(),
            'q1': q1.tolist(),
            'q2': q1.tolist(),
        }
        df = pd.DataFrame(pandas_data)
        df.to_csv('debug.csv', index=False)
        df[df['target'] == 1].to_csv('debug.csv', index=False)
        q1_close_loss = F.mse_loss(torch.tensor(df[df['target'] == 1]['q1'].to_numpy()), torch.tensor(df[df['target'] == 1]['target'].to_numpy())) 
        q2_close_loss = F.mse_loss(torch.tensor(df[df['target'] == 1]['q2'].to_numpy()), torch.tensor(df[df['target'] == 1]['target'].to_numpy()))  
        if self.config['wandb_log']:
            wandb.log({f"q1 close loss": q1_close_loss})
            wandb.log({f"q2 close loss": q2_close_loss})

        # potentially dont work with average and backprop on smaller batches
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

        new_actions = agent.actor.select_action_tanh_batch(states)
        q1_new = agent.critic1(states, new_actions)
        q2_new = agent.critic2(states, new_actions)
        q_new = torch.min(q1_new, q2_new)

        #actor_loss = (self.config['alpha'] * log_probs - q_new).mean()
        actor_loss = torch.tensor(-1) * (q_new).mean()
        
        agent.actor_optimizer.zero_grad()
        actor_loss.backward(retain_graph=True)
        if self.config['wandb_log']:
            wandb.log({f"actor_loss_{agent_id}": actor_loss})
        agent.actor_optimizer.step()
        
        if self.config['wandb_log']:
            wandb.log({'wins': self.wins})
            self.wins = 0

        #if self.config['wandb_log']:
            #for name,param in agent.named_parameters():
                #wandb.log({name: param.data.mean().item()})

        tau = self.config['tau']
        for target_param, param in zip(agent.target_critic1.parameters(), agent.critic1.parameters()):
            target_param.data.copy_(tau * param.data + (1 - tau) * target_param.data)

        for target_param, param in zip(agent.target_critic2.parameters(), agent.critic2.parameters()):
            target_param.data.copy_(tau * param.data + (1 - tau) * target_param.data)


    def run(self):
        if self.config['wandb_log']:
            wandb.init(
                project="visibility-game",
            )
        for episode in range(self.config['training_steps']):
            #[self.replay_buffer_resistry[agent_id].add(self.transition_tuple(agent_id)) for agent_id in range(self.config['no_agents'])]
            self.replay_buffer_resistry[0].add(self.transition_tuple(0))
            if (episode + 1) % self.config['buffer_size'] == 0:
                pass
                self.train(0)
                #[self.train(agent_id) for agent_id in range(self.config['no_agents'])]
        self.env.close()