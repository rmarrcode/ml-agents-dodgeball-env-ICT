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

public class DodgeBallAgent_autoshoot_plus_WP : DodgeBallAgent_Scout
{
    private int index;
    public DodgeBallGameController_WP GameController;

    [Header("Extended  Extended Parameters")]
    public GameObject waypoints;
    public float autoshootDistance = 50;
    public WaypointView initialWaypoint;
    public int m_moveInput;
    public bool moving = false;
    public bool ready = true;
    public bool go = true;
    public WaypointView currentTarget;
    public WaypointView currentWaypoint;
    public Vector3 moveDir = new Vector3(0, 0, 0);

    public override void MoveAgent(ActionBuffers actionBuffers)
    {
        Vector3 targetPosition;
        Vector3 thisPosition;

        if (Stunned)
        {
            return;
        }

        //Receive actions
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;
        m_Rotate = continuousActions[0];
        m_ThrowInput = (int)discreteActions[0];
        m_moveInput = (int)discreteActions[1];

        //HANDLE ROTATION
        m_CubeMovement.Look(m_Rotate);

        //Check which team
        List<DodgeBallGameController_WP.PlayerInfo> teamList;
        if (m_BehaviorParameters.TeamId == 0)
        {
            teamList = GameController.Team0Players;
        }
        else
        {
            teamList = GameController.Team1Players;
        }

        //Only picks next waypoint at decision steps
        if (m_IsDecisionStep)
        {

            //Only move when all agents are ready 
            ready = true;
            foreach (var info in teamList)
            {
                if (info.Agent.gameObject.activeInHierarchy && info.Agent.gameObject.GetComponent<DodgeBallAgent_autoshoot_plus_WP>().go)
                {
                    go = true;
                    break;
                }

                if (info.Agent.gameObject.activeInHierarchy && info.Agent.gameObject.GetComponent<DodgeBallAgent_autoshoot_plus_WP>().moving)
                {
                    ready = false;
                }
            }

            //Move to next waypoint
            if (ready || go)
            {
                index = m_moveInput;

                //If stay in place
                if (index == 0 || currentWaypoint.neighbors[index - 1] == null)
                {
                    currentTarget = currentWaypoint;
                    moving = false;
                    ready = true;
                    moveDir.x = 0;
                    moveDir.y = 0;
                    moveDir.z = 0;
                }
                else //Move
                {
                    currentTarget = currentWaypoint.neighbors[index - 1];
                    moving = true;
                    go = true;
                    ready = false;

                    //Check if new waypoint is taken by teammate
                    foreach (var info in teamList)
                    {
                        if (info.Agent.gameObject.activeInHierarchy && info.Agent != this && info.Agent.GetComponent<DodgeBallAgent_autoshoot_plus_WP>().currentTarget == currentTarget)
                        {
                            currentTarget = currentWaypoint;
                            moving = false;
                            ready = true;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            go = false;
        }

        //Move to new target position
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

        //Arrived at target postion
        if (moving && (this.transform.position - targetPosition).magnitude < 0.5f)
        {
            moving = false;
            ready = true;
            this.transform.position = targetPosition;
            moveDir = new Vector3(0, 0, 0);
            currentWaypoint = currentTarget;
            this.GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
        }

        //Perform discrete actions only once between decisions
        if (m_IsDecisionStep)
        {
            m_IsDecisionStep = false;
            //HANDLE THROWING
            if (m_ThrowInput > 0)
            {
                ThrowTheBall(ComputeAngle());
            }

                /*/HANDLE DASH MOVEMENT
                if (m_DashInput > 0 && m_DashCoolDownReady)
                {
                    m_CubeMovement.Dash(moveDir);
                }*/
        }
    }


    public DodgeBallGameController_WP.PlayerInfo ComputeAngle() //Calculate direction to shoot projectile towards opponent 
    {
        // Fetch Opponent List
        List<DodgeBallGameController_WP.PlayerInfo> opponentsList;
        DodgeBallGameController_WP.PlayerInfo target = null;
        if (m_BehaviorParameters.TeamId == 0)
        {
            opponentsList = GameController.Team1Players;
        }
        else
        {
            opponentsList = GameController.Team0Players;
        }

        //Find the opponent closest to directly ahead (only in y and z)
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

    public void ThrowTheBall(DodgeBallGameController_WP.PlayerInfo target)
    {
        if ((currentNumberOfBalls > 0) && !ThrowController.coolDownWait)
        {
            if (IS_DEBUG) Debug.Log("A) " + gameObject.name + " throws the ball! #b=" + currentNumberOfBalls);
            var db = ActiveBallsQueue.Peek();
            if (db != null && db.GetComponent<Rigidbody>() != null)
            {
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

    public override void Initialize()
    {
        currentWaypoint = initialWaypoint;
        currentTarget = initialWaypoint;
        Time.timeScale = 1f;

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
        GameController = GetComponentInParent<DodgeBallGameController_WP>();

        //Make sure ThrowController is set up to play sounds
        ThrowController.PlaySound = GameController.ShouldPlayEffects;

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

    /*public override void Reward(float r)
    {
        if (ready)
        {
            AddReward(reward);
            reward = 0;
        }
        else
        {
            reward += r;
        }
    }*/

    public override void CollectObservations(VectorSensor sensor) //Removes unnecessary observations
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

            //sensor.AddObservation(HasEnemyFlag);
        }
        if (IS_DEBUG) Debug.Log("A) CollectObservations=" + sensor.ObservationSize() + "; spec=" + sensor.GetObservationSpec().Shape + "; HP Obs=" + ((float)HitPointsRemaining / (float)NumberOfTimesPlayerCanBeHit) + "; ball One Hot=" + string.Join(",", ballOneHot) + "; relative coords=" + string.Join(",", GetRelativeCoordinates(m_HomeBasePosition)));

        //Fetch teammate and opponent lists
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

        //Observation for teammate information
        foreach (var info in teamList)
        {
            if (info.Agent != this && info.Agent.gameObject.activeInHierarchy)
            {
                m_OtherAgentsBuffer.AppendObservation(GetOtherAgentData(info));
            }
        }

        
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

    //Override for new game controller
    public override void ResetAgent()
    {
        // base.ResetAgent();

        //Reset Position
        GetAllParameters();
        StopAllCoroutines();
        transform.position = m_StartingPos;
        currentWaypoint = initialWaypoint;
        currentTarget = initialWaypoint;
        AgentRb.constraints = RigidbodyConstraints.FreezeRotation;

        //Randomly rotate agent during training 
        if (GameController.CurrentSceneType == DodgeBallGameController_WP.SceneType.Game || GameController.CurrentSceneType == DodgeBallGameController_WP.SceneType.Movie)
        {
            transform.rotation = m_StartingRot;
        }
        else //Training Mode
        {
            transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));
        }

        //Reset dodgeballs
        if (!useInfiniteAmmo)
            ActiveBallsQueue.Clear();
        currentNumberOfBalls = 0;
        AgentRb.velocity = Vector3.zero;
        AgentRb.angularVelocity = Vector3.zero;
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

    protected override void FixedUpdate()
    {
        m_DashCoolDownReady = m_CubeMovement.dashCoolDownTimer > m_CubeMovement.dashCoolDownDuration;
        if (StepCount % 5 == 0)
        {
            m_IsDecisionStep = true;
            m_AgentStepCount++;
        }
    }

    //Fetch teammate's data for observation
    protected float[] GetOtherAgentData(DodgeBallGameController_WP.PlayerInfo info)
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

    public override void PlayHitFX()
    {
        // Only shake if player object
        if (ThrowController.UseScreenShake && GameController.PlayerGameObject == gameObject)
        {
            ThrowController.impulseSource.GenerateImpulse();
        }
        PlayBallThwackSound();
        HitByParticles.Play();
        /*if (AnimateEyes)
        {
            StartCoroutine(ShowHitFace());
        }*/
    }

    public override void PlayBallThwackSound()
    {
        if (GameController.ShouldPlayEffects)
        {
            m_BallImpactAudioSource.pitch = Random.Range(2f, 3f);
            if (GameController.BallImpactClip2 != null) m_BallImpactAudioSource.PlayOneShot(m_GameController.BallImpactClip2, 1f);
            if (GameController.BallImpactClip1 != null) m_BallImpactAudioSource.PlayOneShot(m_GameController.BallImpactClip1, 1f);
        }
    }

    public override void PlayStunnedVoice()
    {
        if (GameController.ShouldPlayEffects)
        {
            m_StunnedAudioSource.pitch = Random.Range(.3f, .8f);
            if (GameController.HurtVoiceAudioClip != null) m_StunnedAudioSource.PlayOneShot(GameController.HurtVoiceAudioClip, 1f);
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
            if (GameController.GameMode == DodgeBallGameController_WP.GameModeType.CaptureTheFlag)
            {
                // Check if it is a flag
                if (col.gameObject.tag == "purpleFlag" && teamID == 0 || col.gameObject.tag == "blueFlag" && teamID == 1)
                {
                    GameController.FlagWasTaken(this);
                }
                else if (col.gameObject.tag == "purpleFlag" && teamID == 1 || col.gameObject.tag == "blueFlag" && teamID == 0)
                {
                    GameController.ReturnFlag(this);
                }
                DodgeBallAgent hitAgent = col.gameObject.GetComponent<DodgeBallAgent>();
                if (hitAgent && HasEnemyFlag && GameController.FlagCarrierKnockback)
                {
                    if (hitAgent.teamID != teamID && !hitAgent.Stunned)
                    {
                        if (GameController.ShouldPlayEffects && (GameController.FlagHitClip != null))
                        {
                            m_BallImpactAudioSource.PlayOneShot(GameController.FlagHitClip, 1f);
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
                GameController.PlayerWasHit(this, db.thrownBy);
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

    //Places projectiles back into agent's possession 
    protected override void PickUpBall(DodgeBall db)
    {
        if (GameController.ShouldPlayEffects && (GameController.BallPickupClip != null))
        {
            m_BallImpactAudioSource.PlayOneShot(GameController.BallPickupClip, .1f);
        }

        //update counter
        currentNumberOfBalls++;
        SetActiveBalls(currentNumberOfBalls);
        if (IS_DEBUG) Debug.Log(gameObject?.name + " throws. Pickup Ball. #balls=" + currentNumberOfBalls);

        if (!useInfiniteAmmo || (useInfiniteAmmo && (currentNumberOfBalls < maxAmmoCapacity)))
        {
            //add to our inventory
            ActiveBallsQueue.Enqueue(db);
            db.BallIsInPlay(true);
            db.transform.parent = transform;
            db.transform.localPosition = Vector3.zero;
        }
    }


    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!moving)
        {
            //Mask unavailable directions 
            for (int i = 0; i < 8; i++)
            {
                if (currentWaypoint.neighbors[i] != null)
                {
                    actionMask.SetActionEnabled(1, i + 1, true);
                }
                else
                {
                    actionMask.SetActionEnabled(1, i + 1, false);
                }
            }
        }
        else //Mask all other actions while moving between waypoints
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
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }
        
    }

    public override WaypointView getCurrentWaypoint()
    {
        return currentWaypoint;
    }

    public override WaypointView getTargetWaypoint()
    {
        return currentTarget;
    }
}