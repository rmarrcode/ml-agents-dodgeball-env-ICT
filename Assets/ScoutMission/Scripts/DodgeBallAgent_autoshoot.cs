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

public class DodgeBallAgent_autoshoot : DodgeBallAgent_Scout
{

    public override void MoveAgent(ActionBuffers actionBuffers)
    {
        if (Stunned)
        {
            return;
        }
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;

        m_InputV = continuousActions[0];
        m_InputH = continuousActions[1];
        m_Rotate = continuousActions[2];
        m_ThrowInput = (int)discreteActions[0];
        m_DashInput = (int)discreteActions[1];

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

    public float ComputeAngle()
    {
        // Fetch Opponent List
        List<DodgeBallGameController.PlayerInfo> opponentsList;
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
        float angle = 0;
        foreach(var info in opponentsList)
        {
            if (info.Agent.gameObject.activeInHierarchy)
            {
                //Ignore Y
                Vector2 opponentPosition = new Vector2(info.Agent.gameObject.transform.position.x, info.Agent.gameObject.transform.position.z);
                if (IS_DEBUG) Debug.Log("opponentPosition: " +  opponentPosition);
                Vector2 agentPosition = new Vector2(this.ThrowController.projectileOrigin.position.x, this.ThrowController.projectileOrigin.position.z);
                if (IS_DEBUG) Debug.Log("agentPosition: " + agentPosition);

                float dot = Vector2.Dot(new Vector2(this.transform.forward.x, this.transform.forward.z), (opponentPosition - agentPosition).normalized);
                if (IS_DEBUG) Debug.Log("dot " + dot);
                
                if (dot > max)
                {
                    max = dot;

                    //Calculate x angle
                    opponentPosition = new Vector2(info.Agent.gameObject.transform.position.y, info.Agent.gameObject.transform.position.z);
                    agentPosition = new Vector2(this.ThrowController.projectileOrigin.position.y, this.ThrowController.projectileOrigin.position.z);

                    angle = Vector2.SignedAngle(new Vector2(this.transform.forward.y, this.transform.forward.z), (opponentPosition - agentPosition));
                    
                    //Correct for always negative angle
                    if (opponentPosition.x > agentPosition.x)
                    {
                        angle = -angle;
                    }
                    if (IS_DEBUG) Debug.Log("angle2: " + angle);
                }
            }
        }
        
        if (angle > 45 || angle < -45)
        {
            angle = 0;
        }
        if (IS_DEBUG) Debug.Log("returned angle: " + angle);
        return angle / 90.0f + 0.5f;
    }


    public void ThrowTheBall(float input)
    {
        if ((currentNumberOfBalls > 0) && !ThrowController.coolDownWait)
        {
            if (IS_DEBUG) Debug.Log("A) " + gameObject.name + " throws the ball! #b=" + currentNumberOfBalls);
            var db = ActiveBallsQueue.Peek();
            if (db != null && db.GetComponent<Rigidbody>() != null)
            {
                //((DodgeBall_Extended)db).SetActive(true);
                ThrowController.Throw(db, this, m_BehaviorParameters.TeamId, input);
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
}
