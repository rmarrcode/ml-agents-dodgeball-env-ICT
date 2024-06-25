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

public class DodgeBallAgent_Action
{
    public FourConnectedNode.DIRECTION movementDirection, facingDirection;
    public bool isDashing = false;

}

public class DodgeBallAgent_Scout_Waypoint : DodgeBallAgent_Scout
{
    //private static readonly bool IS_DEBUG = false;

    [Header("Attack")]
    public float attackRange = 100f;

    [Header("Waypoint")]
    public DivisibleWaypoint lastWaypoint;
    public DivisibleWaypoint currentWaypoint, nextWaypoint;
    [SerializeField]
    private DivisibleWaypoint initialWaypoint = null;
    public bool isMoving = false;
    public int moveCounter = 0;
    public float m_WaypointMovementInput;

    // Peak Flag
    protected float m_OccupationBaseBonus;
    protected float m_OtherPeakConfederateOccupationBonus;
    protected float m_ThisOccupiedPeakNoOpponentBonus;

    protected int m_NumTimesSeenByOpponent = 0;
    public int NumTimesSeenByOpponent
    {
        get { return m_NumTimesSeenByOpponent; }
        set { m_NumTimesSeenByOpponent = value; }
    }

    public int DamageGivenThisStep = 0, DamageTakenThisStep = 0;

    //private float movementCompletion = 0f;
    public float movementSpeed = 10f, movementTolerance = 0.1f, movementForce = 1f;
    public static readonly float slowMovementSpeed = 10f, fastMovementSpeed = 20f;
    public int initialStepPause = 0;
    [SerializeField]
    private float distanceToNextWaypoint = 0f;

    public enum WAYPOINT_MOVEMENT_STATUS
    {
        WAITING_TO_MOVE,
        MOVING,
        FINISHED_MOVING
    }
    public WAYPOINT_MOVEMENT_STATUS currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.WAITING_TO_MOVE;

    private static int numberOfSubWaypoints = 3; // Each waypoint can have 3 parts
    //[SerializeField]
    //private int m_currentSubWaypointIndex = 0; // goes from 0 to numberOfSubWaypoints-1
    protected Vector3[] interpolation;
    private bool m_isFastMovement = true;

    private int numMovementTries = 0, maxMovementTries = 100;

    public enum HEURISTIC_CONTROL
    {
        STATIONARY,
        RANDOM,
        USER_INPUT,
        STATE_MACHINE
    }
    public HEURISTIC_CONTROL heuristicControl = HEURISTIC_CONTROL.STATIONARY;
    public bool freezeRotation = false;
    [Tooltip("-1 means continuous. Usually 4 or 8. If >0, set discrete and continuous actions on BehaviorParameters script accordingly")]
    public int numberOfRotationDirections = -1;

    [Header("Attacking")]
    [Tooltip("spawnPos object that spawns projectiles/bullets")]
    public Transform projectileSpawnerForAiming;
    [Tooltip("Object to aim at, usually an opponent")]
    public Transform aimTarget;
    private DodgeBallAgent_Scout_Waypoint target;

    [Header("Testing")]
    public bool test = false;
    public bool test2 = false, test3 = false;
    public float test_float = 0f;
    private List<GameObject> igos = new List<GameObject>();
    //public bool isRandom = true;
    private string movementHistory = string.Empty;
    private void Update()
    {
        if (test)
        {
            //MoveToNextWaypoint(0);
            //movementSpeed = (movementSpeed == slowMovementSpeed) ? fastMovementSpeed : slowMovementSpeed;
            //Debug.Log("Number of opponent agents in sight = " + NumberOfOpponentsInSight());
            //m_currentSubWaypointIndex = (m_currentSubWaypointIndex + 1);
            test = false;
        }

        if (test2)
        {
            //m_CubeMovement.Look(test_float);
            //m_currentSubWaypointIndex = 0;
            test2 = false;
        }

        if (test3)
        {
            foreach(GameObject go in igos)
            {
                Destroy(go);
            }

            igos = new List<GameObject>();
            //Vector3[] interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(ConvertWaypointPosition(currentWaypoint.waypoint), ConvertWaypointPosition(nextWaypoint.waypoint), numberOfSubWaypoints);
            if (interpolation != null)
            {
                foreach (Vector3 pos in interpolation)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.position = pos;
                    go.GetComponent<Collider>().enabled = false;
                    igos.Add(go);
                }
            }
            //test3 = false;
        }
    }

    private void Start()
    {
        if (initialWaypoint == null)
            initialWaypoint = currentWaypoint;
    }

    public override void ResetAgent()
    {
        base.ResetAgent();
        currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.WAITING_TO_MOVE;
        if (initialWaypoint == null || initialWaypoint.waypoint == null)
        {
            initialWaypoint = currentWaypoint;
        }
        currentWaypoint = initialWaypoint;
        lastWaypoint = new DivisibleWaypoint(null, 0);
        transform.position = initialWaypoint.waypoint.position;
        m_isFastMovement = true;
        m_NumTimesSeenByOpponent = 0;

        Debug.Log(movementHistory);
        movementHistory = "Movement History for " + gameObject.name + "\n";
    }
    protected override void FixedUpdate()
    {
        AimProjectile();

        m_DashCoolDownReady = m_CubeMovement.dashCoolDownTimer > m_CubeMovement.dashCoolDownDuration;
        if (StepCount % 2 == 0) // Since about 50 steps between waypoints and we want 25 chances to attack, set it to 2
        {
            m_IsDecisionStep = true;
            m_AgentStepCount++;

            if ((teamID == 1) && 
                ((DodgeBallGameController_Extended)m_GameController).IsInMachineGunZone(currentWaypoint.waypoint))
            {
                HitPointsRemaining -= ((DodgeBallGameController_Extended)m_GameController).m_MachineGunDamage;
                if (HitPointsRemaining <= 0)
                {
                    HitPointsRemaining = 0;
                    ((DodgeBallGameController_Extended)m_GameController).AgentDied(this);
                }
            }
        }
        // Handle if flag gets home
        if (Vector3.Distance(m_HomeBasePosition, transform.position) <= 3.0f && HasEnemyFlag)
        {
            m_GameController.FlagWasBroughtHome(this);
        }

        // Could replace with DOTween
        if (isMoving)
        {
            //Vector3 nextPos = ConvertWaypointPosition(nextWaypoint);
            /*if (m_isFastMovement)
            {
                m_currentSubWaypointIndex = 0;
            }*/
            
            Vector3 nextPos = ConvertWaypointPosition(nextWaypoint.waypoint);
            //float interWaypointDist = Vector3.Distance(ConvertWaypointPosition(currentWaypoint), ConvertWaypointPosition(nextWaypoint));
            Vector3 nextDir = (nextPos - transform.position).normalized;
            transform.position += nextDir * movementSpeed * Time.fixedDeltaTime;
            if (movementForce > 0f)
                GetComponent<Rigidbody>().AddForce(movementForce * nextDir);

            // Need to interpolate points on line connecting currentWaypoint and nextwaypoint
            //Vector3[] interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(ConvertWaypointPosition(currentWaypoint.waypoint), ConvertWaypointPosition(nextWaypoint.waypoint), numberOfSubWaypoints);
            /*distanceToNextWaypoint = m_isFastMovement ?
                Vector3.Distance(transform.position, nextPos)
                :
                Vector3.Distance(transform.position, interpolation[m_currentSubWaypointIndex+1]);
            */
            distanceToNextWaypoint = Vector3.Distance(transform.position, interpolation[nextWaypoint.subIndex]); // Should work for both slow and fast movement.
            //if (IS_DEBUG) Debug.Log(gameObject + ": Moving closer to " + nextPos + "; d=" + distanceToNextWaypoint + "; subwaypoint=" + m_currentSubWaypointIndex + "; currentWaypointPos(conv)=" + ConvertWaypointPosition(currentWaypoint) + "; next(conv)=" + ConvertWaypointPosition(nextWaypoint) + "; Transform pos=" + transform.position + "; tlocpos=" + transform.localPosition);
            numMovementTries++; // Failsafe counter
            if ((distanceToNextWaypoint <= movementTolerance) || (numMovementTries >= maxMovementTries))
            {
                if (IS_DEBUG && (numMovementTries >= maxMovementTries)) Debug.Log("<color=red>EXCEEDED MAX MOVEMENT TRIES: " + numMovementTries + "</color>");
                numMovementTries = 0;

                transform.position = interpolation[nextWaypoint.subIndex];
                isMoving = false;
                currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING;

                if (IS_DEBUG) Debug.Log("Major Steps=" + DodgeBallGameController_Extended.NumMajorSteps + "<color=yellow>(A)MoveToNextWaypoint for " + gameObject.name + "; lastWaypoint=" + lastWaypoint.waypoint + "; lastSubIndex=" + lastWaypoint.subIndex + "; currentWaypoint=" + currentWaypoint.waypoint + "; currentSubindex=" + currentWaypoint.subIndex + "; nextWaypoint=" + nextWaypoint.waypoint + "; nextsubIndex=" + nextWaypoint.subIndex + "; pos=" + transform.position + "</color>");
                movementHistory += DodgeBallGameController_Extended.NumMajorSteps + "\t" + currentWaypoint.waypoint.name.Split('_')[1] + ";" + currentWaypoint.subIndex + "\t" + nextWaypoint.waypoint.name.Split('_')[1] + ";" + nextWaypoint.subIndex + "\n";

                lastWaypoint = currentWaypoint;
                currentWaypoint = nextWaypoint;

                if (IS_DEBUG) Debug.Log("<color=yellow>(B)MoveToNextWaypoint for " + gameObject.name + "; lastWaypoint=" + lastWaypoint.waypoint + "; lastSubIndex=" + lastWaypoint.subIndex + "; currentWaypoint=" + currentWaypoint.waypoint + "; currentSubindex=" + currentWaypoint.subIndex + "; nextWaypoint=" + nextWaypoint.waypoint + "; nextsubIndex=" + nextWaypoint.subIndex + "; pos=" + transform.position + "; time=" + Time.realtimeSinceStartup + "</color>");

                distanceToNextWaypoint = -1f;
                //if (IS_DEBUG) Debug.Log(gameObject + ": Done moving. lastWaypoint=" + lastWaypoint.name + "; currentWaypoint=" + currentWaypoint.name + "; subwaypoint=" + m_currentSubWaypointIndex);
            }
        }
    }

    public void ChangeMovementSpeed(bool isFast)
    {
        //movementSpeed = isFast ? fastMovementSpeed : slowMovementSpeed;
        m_isFastMovement = isFast;
    }

    protected override void GetAllParameters()
    {
        base.GetAllParameters();

        m_OccupationBaseBonus = m_EnvParameters.GetWithDefault("peak_base_bonus", 0f);
        m_OtherPeakConfederateOccupationBonus = m_EnvParameters.GetWithDefault("peak_confederate_bonus", 0f);
        m_ThisOccupiedPeakNoOpponentBonus = m_EnvParameters.GetWithDefault("peak_no_opponent_bonus", 0f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        base.CollectObservations(sensor);

        //Debug.Log("CollectObservations @ " + Time.realtimeSinceStartup); //  About once per millisecond
        List<GameObject> peakFlags = ((DodgeBallGameController_Extended)m_GameController).peakFlags;
        foreach (GameObject peakFlag in peakFlags)
        {
            sensor.AddObservation(GetRelativeCoordinates(peakFlag.transform.position));
        }

        PeakFlagOccupancyStateData pfosData = ((DodgeBallGameController_Extended)m_GameController).GetOccupancyData(this);
        DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE agentColor = ((DodgeBallGameController_Extended)m_GameController).GetAgentColor(this);
        //bool oneIsEmpty = false;
        bool thisContainsOpponent = false;
        bool otherContainsOpponent = false;
        bool thisContainsTeammate = false;
        bool otherContainsTeammate = false;
        if (pfosData.isSelfOnFlagSpot)
        {
            AddReward(m_OccupationBaseBonus);

            //foreach (DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE pos in pfosData.occupancyStates)
            for (int i = 0; i < pfosData.occupancyStates.Length; ++i)
            {
                DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE pos = pfosData.occupancyStates[i];
                bool isOnThisFlag = (i == pfosData.occupiedFlagSpotID);
                // Redo with consideration of knowing which flag they are on;l
                switch (pos)
                {
                    case DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE.EMPTY:
                        if (isOnThisFlag)
                            if (IS_DEBUG) Debug.LogError("Registered flagpoint as empty but Agent is on it!");
                        /*else
                            oneIsEmpty = true;*/
                        break;
                    case DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE.BLUE:
                        if (agentColor == DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE.PURPLE)
                        {
                            if (isOnThisFlag)
                            {
                                thisContainsOpponent = true;
                            }
                            else
                            {
                                otherContainsOpponent = true;
                            }
                        }
                        else
                        {
                            if (isOnThisFlag)
                            {
                                thisContainsTeammate = true;
                            }
                            else
                            {
                                otherContainsTeammate = true;
                            }
                        }
                        break;
                    case DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE.PURPLE:
                        if (agentColor == DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE.BLUE)
                        {
                            if (isOnThisFlag)
                            {
                                thisContainsOpponent = true;
                            }
                            else
                            {
                                otherContainsOpponent = true;
                            }
                        }
                        else
                        {
                            if (isOnThisFlag)
                            {
                                thisContainsTeammate = true;
                            }
                            else
                            {
                                otherContainsTeammate = true;
                            }
                        }
                        break;
                    case DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE.BOTH:
                        if (isOnThisFlag)
                        {
                            thisContainsOpponent = true;
                            thisContainsTeammate = true;
                        }
                        else
                        {
                            otherContainsOpponent = true;
                            otherContainsTeammate = true;
                        }
                        break;
                    default:
                        break;
                }
            }
            if (!thisContainsOpponent)
            {
                AddReward(m_ThisOccupiedPeakNoOpponentBonus);
            }
            if (otherContainsOpponent) { }
            if (thisContainsTeammate) { }
            if (otherContainsTeammate)
            {
                AddReward(m_OtherPeakConfederateOccupationBonus);
            }

            if (IS_DEBUG) Debug.Log("<color=yellow>" + gameObject.name + ": Collect Observations. IsSelfOnFlagSpot=" + pfosData.isSelfOnFlagSpot + "; ThisContainsOpponent = " + thisContainsOpponent + "; otherContainsTeammate=" + otherContainsTeammate + "</color>");
        }
    }


    //Execute agent movement
    public override void MoveAgent(ActionBuffers actionBuffers)
    {
        // Continuous: 1
        // Discrete: 5,2/*,2*/

        if (Stunned)
        {
            return;
        }
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;

        m_WaypointMovementInput = (int)discreteActions[0]; // 5
        //m_ThrowInput = (int)discreteActions[1]; // 2
        aimTarget = GetTargetAgent()?.transform;
        m_ThrowInput = aimTarget == null ? 0 : 1;

        if (numberOfRotationDirections <= 0)
        {
            m_Rotate = continuousActions[0];
            m_CubeMovement.Look(m_Rotate);
        }
        else
        {
            m_Rotate = 0f;
            transform.localEulerAngles = new Vector3(
                0f,
                Mathf.Lerp(0f, 360f, (float)discreteActions[2] / (float)numberOfRotationDirections),
                0f);
        }
        m_DashInput = 0; // (int)discreteActions[2]; // 2

        //HANDLE ROTATION


        //HANDLE XZ MOVEMENT
        //var moveDir = transform.TransformDirection(new Vector3(m_InputH, 0, m_InputV));
        //m_CubeMovement.RunOnGround(moveDir);
        if (//(((DodgeBallGameController_Extended)m_GameController).m_ReadyForWaypointMove) &&
            (currentMovementStatus == WAYPOINT_MOVEMENT_STATUS.WAITING_TO_MOVE))
        {
            //((DodgeBallGameController_Extended)m_GameController).m_ReadyForWaypointMove = false;
            MoveToNextWaypoint(discreteActions[0]);
        }

        //perform discrete actions only once between decisions
        if (m_IsDecisionStep)
        {
            m_IsDecisionStep = false;

            //HANDLE THROWING
            if (m_ThrowInput > 0)
            {
                ThrowTheBall();
            }
            //HANDLE DASH MOVEMENT
            //if (m_DashInput > 0 && m_DashCoolDownReady)
            //{
            //  m_CubeMovement.Dash(moveDir);
            //}
        }
    }

    public virtual int GetDirectionFromWaypoint(WaypointMono waypoint)
    {
        for (int i = 0; i < 5; ++i)
        {
            if (i == 0)
            {
                if (currentWaypoint.waypoint == waypoint)
                {
                    return i;
                }
            }
            else
            {
                
                if ( currentWaypoint.waypoint.neighbors.ContainsKey((FourConnectedNode.DIRECTION)i) &&
                    (currentWaypoint.waypoint.neighbors[(FourConnectedNode.DIRECTION)i] == waypoint)
                    )
                {
                    return i;
                }
            }
        }
        if (IS_DEBUG) Debug.LogError("WAYPOINT NOT A NEIGHBOR!");
        return 0;
    }

    public virtual void MoveToNextWaypoint(WaypointMono waypoint)
    {
        MoveToNextWaypoint(GetDirectionFromWaypoint(waypoint));
    }

    // Occurs after agents have all finished moving 
    public virtual void MoveToNextWaypoint(int index)
    {
        Debug.Log(gameObject.name + ": MoveToNextWaypoint(" + index + ")");
        currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.MOVING;

        // At a full waypoint
        if (currentWaypoint.subIndex % numberOfSubWaypoints == 0)
        {
            // Check movement direction
            // Movement is NONE
            if ((index == 0) ||
                !currentWaypoint.waypoint.neighbors.ContainsKey((FourConnectedNode.DIRECTION)index))
            {
                if (IS_DEBUG) Debug.Log("Major Steps=" + DodgeBallGameController_Extended.NumMajorSteps + "<color=orange>(A)MoveToNextWaypoint for " + gameObject.name + ": index=" + index + "</color>");
                // do NOT update waypoint or index!
                transform.position = ConvertWaypointPosition(currentWaypoint.waypoint);
                isMoving = false;
                currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING;
                moveCounter++;
                //lastWaypoint = currentWaypoint; // Don't set this if staying still
                nextWaypoint.waypoint = currentWaypoint.waypoint;
                if (IS_DEBUG) Debug.Log(gameObject.name + ": No movement. Set waypoint to " + currentWaypoint);
                return;
            }
            else // We can move! But let's assume only partially (we can change amount of move later
            {
                nextWaypoint.waypoint = currentWaypoint.waypoint.neighbors[(FourConnectedNode.DIRECTION)index];
                if (m_isFastMovement)
                    nextWaypoint.subIndex = numberOfSubWaypoints;
                else
                    nextWaypoint.subIndex = 1;
                // Set up interpolation here and only here. Otherwise, we might recalculate them between two subwaypoints
                /*
                if (lastWaypoint.waypoint == null)
                {
                    interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(ConvertWaypointPosition(currentWaypoint.waypoint), ConvertWaypointPosition(nextWaypoint.waypoint), numberOfSubWaypoints);
                }
                else
                {
                    interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(ConvertWaypointPosition(lastWaypoint.waypoint), ConvertWaypointPosition(nextWaypoint.waypoint), numberOfSubWaypoints);
                }*/
                WaypointMono closestWaypoint = AdjacencyMatrixUtility.GetClosestWaypointToPosition(transform.position);
                if ((closestWaypoint != null) && closestWaypoint.neighbors.ContainsValue(nextWaypoint.waypoint))
                {
                    interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(
                        ConvertWaypointPosition(closestWaypoint),
                        ConvertWaypointPosition(nextWaypoint.waypoint),
                        numberOfSubWaypoints);
                }
                else
                {
                    if (closestWaypoint == null)
                    {
                        currentWaypoint = initialWaypoint;
                    }

                    if (IS_DEBUG) Debug.LogError("Major Steps=" + DodgeBallGameController_Extended.NumMajorSteps + "; " + closestWaypoint + " not adjacent to " + nextWaypoint.waypoint + " for interpolation!");
                    // No move.
                    transform.position = ConvertWaypointPosition(currentWaypoint.waypoint);
                    isMoving = false;
                    currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING;
                    moveCounter++;
                    //lastWaypoint = currentWaypoint; // Don't set this if staying still
                    nextWaypoint.waypoint = currentWaypoint.waypoint;
                    return;
                }


                if (IS_DEBUG) Debug.Log(gameObject.name + ": Current Waypoint = " + currentWaypoint.waypoint.name + "Next waypoint = " + nextWaypoint.waypoint.name + "; Direction=" + (FourConnectedNode.DIRECTION)index + "; closest to pos=" + AdjacencyMatrixUtility.GetClosestWaypointToPosition(transform.position));
                transform.position = interpolation[currentWaypoint.subIndex % numberOfSubWaypoints]; //ConvertWaypointPosition(currentWaypoint.waypoint);
                isMoving = true;
                moveCounter++;
                if (IS_DEBUG) Debug.Log("<color=orange>(B)MoveToNextWaypoint for " + gameObject.name + ": index=" + index + "; currentWaypoint=" + currentWaypoint.waypoint + "; currentSubindex=" + currentWaypoint.subIndex + "; nextWaypoint=" + nextWaypoint.waypoint + "; nextsubIndex=" + nextWaypoint.subIndex + "; pos=" + transform.position + "; time=" + Time.realtimeSinceStartup + "</color>");
            }
        }
        else // partially the way to next waypoint
        {
            // We don't care what the action says, we are only going to the next incremental subwaypoint!
            //Vector3[] interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(ConvertWaypointPosition(currentWaypoint.waypoint), ConvertWaypointPosition(nextWaypoint.waypoint), numberOfSubWaypoints);
            transform.position = interpolation[currentWaypoint.subIndex]; //ConvertWaypointPosition(currentWaypoint.waypoint);
            //lastWaypoint = currentWaypoint; set this later
            nextWaypoint = new DivisibleWaypoint(currentWaypoint.waypoint, currentWaypoint.subIndex + 1);
            isMoving = true;
            moveCounter++;
            if (IS_DEBUG) Debug.Log("<color=orange>(C)MoveToNextWaypoint for " + gameObject.name + ": index=" + index + "; currentWaypoint=" + currentWaypoint.waypoint + "; currentSubindex=" + currentWaypoint.subIndex + "; nextWaypoint=" + nextWaypoint.waypoint + "; nextsubIndex=" + nextWaypoint.subIndex + "; pos=" + transform.position + "</color>");
        }

        /*
        // Movement is NONE
        if ((index == 0) ||
            !currentWaypoint.waypoint.neighbors.ContainsKey((FourConnectedNode.DIRECTION)index))
        {
            if (m_isFastMovement || (nextWaypoint == null))
            {
                transform.position = ConvertWaypointPosition(currentWaypoint.waypoint);
            }

            isMoving = false;
            currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING;
            moveCounter++;
            //lastWaypoint = currentWaypoint; // Don't set this if staying still
            nextWaypoint.waypoint = currentWaypoint.waypoint;
            if (IS_DEBUG) Debug.Log(gameObject.name + ": No movement. Set waypoint to " + currentWaypoint);
            return;
        }

        nextWaypoint.waypoint = currentWaypoint.waypoint.neighbors[(FourConnectedNode.DIRECTION)index];

        if (IS_DEBUG) Debug.Log(gameObject.name + ": Current Waypoint = " + currentWaypoint.waypoint.name + "Next waypoint = " + nextWaypoint.name + "; Direction=" + (FourConnectedNode.DIRECTION)index);
        if (m_isFastMovement)
            transform.position = ConvertWaypointPosition(currentWaypoint);
        else
        {
            Vector3[] interpolation = AdjacencyMatrixUtility.GetInterpolatedPoints(ConvertWaypointPosition(currentWaypoint), ConvertWaypointPosition(nextWaypoint), numberOfSubWaypoints);
            transform.position = interpolation[m_currentSubWaypointIndex];
        }

        if (!m_isFastMovement)
        {
            m_currentSubWaypointIndex = (m_currentSubWaypointIndex + 1);
        }

        isMoving = true;
        */
    }

    /*IEnumerator _MoveToWaypoint(WaypointMono nextWaypoint)
    {
        if (IS_DEBUG) Debug.Log(gameObject.name + ": _MoveToWaypoint()");
        yield return new WaitForEndOfFrame();
        for (float f = 0f; f <= 1.0f; f += 0.1f)
        {
            if (IS_DEBUG) Debug.Log(gameObject.name + ":A _MoveToWaypoint(). f=" + f + "; nextWp= " + nextWaypoint);
            transform.position = Vector3.Lerp(
                                    transform.position,
                                    ConvertWaypointPosition(nextWaypoint),
                                    f
                                    );
            if (IS_DEBUG) Debug.Log(gameObject.name + ":B _MoveToWaypoint(). f=" + f + "; nextWp= " + nextWaypoint);
            yield return new WaitForSeconds(0.1f); //null;
        }
        isMoving = false;
        currentMovementStatus = WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING;
        moveCounter++;
        currentWaypoint.waypoint = nextWaypoint;
        if (IS_DEBUG) Debug.Log(gameObject.name + ": Set waypoint to " + currentWaypoint);
        yield return new WaitForEndOfFrame();
    }*/

    private Vector3 ConvertWaypointPosition(WaypointMono waypoint)
    {
        Vector3 parentPos = waypoint.transform.parent.transform.localPosition;
        float scale = waypoint.transform.parent.localScale.x;
        return scale * (parentPos / scale + waypoint.transform.localPosition);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if ((HitPointsRemaining <= 0) || !gameObject.activeSelf)
        {
            actionMask.SetActionEnabled(0, (int)FourConnectedNode.DIRECTION.NORTH, false);
            actionMask.SetActionEnabled(0, (int)FourConnectedNode.DIRECTION.EAST, false);
            actionMask.SetActionEnabled(0, (int)FourConnectedNode.DIRECTION.SOUTH, false);
            actionMask.SetActionEnabled(0, (int)FourConnectedNode.DIRECTION.WEST, false);
        }
        else
        {
            for (int dir = 1; dir <= 4; ++dir)
            {
                actionMask.SetActionEnabled(0, dir,
                    currentWaypoint.waypoint.neighbors.ContainsKey((FourConnectedNode.DIRECTION)dir));
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Stay put if there is an initial step pause
        if (DodgeBallGameController_Extended.NumMajorSteps < initialStepPause)
        {
            StationaryHeuristic(actionsOut);
            return;
        }

        switch (heuristicControl)
        {
            case HEURISTIC_CONTROL.STATIONARY:
                StationaryHeuristic(actionsOut);
                break;
            case HEURISTIC_CONTROL.RANDOM:
                RandomHeuristic(actionsOut);
                break;
            case HEURISTIC_CONTROL.USER_INPUT:
                InputHeuristic(actionsOut);
                break;
            case HEURISTIC_CONTROL.STATE_MACHINE:
                StateMachineHeuristic(actionsOut);
                break;
        }


    }

    private void StationaryHeuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }
        var discreteActionsOut = actionsOut.DiscreteActions;

        discreteActionsOut[0] = 0;
        //discreteActionsOut[1] = 0; //attack
        aimTarget = GetTargetAgent()?.transform;
        discreteActionsOut[1] = (aimTarget == null) || !DodgeBallGameController_Extended.agentsCanShoot ? 0 : 1;

        if (numberOfRotationDirections <= 0)
        {
            var contActionsOut = actionsOut.ContinuousActions;
            contActionsOut[0] = 0f; //rotate
        }
        else
        {
            discreteActionsOut[2] = 0;
        }
    }

    private void RandomHeuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }
        var discreteActionsOut = actionsOut.DiscreteActions;

        int moveInput = Random.Range(0, 5);

        if (IS_DEBUG) Debug.Log(gameObject.name + ": heuristic moveInput=" + moveInput);
        discreteActionsOut[0] = moveInput;
        discreteActionsOut[1] = (Random.value > 0.5f ? 1 : 0); //dash

        if (numberOfRotationDirections <= 0)
        {
            var contActionsOut = actionsOut.ContinuousActions;
            contActionsOut[0] = freezeRotation ? 0 : Random.Range(-1f, 1f) * 3; //rotate
        }
        else
        {
            discreteActionsOut[2] = freezeRotation ? 0 : Random.Range(0, numberOfRotationDirections);
        }

    }

    private void InputHeuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }
        var discreteActionsOut = actionsOut.DiscreteActions;

        int moveInput = 0;
        if (input.moveInput.y > 0)
            moveInput = (int)FourConnectedNode.DIRECTION.NORTH;
        else if (input.moveInput.y < 0)
            moveInput = (int)FourConnectedNode.DIRECTION.SOUTH;
        else if (input.moveInput.x > 0)
            moveInput = (int)FourConnectedNode.DIRECTION.EAST;
        else if (input.moveInput.x < 0)
            moveInput = (int)FourConnectedNode.DIRECTION.WEST;

        if (IS_DEBUG) Debug.Log(gameObject.name + ": heuristic moveInput=" + moveInput);
        discreteActionsOut[0] = moveInput;
        aimTarget = GetTargetAgent()?.transform;
        //discreteActionsOut[1] = (input.CheckIfInputSinceLastFrame(ref input.m_throwPressed) ? 1 : 0); //dash
        //discreteActionsOut[2] = 0; //isRandom ? (Random.value > 1.5f ? 1 : 0) : (input.CheckIfInputSinceLastFrame(ref input.m_dashPressed) ? 1 : 0); //dash
        discreteActionsOut[1] = (aimTarget == null) || !DodgeBallGameController_Extended.agentsCanShoot ? 0 : 1;

        if (numberOfRotationDirections <= 0)
        {
            var contActionsOut = actionsOut.ContinuousActions;
            contActionsOut[0] = freezeRotation ? 0 : (input.rotateInput) * 3; //rotate
        }
        else
        {
            // Rotation based on mouse
            // int currentRotationSteps = Mathf.RoundToInt((transform.localEulerAngles.y / 360f) * (float)numberOfRotationDirections);
            // discreteActionsOut[2] = freezeRotation ? 0 : currentRotationSteps + ((input.rotateInput > 0.25f) ? 1 : ((input.rotateInput < -0.25f) ? -1 : 0));

            // Rotation based on enemy target
            if (aimTarget == null)
            {
                discreteActionsOut[2] = discreteActionsOut[0];
            }
            else
            {
                transform.LookAt(aimTarget);
                discreteActionsOut[2] = GetDiscreteRotationIndexFromAngle(transform.eulerAngles.y, numberOfRotationDirections);
            }
        }
    }

    private void StateMachineHeuristic(in ActionBuffers actionsOut)
    {
        if (disableInputCollectionInHeuristicCallback || m_IsStunned)
        {
            return;
        }

        /*if (!m_isFastMovement && (m_currentSubWaypointIndex != 0))
        {
            return;
        }*/

        // FSM code
        DodgeBallGameController_Extended gc = (DodgeBallGameController_Extended)m_GameController;
        DodgeBallGroup agentGroup = gc.GetGroupForAgent(this);
        if (m_IsDecisionStep && (StepCount != agentGroup.lastActedStep) && gc.IsInitialized())
        {
            agentGroup.lastActedStep = StepCount;            
        }

        var discreteActionsOut = actionsOut.DiscreteActions;
        if (gc.IsInitialized())
        {
            DodgeBallAgent_Action actionData = gc.CheckState(agentGroup);
            if (IS_DEBUG) Debug.Log(gameObject.name + " FSM move to " + actionData.movementDirection);
            discreteActionsOut[0] = (int)actionData.movementDirection;
            aimTarget = GetTargetAgent()?.transform;
        }
        else
        {
            discreteActionsOut[0] = 0;
        }

        discreteActionsOut[1] = (aimTarget == null) || !DodgeBallGameController_Extended.agentsCanShoot ? 0 : 1;
        if (numberOfRotationDirections <= 0)
        {
            var contActionsOut = actionsOut.ContinuousActions;
            contActionsOut[0] = 0f; //rotate
        }
        else
        {
            if (aimTarget == null)
            {
                discreteActionsOut[2] = discreteActionsOut[0]; // Same as movement direction
            }
            else
            {
                // Get float angle to aimTarget
                //Vector2 this_flat = new Vector2(transform.position.x, transform.position.z);
                //Vector2 target_flat = new Vector2(aimTarget.position.x, aimTarget.position.z);
                //float flatAngle_deg = Mathf.Rad2Deg*Mathf.Atan2(this_flat.y - target_flat.y, this_flat.x - target_flat.x);
                transform.LookAt(aimTarget);
                discreteActionsOut[2] = GetDiscreteRotationIndexFromAngle(transform.eulerAngles.y, numberOfRotationDirections);
            }
        }
    }

    private void AimProjectile()
    {
        if (projectileSpawnerForAiming != null)
        {
            if (aimTarget == null)
            {
                projectileSpawnerForAiming.localEulerAngles = Vector3.zero;
            }
            else
            {
                projectileSpawnerForAiming.LookAt(aimTarget);
            }
        }
    }

    // TODO: Take into account FOV of 180, might be trivial
    public List<DodgeBallAgent_Scout_Waypoint> GetOpponentsInSight()
    {
        List<DodgeBallAgent_Scout_Waypoint> opponentsInSight = new List<DodgeBallAgent_Scout_Waypoint>();

        //int numOpponentsInSight = 0;
        int opponentTeamID = (teamID == 0) ? 1 : 0;
        List<DodgeBallGameController.PlayerInfo> opponentTeam = (opponentTeamID == 0) ? ((DodgeBallGameController_Extended)m_GameController).Team0Players : ((DodgeBallGameController_Extended)m_GameController).Team1Players;

        int agentIndex = 0;
        foreach (DodgeBallGameController.PlayerInfo playerInfo in opponentTeam)
        {
            if (IS_DEBUG) Debug.Log(agentIndex + ") Opponent agent = " + playerInfo.Agent.name + "; HP=" + playerInfo.Agent.HitPointsRemaining);
            if (playerInfo.Agent.HitPointsRemaining > 0)
            {
                float maxDist = Vector3.Distance(playerInfo.Agent.transform.position, transform.position) + test_float;
                Vector3 dir = (playerInfo.Agent.transform.position - transform.position).normalized;

                if (((DodgeBallAgent_Scout_Waypoint)playerInfo.Agent).currentWaypoint == currentWaypoint)
                {
                    Debug.Log("Same waypoint!");
                    //numOpponentsInSight++;
                    opponentsInSight.Add((DodgeBallAgent_Scout_Waypoint)playerInfo.Agent);
                    continue;
                }

                RaycastHit hit;
                if (IS_DEBUG) Debug.DrawRay(transform.position, playerInfo.Agent.transform.position - transform.position, Color.red);
                if (Physics.Raycast(transform.position, dir, out hit, maxDist))
                {
                    if (IS_DEBUG) Debug.Log(agentIndex + "A) Raycast Hit: " + hit.transform.gameObject.name + "; hit? " + (hit.transform.gameObject.name.Trim() == playerInfo.Agent.gameObject.name.Trim()));
                    if (hit.transform.gameObject.name.Trim() == playerInfo.Agent.gameObject.name.Trim())
                    {
                        //numOpponentsInSight++;
                        opponentsInSight.Add((DodgeBallAgent_Scout_Waypoint)playerInfo.Agent);
                        continue;
                    }
                }
                else
                {
                    if (IS_DEBUG) Debug.Log(agentIndex + "A) No Hit");
                }

                if (IS_DEBUG) Debug.DrawRay(playerInfo.Agent.transform.position, transform.position - playerInfo.Agent.transform.position, Color.blue);
                if (Physics.Raycast(playerInfo.Agent.transform.position, -dir, out hit, maxDist))
                {
                    if (IS_DEBUG) Debug.Log(agentIndex + "B) Raycast Hit: " + hit.transform.gameObject.name + "; hit? " + (hit.transform.gameObject.name.Trim() == gameObject.name.Trim()));
                    if (hit.transform.gameObject.name.Trim() == gameObject.name.Trim())
                    {
                        //numOpponentsInSight++;
                        opponentsInSight.Add((DodgeBallAgent_Scout_Waypoint)playerInfo.Agent);
                        continue;
                    }
                }
                else
                {
                    if (IS_DEBUG) Debug.Log(agentIndex + "B) No Hit");
                }
            }
            agentIndex++;
        }

        return opponentsInSight;
    }

    public int NumberOfOpponentsInSight()
    {
        return GetOpponentsInSight().Count;
    }

    public DodgeBallAgent_Scout_Waypoint GetTargetAgent()
    {
        target = null;
        List<DodgeBallAgent_Scout_Waypoint> inSightOpponents = GetOpponentsInSight();

        Dictionary<DodgeBallAgent_Scout_Waypoint, float> rangeDic = new Dictionary<DodgeBallAgent_Scout_Waypoint, float>();
        float minD = float.MaxValue;
        foreach(DodgeBallAgent_Scout_Waypoint inSightOpponent in inSightOpponents)
        {
            if (inSightOpponent.HitPointsRemaining <= 0 || inSightOpponent.NumberOfTimesPlayerCanBeHit <= 0)
                continue;

            float d = Vector3.Distance(transform.position, inSightOpponent.transform.position);
            if (d < attackRange)
            {
                rangeDic.Add(inSightOpponent, d);
                if (d < minD)
                {
                    target = inSightOpponent;
                    minD = d;
                }
            }
        }

        return target;
    }

    public bool IsEngaged()
    {
        return GetTargetAgent() != null;
    }

    public static int GetDiscreteRotationIndexFromAngle(float angle, int numRotationDirections)
    {
        //Debug.Log("A) DiscreteRotationIndexFromAngle. Angle=" + angle);
        float divAngle = 360f / numRotationDirections;
        angle += divAngle/2f; // offset it
        while (angle < 0f)
        {
            angle += 360f;
        }
        while (angle > 360f)
        {
            angle -= 360f;
        }

        int index = Mathf.Clamp(Mathf.FloorToInt(angle / divAngle), 0, numRotationDirections-1);
        //Debug.Log("B) DiscreteRotationIndexFromAngle. Angle=" + angle + "; index=" + index);
        return index;
    }
}
