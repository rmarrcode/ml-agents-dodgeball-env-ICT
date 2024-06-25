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

//Adds automatic targetting functionality and optimizes observation space
public class DodgeBallAgent_autoshoot_plus_obs : DodgeBallAgent_Scout
{
    [Header("Extended  Extended Parameters")]
    public float autoshootDistance = 50;
    public bool razish = false;

    public override void MoveAgent(ActionBuffers actionBuffers)
    {
        //If stunned do nothing
        if (Stunned)
        {
            return;
        }

        //Fetch Actions
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;

        m_InputV = continuousActions[0];
        m_InputH = continuousActions[1];
        m_Rotate = continuousActions[2];
        m_ThrowInput = (int)discreteActions[0];
        m_DashInput = 0; //Remove dash for consistency with waypoint version

        //HANDLE ROTATION
        m_CubeMovement.Look(m_Rotate);

        //HANDLE XZ MOVEMENT
        var moveDir = transform.TransformDirection(new Vector3(m_InputH, 0, m_InputV));
        m_CubeMovement.RunOnGround(moveDir);

        //perform discrete actions only once between decisions
        if (m_IsDecisionStep)
        {
            m_IsDecisionStep = false;
            //HANDLE THROWING
            if (m_ThrowInput > 0)
            {
                ThrowTheBall(ComputeAngle());
            }
            //HANDLE DASH MOVEMENT
            if (m_DashInput > 0 && m_DashCoolDownReady)
            {
                m_CubeMovement.Dash(moveDir);
            }
        }
    }

    //Calculate projectile direction
    public DodgeBallGameController.PlayerInfo ComputeAngle()
    {
        // Fetch Opponent List
        List<DodgeBallGameController.PlayerInfo> opponentsList;
        DodgeBallGameController.PlayerInfo target = null;
        if (m_BehaviorParameters.TeamId == 0)
        {
            opponentsList = m_GameController.Team1Players;
        }
        else
        {
            opponentsList = m_GameController.Team0Players;
        }

        //Find the opponent agent is closest to facing (only in y and z)
        float max = -1;
        foreach (var info in opponentsList)
        {
            if (info.Agent.gameObject.activeInHierarchy && Vector3.Distance(this.transform.position, info.Agent.gameObject.transform.position) < autoshootDistance)
            {
                //Ignore Y
                Vector2 opponentPosition = new Vector2(info.Agent.gameObject.transform.position.x, info.Agent.gameObject.transform.position.z);
                if (IS_DEBUG) Debug.Log("opponentPosition: " + opponentPosition);
                Vector2 agentPosition = new Vector2(this.ThrowController.projectileOrigin.position.x, this.ThrowController.projectileOrigin.position.z);
                if (IS_DEBUG) Debug.Log("agentPosition: " + agentPosition);

                float dot = Vector2.Dot(new Vector2(this.transform.forward.x, this.transform.forward.z), (opponentPosition - agentPosition));
                if (IS_DEBUG) Debug.Log("dot " + dot);

                if (dot > max)
                {
                    max = dot;
                    target = info;
                }
            }
        }

        return target;
    }


    public void ThrowTheBall(DodgeBallGameController.PlayerInfo target)
    {
        if ((currentNumberOfBalls > 0) && !ThrowController.coolDownWait)
        {
            if (IS_DEBUG) Debug.Log("A) " + gameObject.name + " throws the ball! #b=" + currentNumberOfBalls);
            var db = ActiveBallsQueue.Peek();
            if (db != null && db.GetComponent<Rigidbody>() != null)
            {
                //((DodgeBall_Extended)db).SetActive(true);
                //Debug.Log("Here2");
                ThrowController.Throw(db, this, target, m_BehaviorParameters.TeamId);
                StartCoroutine(_ActivateAfterThrow());
                IEnumerator _ActivateAfterThrow()
                {
                    yield return new WaitForEndOfFrame();
                    ((DodgeBall_Extended)db).SetThrowable(true);
                }
            }
            else
            {
                if (IS_DEBUG) Debug.Log("Tried throwing null ball");
            }
            if (!useInfiniteAmmo)
            {
                ActiveBallsQueue.Dequeue();
                currentNumberOfBalls--;
            }
            SetActiveBalls(currentNumberOfBalls);
            if (IS_DEBUG) Debug.Log("B) " + gameObject.name + " throws the ball! #b=" + currentNumberOfBalls);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //Make observations
        if (UseVectorObs)
        {
            sensor.AddObservation(ThrowController.coolDownWait); 
            sensor.AddObservation((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit); //Remaining Hit Points Normalized
            sensor.AddObservation(Vector3.Dot(AgentRb.velocity, AgentRb.transform.forward));
            sensor.AddObservation(Vector3.Dot(AgentRb.velocity, AgentRb.transform.right));
            sensor.AddObservation(transform.InverseTransformDirection(m_HomeDirection));
            sensor.AddObservation(GetRelativeCoordinates(m_HomeBasePosition));// Location to base (usually spawn point)
            sensor.AddObservation(this.transform.rotation.y);
        }
        if (IS_DEBUG) Debug.Log("A) CollectObservations=" + sensor.ObservationSize() + "; spec=" + sensor.GetObservationSpec().Shape + "; HP Obs=" + ((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit) + "; ball One Hot=" + string.Join(",", ballOneHot) + "; relative coords=" + string.Join(",", GetRelativeCoordinates(m_HomeBasePosition)));

        //Fetch team lists
        List<DodgeBallGameController.PlayerInfo> teamList;
        List<DodgeBallGameController.PlayerInfo> opponentsList;
        if (m_BehaviorParameters.TeamId == 0)
        {
            teamList = m_GameController.Team0Players;
            opponentsList = m_GameController.Team1Players;
            /*if (razish)
            {
                AddReward(1 / (this.transform.position - m_HomeBasePosition).magnitude);
            }*/
        }
        else
        {
            teamList = m_GameController.Team1Players;
            opponentsList = m_GameController.Team0Players;
            /*if (razish)
            {
                AddReward(1 / (this.transform.position - m_GameController.Team0Base.transform.position).magnitude);
            }*/
        }

        //Add observations with team info
        foreach (var info in teamList)
        {
            if (info.Agent != this && info.Agent.gameObject.activeInHierarchy)
            {
                m_OtherAgentsBuffer.AppendObservation(GetOtherAgentData(info));
            }
        }

        //Count number of enemies remaining 
        int numEnemiesRemaining = 0;
        bool enemyHasFlag = false;
        foreach (var info in opponentsList)
        {
            if (info.Agent.gameObject.activeInHierarchy)
            {
                numEnemiesRemaining++;
            }
        }

        //Different observation for different mode. Enemy Has Flag is only relevant to CTF
        if (m_GameController.GameMode == DodgeBallGameController.GameModeType.CaptureTheFlag)
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

    protected override float[] GetOtherAgentData(DodgeBallGameController.PlayerInfo info)
    {
        var otherAgentdata = new float[6];
        otherAgentdata[0] = (float)info.Agent.HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit;
        var relativePosition = transform.InverseTransformPoint(info.Agent.transform.position);
        otherAgentdata[1] = relativePosition.x / m_LocationNormalizationFactor;
        otherAgentdata[2] = relativePosition.z / m_LocationNormalizationFactor;
        otherAgentdata[3] = info.TeamID == teamID ? 0.0f : 1.0f;
        //otherAgentdata[4] = info.Agent.HasEnemyFlag ? 1.0f : 0.0f;
        //otherAgentdata[5] = info.Agent.Stunned ? 1.0f : 0.0f;
        var relativeVelocity = transform.InverseTransformDirection(info.Agent.AgentRb.velocity);
        otherAgentdata[4] = relativeVelocity.x / 30.0f;
        otherAgentdata[5] = relativeVelocity.z / 30.0f;
        return otherAgentdata;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }
        var contActionsOut = actionsOut.ContinuousActions;
        contActionsOut[0] = Random.value;
        contActionsOut[1] = Random.value;
        contActionsOut[2] = Random.value;
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 1;
    }
}
