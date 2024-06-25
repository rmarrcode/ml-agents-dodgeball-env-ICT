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

public class DodgeBallAgent_autoshoot_plus_WP2_obs : DodgeBallAgent_autoshoot_plus_WP
{
    public bool noStand = false;
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

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!moving)
        {
            for (int i = 0; i < 8; i++)
            {
                if (currentWaypoint.neighbors[i] != null && !currentWaypoint.neighbors[i].taken)
                {
                    if (!noStand)
                    {
                        actionMask.SetActionEnabled(0, i + 1, true);
                    }
                    else
                    {
                        actionMask.SetActionEnabled(0, i, true);
                    }
                }
                else
                {
                    if (!noStand)
                    {
                        actionMask.SetActionEnabled(0, i + 1, false);
                    }
                    else
                    {
                        actionMask.SetActionEnabled(0, i, false);
                    }
                }
            }
        }
        /*else
        {
            actionMask.SetActionEnabled(1, 0, false);
            actionMask.SetActionEnabled(1, 1, false);
            actionMask.SetActionEnabled(1, 2, false);
            actionMask.SetActionEnabled(1, 3, false);
            actionMask.SetActionEnabled(1, 4, false);
            actionMask.SetActionEnabled(1, 5, false);
            actionMask.SetActionEnabled(1, 6, false);
            actionMask.SetActionEnabled(1, 7, false);
            actionMask.SetActionEnabled(1, 8, false);

            actionMask.SetActionEnabled(1, m_moveInput, true);
        }*/
    }

    public override void MoveAgent(ActionBuffers actionBuffers)
    {
        Vector3 targetPosition;
        Vector3 thisPosition;

        if (Stunned)
        {
            return;
        }
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;
        m_ThrowInput = 1;
        if (!noStand)
        {
            m_moveInput = (int)discreteActions[0];
        }
        else
        {
            m_moveInput = (int)discreteActions[0] + 1;
        }

        if (!moving)
        {
            int index = m_moveInput;
            if (index == 0 || currentWaypoint.neighbors[index - 1] == null)
            {
                currentTarget = currentWaypoint;
                moving = false;
                currentTarget.taken = true;
                moveDir.x = 0;
                moveDir.y = 0;
                moveDir.z = 0;
            }
            else
            {
                currentTarget = currentWaypoint.neighbors[index - 1];
                moving = true;
                currentTarget.taken = true;
                currentWaypoint.taken = false;
            }
        }
        targetPosition = currentTarget.transform.position;
        targetPosition.y = 0;
        thisPosition = currentWaypoint.transform.position;
        thisPosition.y = 0;
        moveDir = (targetPosition - thisPosition).normalized;
        moveDir.y = 0;
        if (currentTarget == currentWaypoint)
        {
            moveDir = new Vector3(0, 0, 0);
        }
        targetPosition = currentTarget.transform.position;
        targetPosition.y += 0.5f;
        thisPosition = this.transform.position;

        if (moving)
        {
            moveDir = (targetPosition - thisPosition).normalized;
        }

        m_CubeMovement.RunOnGround(moveDir);
        //HANDLE ROTATION

        if (m_moveInput == 0)
        {
            DodgeBallGameController_WP.PlayerInfo target = ComputeAngle();
            if (target != null)
            {
                this.transform.LookAt(target.Agent.transform.position);
            }
        }
        else
        {
            this.transform.LookAt(targetPosition);
        }

        if (moving && (this.transform.position - targetPosition).magnitude < 0.5f)
        {
            moving = false;
            this.transform.position = targetPosition;
            moveDir = new Vector3(0, 0, 0);
            currentWaypoint.taken = false;
            currentWaypoint = currentTarget;
            currentWaypoint.taken = true;
            this.GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
            Debug.Log(StepCount);
        }
        ThrowTheBall(ComputeAngle());
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }

        var discreteActionsOut = actionsOut.DiscreteActions;
        if (!noStand)
        {
            discreteActionsOut[0] = 4;
        }
        else
        {
            discreteActionsOut[0] = 4;
        }
    }

    /*protected virtual void FixedUpdate()
    {
        m_DashCoolDownReady = m_CubeMovement.dashCoolDownTimer > m_CubeMovement.dashCoolDownDuration;
        if (StepCount % 40 == 0)
        {
            m_IsDecisionStep = true;
            m_AgentStepCount++;
        }
        // Handle if flag gets home
        if (Vector3.Distance(m_HomeBasePosition, transform.position) <= 3.0f && HasEnemyFlag)
        {
            m_GameController.FlagWasBroughtHome(this);
        }
    }*/
}
