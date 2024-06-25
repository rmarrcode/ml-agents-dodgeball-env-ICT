using System;
using System.Collections;
using System.Collections.Generic;
using MLAgents;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using Random = UnityEngine.Random;

public class DodgeBallAgent_autoshoot_plus_WP_obs : DodgeBallAgent_autoshoot_plus_WP
{
    public override void CollectObservations(VectorSensor sensor)
    {
        //AddReward(m_BallHoldBonus * (float)currentNumberOfBalls);
        if (UseVectorObs)
        {
            sensor.AddObservation(ThrowController.coolDownWait); //Held DBs Normalized
            //sensor.AddObservation(Stunned);
            //Array.Clear(ballOneHot, 0, 5);
            //ballOneHot[currentNumberOfBalls] = 1f;
            //sensor.AddObservation(ballOneHot); //Held DBs Normalized
            sensor.AddObservation((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit); //Remaining Hit Points Normalized

            sensor.AddObservation(Vector3.Dot(AgentRb.velocity, AgentRb.transform.forward));
            sensor.AddObservation(Vector3.Dot(AgentRb.velocity, AgentRb.transform.right));
            sensor.AddObservation(transform.InverseTransformDirection(m_HomeDirection));
            //sensor.AddObservation(m_DashCoolDownReady);  // Remaining cooldown, capped at 1
            // Location to base
            sensor.AddObservation(GetRelativeCoordinates(m_HomeBasePosition));
            sensor.AddObservation(this.transform.rotation.y);
            //sensor.AddObservation(HasEnemyFlag);
        }
        if (IS_DEBUG) Debug.Log("A) CollectObservations=" + sensor.ObservationSize() + "; spec=" + sensor.GetObservationSpec().Shape + "; HP Obs=" + ((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit) + "; ball One Hot=" + string.Join(",", ballOneHot) + "; relative coords=" + string.Join(",", GetRelativeCoordinates(m_HomeBasePosition)));

        List<DodgeBallGameController_WP.PlayerInfo> teamList;
        List<DodgeBallGameController_WP.PlayerInfo> opponentsList;
        if (m_BehaviorParameters.TeamId == 0)
        {
            teamList = GameController.Team0Players;
            opponentsList = GameController.Team1Players;
        }
        else
        {
            teamList = GameController.Team1Players;
            opponentsList = GameController.Team0Players;
        }

        foreach (var info in teamList)
        {
            if (info.Agent != this && info.Agent.gameObject.activeInHierarchy)
            {
                m_OtherAgentsBuffer.AppendObservation(GetOtherAgentData(info));
            }
            if (info.Agent.HasEnemyFlag) // If anyone on my team has the enemy flag
            {
                AddReward(m_TeamHasFlagBonus);
            }
        }
        //Only opponents who picked up the flag are visible
        //var currentFlagPosition = TeamFlag.transform.position;
        int numEnemiesRemaining = 0;
        bool enemyHasFlag = false;
        foreach (var info in opponentsList)
        {
            if (info.Agent.gameObject.activeInHierarchy)
            {
                numEnemiesRemaining++;
            }
            if (info.Agent.HasEnemyFlag)
            {
                enemyHasFlag = true;
                //currentFlagPosition = info.Agent.transform.position;
                //AddReward(m_OpponentHasFlagPenalty); // If anyone on the opposing team has a flag
            }
        }
        var portionOfEnemiesRemaining = (float)numEnemiesRemaining / (float)opponentsList.Count;

        //Different observation for different mode. Enemy Has Flag is only relevant to CTF
        if (GameController.GameMode == DodgeBallGameController_WP.GameModeType.CaptureTheFlag)
        {
            sensor.AddObservation(enemyHasFlag);
        }
        else
        {
            sensor.AddObservation(numEnemiesRemaining);
        }

        //Location to flag
        //sensor.AddObservation(GetRelativeCoordinates(currentFlagPosition));
        if (IS_DEBUG) Debug.Log(StepCount + " B) CollectObservations=" + sensor.ObservationSize() + "; spec=" + sensor.GetObservationSpec().Shape + "; obs=" + string.Join(",", GetObservations()) + "; all obs=" + string.Join(",", GetObservations()));
        if (IS_DEBUG) Debug.Log(gameObject.name + "\tObs=\t" + string.Join(",", GetObservations()));

    }
}