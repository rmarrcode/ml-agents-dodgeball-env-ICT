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

//This class only calls decisions manually when the agent arrives at a waypoint 
public class DodgeBallAgent_autoshoot_plus_WPM_obs : DodgeBallAgent_autoshoot_plus_WP
{
    public bool noStand = false;
    public int lastInput = 0;
    public bool standing = false;
    public int standCounter = 0;

    public override void CollectObservations(VectorSensor sensor)
    {
        //Add observations
        if (UseVectorObs)
        {
            sensor.AddObservation(ThrowController.coolDownWait); 
            sensor.AddObservation((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit); //Remaining Hit Points Normalized
            sensor.AddObservation(Vector3.Dot(AgentRb.velocity, AgentRb.transform.forward));
            sensor.AddObservation(Vector3.Dot(AgentRb.velocity, AgentRb.transform.right));
            sensor.AddObservation(transform.InverseTransformDirection(m_HomeDirection));
            sensor.AddObservation(GetRelativeCoordinates(m_HomeBasePosition)); // Location to base (usually spawn point)
            sensor.AddObservation(this.transform.rotation.y);
        }
        if (IS_DEBUG) Debug.Log("A) CollectObservations=" + sensor.ObservationSize() + "; spec=" + sensor.GetObservationSpec().Shape + "; HP Obs=" + ((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit) + "; ball One Hot=" + string.Join(",", ballOneHot) + "; relative coords=" + string.Join(",", GetRelativeCoordinates(m_HomeBasePosition)));

        //Fetch team lists
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

        //Add teammate's info to observation 
        foreach (var info in teamList)
        {
            if (info.Agent != this && info.Agent.gameObject.activeInHierarchy)
            {
                m_OtherAgentsBuffer.AppendObservation(GetOtherAgentData(info));
            }
        }

        //Count remaining enemies
        int numEnemiesRemaining = 0;
        bool enemyHasFlag = false;
        foreach (var info in opponentsList)
        {
            if (info.Agent.gameObject.activeInHierarchy)
            {
                numEnemiesRemaining++;
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
            //Mask unavailable waypoints
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
        /*else Decisions are no longer requested between waypoints so no need to mask
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

        //Fetch actions
        var discreteActions = actionBuffers.DiscreteActions;
        m_ThrowInput = 1;

        if (!noStand) //noStand removes option to stay in place
        {
            //New input
            if (!moving && !standing)
            {
                m_moveInput = (int)discreteActions[0];
                lastInput = m_moveInput;
                ready = true;
                if (m_moveInput == 0)
                {
                    standing = true;
                }
            }
            else 
            {
                m_moveInput = lastInput;
            }
        }
        /*else
        {
            if (StepCount % 40 == 0)
            {
                m_moveInput = (int)discreteActions[0] + 1;
                lastInput = m_moveInput;
                ready = true;
            }
            else if (ready)
            {
                m_moveInput = lastInput;
            }
            else
            {
                m_moveInput = 0;
            }
        }*/

        //Set target position
        if (!moving)
        {
            int index = m_moveInput;
            if (index == 0 || currentWaypoint.neighbors[index - 1] == null)
            {
                currentTarget = currentWaypoint;
                moving = false;
                lastInput = 0;
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
        
        if (moving) //Continue in same direction
        {
            moveDir = (targetPosition - thisPosition).normalized;
        }

        //Move 
        m_CubeMovement.RunOnGround(moveDir);
        
        
        //HANDLE ROTATION
        if (m_moveInput == 0) //Automatically face closest opponent if agent stays in place
        {
            DodgeBallGameController_WP.PlayerInfo target = ComputeAngle();
            if (target != null)
            {
                this.transform.LookAt(target.Agent.transform.position);
            }
        }
        else //Face direction agent is moving 
        {
            this.transform.LookAt(targetPosition);
        }

        //If agent arrived at target position
        if (moving && (this.transform.position - targetPosition).magnitude < 0.5f)
        {
            moving = false;
            ready = false;
            m_moveInput = 0;
            lastInput = 0;
            this.transform.position = targetPosition;
            moveDir = new Vector3(0, 0, 0);
            currentWaypoint.taken = false;
            currentWaypoint = currentTarget;
            currentWaypoint.taken = true;
            this.GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
        }

        //Throw ball
        ThrowTheBall(ComputeAngle());
    }

    protected override void FixedUpdate()
    {
        //m_DashCoolDownReady = m_CubeMovement.dashCoolDownTimer > m_CubeMovement.dashCoolDownDuration;
        
        //Request decision at new waypoint or after stand cooldown 
        if (!moving && !standing)
        {
            m_IsDecisionStep = true;
            this.RequestDecision();
            m_AgentStepCount++;
        }
        else if (moving) //Continue in same direction while between waypoints
        {
            this.RequestAction();
        }
        else if (standCounter >= 40) //Pause for 40 fixed updates when stay in place is selected
        {
            standing = false;
            standCounter = 0;
        }
        else
        {
            standCounter++;
        }
    }
}
