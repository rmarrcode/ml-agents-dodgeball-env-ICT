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

public class DodgeBallAgent_autoshoot_plus_test : DodgeBallAgent_Scout
{

    [Header("Extended  Extended Parameters")]
    public float autoshootDistance = 50;
    public bool discretized = false;
    public int move_input = 0;

    public override void Initialize()
    {
        //Time.timeScale = 10f;
        //SETUP STUNNED AS
        m_StunnedAudioSource = gameObject.AddComponent<AudioSource>();
        m_StunnedAudioSource.spatialBlend = 1;
        m_StunnedAudioSource.maxDistance = 250;

        //SETUP IMPACT AS
        m_BallImpactAudioSource = gameObject.AddComponent<AudioSource>();
        m_BallImpactAudioSource.spatialBlend = 1;
        m_BallImpactAudioSource.maxDistance = 250;

        var bufferSensors = GetComponentsInChildren<BufferSensorComponent>();
        m_OtherAgentsBuffer = bufferSensors[0];

        m_CubeMovement = GetComponent<AgentCubeMovement>();
        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();

        AgentRb = GetComponent<Rigidbody>();
        input = GetComponent<DodgeBallAgentInput>();
        m_GameController = GetComponentInParent<DodgeBallGameController>();

        //Make sure ThrowController is set up to play sounds
        ThrowController.PlaySound = m_GameController.ShouldPlayEffects;

        if (m_FirstInitialize)
        {
            m_StartingPos = transform.position;
            m_StartingRot = transform.rotation;
            //If we don't have a home base, just use the starting position.
            if (HomeBaseLocation is null)
            {
                m_HomeBasePosition = m_StartingPos;
                m_HomeDirection = transform.forward;
            }
            else
            {
                m_HomeBasePosition = HomeBaseLocation.position;
                m_HomeDirection = HomeBaseLocation.forward;
            }
            m_FirstInitialize = false;
            Flag.gameObject.SetActive(false);
        }
        m_EnvParameters = Academy.Instance.EnvironmentParameters;
        GetAllParameters();
    }
    public override void MoveAgent(ActionBuffers actionBuffers)
    {
        if (Stunned)
        {
            return;
        }
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;

        if (!discretized)
        {
            m_InputV = continuousActions[0];
            m_InputH = continuousActions[1];
            m_Rotate = continuousActions[2];
            m_ThrowInput = (int)discreteActions[0];
            m_DashInput = 0;
        }
        else
        {
            move_input = (int)discreteActions[0];
            m_DashInput = 0;
            m_ThrowInput = 1;
            if (move_input == 0)
            {
                m_InputV = 0;
                m_InputH = 0;
                if (ComputeAngle() != null)
                {
                    this.transform.LookAt(ComputeAngle().Agent.transform);
                }
            }
            else if (move_input == 1)
            {
                m_InputV = 1;
                m_InputH = 0;
                this.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
            else if (move_input == 2)
            {
                m_InputV = 1;
                m_InputH = 1;
                this.transform.rotation = Quaternion.Euler(0, 45, 0);
            }
            else if (move_input == 3)
            {
                m_InputV = 0;
                m_InputH = 1;
                this.transform.rotation = Quaternion.Euler(0,90,0);
            }
            else if (move_input == 4)
            {
                m_InputV = -1;
                m_InputH = 1;
                this.transform.rotation = Quaternion.Euler(0, 135, 0);
            }
            else if (move_input == 5)
            {
                m_InputV = -1;
                m_InputH = 0;
                this.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            else if (move_input == 6)
            {
                m_InputV = -1;
                m_InputH = -1;
                this.transform.rotation = Quaternion.Euler(0, 225, 0);
            }
            else if (move_input == 7)
            {
                m_InputV = 0;
                m_InputH = -1;
                this.transform.rotation = Quaternion.Euler(0, 270, 0);
            }
            else if (move_input == 8)
            {
                m_InputV = 1;
                m_InputH = -1;
                this.transform.rotation = Quaternion.Euler(0, 315, 0);
            }
        }

        //HANDLE ROTATION
        var moveDir = new Vector3(m_InputH, 0, m_InputV);

        //HANDLE XZ MOVEMENT
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

                float dot = Vector2.Dot(new Vector2(this.transform.forward.x, this.transform.forward.z), (opponentPosition - agentPosition).normalized);
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
        List<DodgeBallGameController.PlayerInfo> teamList;
        List<DodgeBallGameController.PlayerInfo> opponentsList;
        if (m_BehaviorParameters.TeamId == 0)
        {
            teamList = m_GameController.Team0Players;
            opponentsList = m_GameController.Team1Players;
        }
        else
        {
            teamList = m_GameController.Team1Players;
            opponentsList = m_GameController.Team0Players;
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


    protected override void FixedUpdate()
    {
        m_DashCoolDownReady = m_CubeMovement.dashCoolDownTimer > m_CubeMovement.dashCoolDownDuration;
        if (StepCount % 40 == 0)
        {
            m_IsDecisionStep = true;
            m_AgentStepCount++;
            this.RequestDecision();
        }
        else
        {
            this.RequestAction();
            //Debug.Log("Here");
        }
        // Handle if flag gets home
        if (Vector3.Distance(m_HomeBasePosition, transform.position) <= 3.0f && HasEnemyFlag)
        {
            m_GameController.FlagWasBroughtHome(this);
        }
    }

    protected override void OnCollisionEnter(Collision col)
    {
        // Ignore all collisions when stunned
        if (Stunned)
        {
            return;
        }
        DodgeBall db = col.gameObject.GetComponent<DodgeBall>();
        if (!db)
        {
            if (m_GameController.GameMode == DodgeBallGameController.GameModeType.CaptureTheFlag)
            {
                // Check if it is a flag
                if (col.gameObject.tag == "purpleFlag" && teamID == 0 || col.gameObject.tag == "blueFlag" && teamID == 1)
                {
                    m_GameController.FlagWasTaken(this);
                }
                else if (col.gameObject.tag == "purpleFlag" && teamID == 1 || col.gameObject.tag == "blueFlag" && teamID == 0)
                {
                    m_GameController.ReturnFlag(this);
                }
                DodgeBallAgent hitAgent = col.gameObject.GetComponent<DodgeBallAgent>();
                if (hitAgent && HasEnemyFlag && m_GameController.FlagCarrierKnockback)
                {
                    if (hitAgent.teamID != teamID && !hitAgent.Stunned)
                    {
                        if (m_GameController.ShouldPlayEffects && (m_GameController.FlagHitClip != null))
                        {
                            m_BallImpactAudioSource.PlayOneShot(m_GameController.FlagHitClip, 1f);
                        }
                        // Play Flag Whack
                        if (FlagAnimator != null)
                        {
                            FlagAnimator.SetTrigger("FlagSwing");
                        }
                        hitAgent.PlayHitFX();
                        var moveDirection = hitAgent.transform.position - transform.position;
                        hitAgent.AgentRb.AddForce(moveDirection * 150, ForceMode.Impulse);
                    }
                }
            }
            return;
        }

        if (db != null) { if (IS_DEBUG) Debug.Log("A) " + gameObject.name + " hit by " + col.gameObject.name + "; inPlay? " + db.inPlay + "; state=" + ((DodgeBall_Extended)db).ballState); }
        //if (db.inPlay) //HIT BY LIVE BALL
        if (((DodgeBall_Extended)db).ballState == DodgeBall_Extended.BallState.THROWABLE)
        {
            if (IS_DEBUG) Debug.Log("B) hit by: teamToIgnore=" + db.TeamToIgnore + "; teamID=" + m_BehaviorParameters.TeamId + "; thrownby teamID=" + db?.thrownBy?.teamID);
            //if (db.TeamToIgnore != -1 && db.TeamToIgnore != m_BehaviorParameters.TeamId) //HIT BY LIVE BALL
            if ((db?.thrownBy != null) && (db?.thrownBy?.teamID != m_BehaviorParameters.TeamId))
            {
                PlayHitFX();
                m_GameController.PlayerWasHit(this, db.thrownBy);
                if (IS_DEBUG) Debug.Log("C) " + gameObject + " hit by " + col.gameObject.name + " @ " + Time.realtimeSinceStartup);
                if (db.gameObject.GetComponent<DodgeBall_Extended>())
                {
                    if (db.gameObject.GetComponent<DodgeBall_Extended>().destroyOnImpact)
                        db.gameObject.GetComponent<DodgeBall_Extended>().RemoveDodgeball();
                    else
                        db.BallIsInPlay(false);

                }
                else
                {
                    db.BallIsInPlay(false);
                }
            }
        }
        else //TRY TO PICK IT UP
        {
            if (currentNumberOfBalls < maxAmmoCapacity)
            {
                PickUpBall(db);
            }
        }
    }

    public override void ResetAgent()
    {
        //base.ResetAgent();

        //Reset Position
        GetAllParameters();
        StopAllCoroutines();
        transform.position = m_StartingPos;
        AgentRb.constraints = RigidbodyConstraints.FreezeRotation;
        AgentRb.velocity = Vector3.zero;
        AgentRb.angularVelocity = Vector3.zero;
        transform.rotation = m_StartingRot;

        /*//Randomly rotate agent during training
        if (m_GameController.CurrentSceneType == DodgeBallGameController_WP.SceneType.Game || m_GameController.CurrentSceneType == DodgeBallGameController_WP.SceneType.Movie)
        {
            transform.rotation = m_StartingRot;
        }
        else //Training Mode 
        {
            transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));
        }*/

        //Reset dodgeballs 
        if (!useInfiniteAmmo)
            ActiveBallsQueue.Clear();
        currentNumberOfBalls = 0;
        if (!useInfiniteAmmo)
            SetActiveBalls(0);

        //Reset parameters 
        NormalEyes.gameObject.SetActive(true);
        HitEyes.gameObject.SetActive(false);
        HasEnemyFlag = false;
        Stunned = false;
        AgentRb.drag = 4;
        AgentRb.angularDrag = 1;
        Dancing = false;

        //Setup infinite projectiles
        if (useInfiniteAmmo)
        {
            base.SetActiveBalls(maxAmmoCapacity);
            PickUpBall(currentlyHeldBalls[0]);
            currentNumberOfBalls = maxAmmoCapacity;
        }
        else
        {
            foreach (DodgeBall d in currentlyHeldBalls)
            {
                Destroy(d.gameObject);
            }
            currentlyHeldBalls.Clear();
        }
        SetAmmoCapacity(maxAmmoCapacity);
    }

}
