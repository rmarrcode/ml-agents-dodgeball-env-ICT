using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

[System.Serializable]
public class DivisibleWaypoint
{
    public WaypointMono waypoint = null;
    public int subIndex = 0;

    public DivisibleWaypoint(WaypointMono waypoint, int subIndex)
    {
        this.waypoint = waypoint;
        this.subIndex = subIndex;
    }
}

public class PeakFlagOccupancyStateData
{
    public bool isSelfOnFlagSpot = false;
    public int occupiedFlagSpotID = -1;
    public DodgeBallGameController_Extended.PEAKFLAG_OCCUPANCY_STATE[] occupancyStates;
}

[System.Serializable]
public class DodgeBallGroup
{
    public int index;
    public int lastActedStep = -1; // If the current step doesn't equal this one, then do it.This way, agents in the group can drive action.
    public List<DodgeBallAgent_Scout_Waypoint> agents = new List<DodgeBallAgent_Scout_Waypoint>();
    public bool isSendingAssistanceSignal = false, shelterBound = false;
    public WaypointMono goalWaypoint, safetyWaypoint;
    public DodgeBallGameController_Extended.SCENARIO_STATE currentScenarioState = DodgeBallGameController_Extended.SCENARIO_STATE.INITIAL;

    public void SetAssistanceSignal(bool isSet)
    {
        isSendingAssistanceSignal = isSet;
    }

    public bool GetAssistanceSignal()
    {
        return isSendingAssistanceSignal;
    }

    public void SetShelterBound(bool isSet)
    {
        shelterBound = isSet;
    }

    public bool GetShelterBound()
    {
        return shelterBound;
    }

    public WaypointMono GetCurrentWaypoint()
    {
        foreach(DodgeBallAgent_Scout_Waypoint agent in agents)
        {
            if (agent.HitPointsRemaining > 0)
            {
                return agent.currentWaypoint.waypoint;
            }
        }
        return null;
    }

    public DodgeBallAgent_Scout_Waypoint GetTarget()
    {
        foreach(DodgeBallAgent_Scout_Waypoint agent in agents)
        {
            if (agent.HitPointsRemaining > 0)
            {
                return agent.GetTargetAgent();
            }
        }
        return null;
    }
}

public class DodgeBallGameController_Extended : DodgeBallGameController
{
    private static bool IS_DEBUG = false;
    private static bool REWARD_DEBUG = true;

    [Header("Extended Properties")]
    //private static readonly bool IS_DEBUG = false;

    public int BlueTeamMaxHP = 200;
    public int PurpleTeamMaxHP = 100;
    public int projectileDamage = 3;

    public bool force_reset = false;
    public bool m_ReadyForWaypointMove = true;
    public List<WaypointMono> flagWaypoints = new List<WaypointMono>();
    public List<GameObject> peakFlags = new List<GameObject>();

    private static int m_numMajorSteps = 0;
    public static int NumMajorSteps
    {
        get { return m_numMajorSteps; }
    }
    public int MaxMajorSteps = 40;

    public static bool agentsCanShoot =
#if UNITY_EDITOR
        false;
#else
        true;
#endif


    public bool areAgentsAtPeaks_indicator = false;

    //private static string ridgeLineString = "(12,10);(12,9);(18,7);(18,6);(12,13);(12,11);(10,14);(13,4);(14,4);(15,11);(13,11);(14,7);(14,6);(14,5)";
    public List<WaypointMono> machineGunZone = new List<WaypointMono>();

    private int m_EpisodeCount = 0;
    [TextArea(5, 50)]
    public string tabulatedData = "#Ep\t#Steps\t#BlueAlive\t#RedAlive\tAssist?\tRetreat?\tDelayReward\tAnyAtShelter?\tBlueAtPeaks?\tHealthRew\tCumRew\n";

    [SerializeField]
    public List<DodgeBallGroup> groups = new List<DodgeBallGroup>();
    private float dangerousProbability = 0.9f, cautiousProbability = 0.5f;

    public enum SCENARIO_STATE
    {
        INITIAL,
        RETREAT,
        FORWARD,
        ASSIST,
        MOVE_TO_TARGET,
        OPFOR_IN_SIGHT,
        SEND_ASSIST_SIGNAL
    }
    //public SCENARIO_STATE currentScenarioState = SCENARIO_STATE.INITIAL;

    [TextArea(5, 10)]
    public string[] flagWaypointData = new string[2];

    public enum PEAKFLAG_OCCUPANCY_STATE
    {
        EMPTY,
        BLUE,
        PURPLE,
        BOTH
    }

    [Header("Hyperparameters")]
    public int m_MachineGunDamage = 10;
    public float m_BlueWinReward = 1f;
    public float m_RedWinReward = 1f;
    public float m_SeenByOpponentReward = 0f;
    public float m_StepDamageBasedReward = 1f;

    protected override void Start()
    {
        base.Start();
#if UNITY_EDITOR
        MaxEnvironmentSteps = 1000000;
#endif
    }


    private void LateUpdate()
    {
#if UNITY_EDITOR
        areAgentsAtPeaks_indicator = AreBlueAgentsAtPeaks();
#endif

        if (force_reset)
        {
            ResetScene();
            force_reset = false;
        }

        // Step Rewards
        if (DoAllAgentsHaveMovementStatus(DodgeBallAgent_Scout_Waypoint.WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING))
        {
            // Check if purple seen by blue
            List<DodgeBallAgent_Scout_Waypoint> seenPurpleAgents = new List<DodgeBallAgent_Scout_Waypoint>();
            foreach(PlayerInfo blue_pi in Team0Players)
            {
                DodgeBallAgent_Scout_Waypoint blueAgent = ((DodgeBallAgent_Scout_Waypoint)blue_pi.Agent);
                foreach (PlayerInfo purple_pi in Team1Players)
                {
                    DodgeBallAgent_Scout_Waypoint purpleAgent = ((DodgeBallAgent_Scout_Waypoint)purple_pi.Agent);
                    if ( blueAgent.GetOpponentsInSight().Contains(purpleAgent)
                         && !seenPurpleAgents.Contains(purpleAgent)
                        )
                    {
                        purpleAgent.NumTimesSeenByOpponent++;
                        seenPurpleAgents.Add(purpleAgent); // Make sure only counted once
                    }
                }
            }

            // Firing rewards: +1 for giving more than taking or same, -1 otherwise
            foreach (PlayerInfo purple_pi in Team1Players)
            {
                DodgeBallAgent_Scout_Waypoint purpleAgent = ((DodgeBallAgent_Scout_Waypoint)purple_pi.Agent);
                // Give rewards only if some action taken
                if (purpleAgent.DamageGivenThisStep > 0 || purpleAgent.DamageTakenThisStep > 0)
                {
                    if (purpleAgent.DamageGivenThisStep >= purpleAgent.DamageTakenThisStep)
                    {
                        purpleAgent.AddReward(m_StepDamageBasedReward);
                        //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>" + purpleAgent.name + ". StepDamageBasedReward=" + m_StepDamageBasedReward + "</color>");
                    }
                    else
                    {
                        purpleAgent.AddReward(-m_StepDamageBasedReward);
                        //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>" + purpleAgent.name + ". StepDamageBasedReward=" + (-m_StepDamageBasedReward) + "</color>");
                    }
                }

                // Reset counters
                purpleAgent.DamageGivenThisStep = 0;
                purpleAgent.DamageTakenThisStep = 0;
            }
        }
        CheckIfAllMovesDone();

        if (DoAllAgentsHaveMovementStatus(DodgeBallAgent_Scout_Waypoint.WAYPOINT_MOVEMENT_STATUS.WAITING_TO_MOVE))
        {
            m_ReadyForWaypointMove = true;
        }

        for (int i = 0; i < flagWaypoints.Count; ++i)
        {
            WaypointMono flagWaypoint = flagWaypoints[i];
            List<DodgeBallAgent_Scout_Waypoint> flagAgents = GetAgentsAtWaypoint(flagWaypoint);
            flagWaypointData[i] = string.Empty;
            foreach(DodgeBallAgent_Scout_Waypoint flagAgent in flagAgents)
            {
                flagWaypointData[i] += flagAgent.gameObject.name + ";";
            }
        }

        CheckBlueAgentsHaveOccupiedPeaks(); // if occupied, win and end episode
        CheckBlueAgentsHaveOccupiedShelter();

        if (m_numMajorSteps >= MaxMajorSteps)
        {
            // Blue must lose
            SetEndOfEpisodeRewards(m_Team1AgentGroup, m_Team0AgentGroup);
            EndGame(1, 0);
        }
    }

    protected override void Initialize()
    {
        m_audioSource = gameObject.AddComponent<AudioSource>();
        m_StatsRecorder = Academy.Instance.StatsRecorder;
        m_EnvParameters = Academy.Instance.EnvironmentParameters;
        GameMode = getCurrentGameMode();
        m_Team0AgentGroup = new SimpleMultiAgentGroup();
        m_Team1AgentGroup = new SimpleMultiAgentGroup();
        InstantiateBalls();

        //INITIALIZE AGENTS
        foreach (var item in Team0Players)
        {
            item.Agent.Initialize();
            item.Agent.HitPointsRemaining = BlueTeamMaxHP; // PlayerMaxHitPoints;
            item.Agent.m_BehaviorParameters.TeamId = 0;
            item.TeamID = 0;
            item.Agent.NumberOfTimesPlayerCanBeHit = BlueTeamMaxHP; // PlayerMaxHitPoints;
            if (IS_DEBUG) Debug.Log("Team0Player. HP=" + item.Agent.HitPointsRemaining + "; numTimesCanBeHit=" + item.Agent.NumberOfTimesPlayerCanBeHit);
            m_Team0AgentGroup.RegisterAgent(item.Agent);
        }
        foreach (var item in Team1Players)
        {
            item.Agent.Initialize();
            item.Agent.HitPointsRemaining = PurpleTeamMaxHP; // PlayerMaxHitPoints;
            item.Agent.m_BehaviorParameters.TeamId = 1;
            item.TeamID = 1;
            item.Agent.NumberOfTimesPlayerCanBeHit = PurpleTeamMaxHP; // PlayerMaxHitPoints;
            m_Team1AgentGroup.RegisterAgent(item.Agent);
        }

        SetActiveLosers(blueLosersList, 0);
        SetActiveLosers(purpleLosersList, 0);

        //Poof Particles
        if (usePoofParticlesOnElimination)
        {
            foreach (var item in poofParticlesList)
            {
                item.SetActive(false);
            }
        }
        m_Initialized = true;
        ResetScene();
    }

    public bool IsInitialized()
    {
        return m_Initialized;
    }

    public bool DoAllAgentsHaveMovementStatus(DodgeBallAgent_Scout_Waypoint.WAYPOINT_MOVEMENT_STATUS status)
    {
        List<PlayerInfo> allPlayers = new List<PlayerInfo>(Team0Players);
        allPlayers.AddRange(Team1Players);
        foreach(PlayerInfo pi in allPlayers)
        {
            if ((pi.Agent.gameObject.activeSelf) &&
                ((DodgeBallAgent_Scout_Waypoint)pi.Agent).currentMovementStatus != status
                )
            {
                if (IS_DEBUG) Debug.Log("MajorStep=" + m_numMajorSteps + "; Agents ready to move = false. AllPlayers=" + allPlayers.Count);
                return false;
            }
        }
        if (IS_DEBUG) Debug.Log("MajorStep=" + m_numMajorSteps + "; Agents ready to move = true. AllPlayers=" + allPlayers.Count);
        return true;
    }

    public void SetAllAgentsReadyToMove()
    {
        List<PlayerInfo> allPlayers = new List<PlayerInfo>(Team0Players);
        allPlayers.AddRange(Team1Players);
        foreach (PlayerInfo pi in allPlayers)
        {
            ((DodgeBallAgent_Scout_Waypoint)pi.Agent).currentMovementStatus =
                DodgeBallAgent_Scout_Waypoint.WAYPOINT_MOVEMENT_STATUS.WAITING_TO_MOVE;
        }

        // Reset state to initial
        foreach (DodgeBallGroup group in groups)
        {
            group.currentScenarioState = SCENARIO_STATE.INITIAL;
        }

        m_numMajorSteps++;
        if (IS_DEBUG) Debug.Log("<color=red>major step incremented to " + m_numMajorSteps + "</color>");
    }

    /// <summary>
    /// Agents call this. Then the controller checks if all agents are done and proceeds.
    /// </summary>
    public void CheckIfAllMovesDone()
    {
        if (DoAllAgentsHaveMovementStatus(DodgeBallAgent_Scout_Waypoint.WAYPOINT_MOVEMENT_STATUS.FINISHED_MOVING))
        {
            /*StartCoroutine(_CheckIfAllMovesDone());
            IEnumerator _CheckIfAllMovesDone()
            {
                //yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(2f);
                SetAllAgentsReadyToMove();
            }*/
            SetAllAgentsReadyToMove();
        }
    }

    protected override void ResetScene()
    {
        if (IS_DEBUG) Debug.Log("<color=red>RESET SCENE. numMajorSteps=" + m_numMajorSteps + "</color>");
        m_EpisodeCount++;

        StopAllCoroutines();

        //Clear win screens and start countdown
        if (ShouldPlayEffects)
        {
            ResetPlayerUI();
            if (CurrentSceneType == SceneType.Game)
            {
                StartCoroutine(GameCountdown());
            }
        }
        m_NumberOfBluePlayersRemaining = Team0Players.Count;
        m_NumberOfPurplePlayersRemaining = Team1Players.Count;

        m_GameEnded = false;
        m_NumFlagDrops = 0;
        m_ResetTimer = 0;
        GameMode = getCurrentGameMode();

        GetAllParameters();

        //print($"Resetting {gameObject.name}");
        //Reset Balls by deleting them and reinitializing them
        int ballSpawnNum = 0;
        int ballSpawnedInPosition = 0;
        for (int ballNum = 0; ballNum < AllBallsList.Count; ballNum++)
        {
            var item = AllBallsList[ballNum];
            if (item == null) continue;

            item.transform.SetParent(null);
            item.BallIsInPlay(false);
            item.rb.velocity = Vector3.zero;
            item.gameObject.SetActive(true);
            var spawnPosition = BallSpawnPositions[ballSpawnNum].position + Random.insideUnitSphere * BallSpawnRadius;
            item.gameObject.GetComponent<DodgeBall_Extended>().spawnPosition = spawnPosition;
            item.transform.position = spawnPosition;
            item.SetResetPosition(spawnPosition);
            ((DodgeBall_Extended)item).ballState = DodgeBall_Extended.BallState.SPAWN;
            ((DodgeBall_Extended)item).BallCollider.gameObject.GetComponent<Renderer>().enabled = true;
            ((DodgeBall_Extended)item).SetPickupMode(((DodgeBall_Extended)item).ballState);

            ballSpawnedInPosition++;

            if (ballSpawnedInPosition >= NumberOfBallsToSpawn)
            {
                ballSpawnNum++;
                ballSpawnedInPosition = 0;
            }

            ((DodgeBall_Extended)item).StopTimedRespawn();
        }

        //Reset the agents
        foreach (var item in Team0Players)
        {
            item.Agent.HitPointsRemaining = BlueTeamMaxHP; //PlayerMaxHitPoints;
            item.Agent.gameObject.SetActive(true);
            item.Agent.ResetAgent();
            m_Team0AgentGroup.RegisterAgent(item.Agent);
        }
        foreach (var item in Team1Players)
        {
            item.Agent.HitPointsRemaining = PurpleTeamMaxHP; //PlayerMaxHitPoints;
            item.Agent.gameObject.SetActive(true);
            item.Agent.ResetAgent();
            m_Team1AgentGroup.RegisterAgent(item.Agent);
        }

        if (GameMode == GameModeType.CaptureTheFlag)
        {
            resetTeam1Flag();
            resetTeam0Flag();
        }
        else
        {
            Team0Flag.gameObject.SetActive(false);
            Team1Flag.gameObject.SetActive(false);
        }

        SetActiveLosers(blueLosersList, 0);
        SetActiveLosers(purpleLosersList, 0);

        foreach(DodgeBallGroup group in groups)
        {
            group.currentScenarioState = SCENARIO_STATE.INITIAL;
            group.SetAssistanceSignal(false);
            group.SetShelterBound(false);
        }

        m_numMajorSteps = 0;
    }

    //Call this method when a player is hit by a dodgeball
    public override void PlayerWasHit(DodgeBallAgent hit, DodgeBallAgent thrower)
    {
        //SET AGENT/TEAM REWARDS HERE
        int hitTeamID = hit.teamID;
        int throwTeamID = thrower?.teamID ?? -1;
        var HitAgentGroup = hitTeamID == 1 ? m_Team1AgentGroup : m_Team0AgentGroup;
        var ThrowAgentGroup = hitTeamID == 1 ? m_Team0AgentGroup : m_Team1AgentGroup;
        float hitBonus = GameMode == GameModeType.Elimination ? EliminationHitBonus : CTFHitBonus;

        ((DodgeBallAgent_Scout_Waypoint)hit).DamageTakenThisStep += projectileDamage;
        ((DodgeBallAgent_Scout_Waypoint)thrower).DamageGivenThisStep += projectileDamage;

        // Always drop the flag
        if (DropFlagImmediately)
        {
            dropFlagIfHas(hit, thrower);
        }

        if (hit.HitPointsRemaining <= projectileDamage) //FINAL HIT
        {
            hit.HitPointsRemaining = 0; // Force it to 0
            if (GameMode == GameModeType.CaptureTheFlag)
            {
                hit.StunAndReset();
                dropFlagIfHas(hit, thrower);
            }
            else if (GameMode == GameModeType.Elimination)
            {
                m_NumberOfBluePlayersRemaining -= hitTeamID == 0 ? 1 : 0;
                m_NumberOfPurplePlayersRemaining -= hitTeamID == 1 ? 1 : 0;
                // The current agent was just killed and is the final agent
                if (IS_DEBUG) Debug.Log("m_NumberOfPurplePlayersRemaining =" + m_NumberOfPurplePlayersRemaining);
                if (m_NumberOfBluePlayersRemaining < Team0Players.Count || // If one blue dies end episode
                    m_NumberOfPurplePlayersRemaining == 0 || 
                    hit.gameObject == PlayerGameObject)
                {
                    /*
                    ThrowAgentGroup.AddGroupReward(2.0f - m_TimeBonus * (m_ResetTimer / MaxEnvironmentSteps));
                    HitAgentGroup.AddGroupReward(-1.0f);
                    */
                    SetEndOfEpisodeRewards(ThrowAgentGroup, HitAgentGroup);
                    ThrowAgentGroup.EndGroupEpisode();
                    HitAgentGroup.EndGroupEpisode();
                    print($"Team {throwTeamID} Won");
                    hit.DropAllBalls();
                    if (ShouldPlayEffects)
                    {
                        // Don't poof the last agent
                        StartCoroutine(TumbleThenPoof(hit, false));
                    }
                    EndGame(throwTeamID);
                }
                // The current agent was just killed but there are other agents
                else
                {
                    // Additional effects for game mode
                    if (ShouldPlayEffects)
                    {
                        StartCoroutine(TumbleThenPoof(hit));
                    }
                    else
                    {
                        hit.gameObject.SetActive(false);
                    }
                    hit.DropAllBalls();
                }
            }
        }
        else
        {
            hit.HitPointsRemaining -= projectileDamage;
            Debug.Log(hit.gameObject.name + " hit for " + projectileDamage + " DMG. HitPointsRemaining=" + hit.HitPointsRemaining);
            thrower.AddReward(hitBonus);
        }
    }

    public void AgentDied(DodgeBallAgent deadAgent)
    {
        //SET AGENT/TEAM REWARDS HERE
        int hitTeamID = deadAgent.teamID;
        int throwTeamID = deadAgent.teamID == 0 ? 1 : 0;
        var HitAgentGroup = hitTeamID == 1 ? m_Team1AgentGroup : m_Team0AgentGroup;
        var ThrowAgentGroup = hitTeamID == 1 ? m_Team0AgentGroup : m_Team1AgentGroup;
        float hitBonus = GameMode == GameModeType.Elimination ? EliminationHitBonus : CTFHitBonus;


        int liveBlue = 0;
        foreach (PlayerInfo pi in Team0Players)
        {
            if (pi.Agent.HitPointsRemaining > 0)
                liveBlue++;
        }
        int livePurple = 0;
        foreach (PlayerInfo pi in Team1Players)
        {
            if (pi.Agent.HitPointsRemaining > 0)
                livePurple++;
        }
        m_NumberOfBluePlayersRemaining = liveBlue;
        m_NumberOfPurplePlayersRemaining = livePurple;
        // The current agent was just killed and is the final agent
        if (IS_DEBUG) Debug.Log("m_NumberOfPurplePlayersRemaining =" + m_NumberOfPurplePlayersRemaining);
        if (m_NumberOfBluePlayersRemaining < Team0Players.Count || // If one blue dies end episode
            m_NumberOfPurplePlayersRemaining == 0)
        {
            /*
            ThrowAgentGroup.AddGroupReward(2.0f - m_TimeBonus * (m_ResetTimer / MaxEnvironmentSteps));
            HitAgentGroup.AddGroupReward(-1.0f);
            */
            SetEndOfEpisodeRewards(ThrowAgentGroup, HitAgentGroup);
            ThrowAgentGroup.EndGroupEpisode();
            HitAgentGroup.EndGroupEpisode();
            print($"Team {throwTeamID} Won");
            deadAgent.DropAllBalls();
            if (ShouldPlayEffects)
            {
                // Don't poof the last agent
                StartCoroutine(TumbleThenPoof(deadAgent, false));
            }
            EndGame(deadAgent.teamID);
        }
        // The current agent was just killed but there are other agents
        else
        {
            // Additional effects for game mode
            if (ShouldPlayEffects)
            {
                StartCoroutine(TumbleThenPoof(deadAgent));
            }
            else
            {
                deadAgent.gameObject.SetActive(false);
            }
            deadAgent.DropAllBalls();
        }

    }

    private void SetEndOfEpisodeRewards(SimpleMultiAgentGroup winGroup, SimpleMultiAgentGroup loseGroup)
    {
        /// ToDo
        /// * No delay reward if agents die
        /// * No group rewards
        /// * Calculate on a per-step/per-agent basis
        /// * Factor in red being in-sight by blue
        /// * Grant at end of episode

        //int tier0 = 17, tier1 = 2 * tier0, max_delay_reward = 30; // tiers are for # times seen
        //                                                          // 0 - 12: 0 reward
        //                                                          // 13 - 24: linear -- 0 to 30. e.g., step 15: accumulate 30*(1)/(26-13) reward only if ANY red is in sight of a blue agent
        //                                                          // >24: 30
        int tier0 = 17, tier1 = 31, max_delay_reward = 30;

        bool anyBlueAtShelter = AreAnyBlueAgentsAtShelter();
        bool areBlueAtPeaks = AreBlueAgentsAtPeaks();
        float delayReward = 0;
        if ((m_NumberOfBluePlayersRemaining < Team0Players.Count) ||
            anyBlueAtShelter
            )
        {
            //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>Max Reward! #blue remaining=" + m_NumberOfBluePlayersRemaining  + "; AnyAtShelter? " + anyBlueAtShelter + "</color>");
            delayReward = max_delay_reward;
            m_Team1AgentGroup.AddGroupReward(max_delay_reward);
        }
        else
        {
            int counter = m_numMajorSteps; //alt: run on per-agent with numtimesseenbyopponent
            if (counter <= tier0)
            {
                // No reward
                //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>GroupDamageBasedReward=0</color>");
            }
            else if (counter <= tier1)
            {
                delayReward = max_delay_reward * (counter - tier0) / (tier1 - tier0);
                m_Team1AgentGroup.AddGroupReward(max_delay_reward * (counter - tier0) / (tier1 - tier0));
                //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>GroupDamageBasedReward=" + (max_delay_reward * (m_numMajorSteps - tier0) / (tier1 - tier0)) + "</color>");
            }
            else
            {
                delayReward = max_delay_reward;
                m_Team1AgentGroup.AddGroupReward(max_delay_reward);
                //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>GroupDamageBasedReward=" + max_delay_reward + "</color>");
            }
        }

        /*foreach (PlayerInfo purple_pi in Team1Players)
        {
            DodgeBallAgent_Scout_Waypoint pa = ((DodgeBallAgent_Scout_Waypoint)purple_pi.Agent);
            int counter = pa.NumTimesSeenByOpponent; //m_numMajorSteps; //alt: run on per-agent with numtimesseenbyopponent
            if (counter <= tier0)
            {
                // No reward
                if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>GroupDamageBasedReward=0</color>");
            }
            else if (counter <= tier1)
            {
                pa.AddReward(max_delay_reward * (counter - tier0) / (tier1 - tier0));
                if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>GroupDamageBasedReward=" + (max_delay_reward * (m_numMajorSteps - tier0) / (tier1 - tier0)) + "</color>");
            }
            else
            {
                pa.AddReward(max_delay_reward);
                if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>GroupDamageBasedReward=" + max_delay_reward + "</color>");
            }
        }*/

        int max_health_reward = 30;
        float[] healthRewards = new float[Team1Players.Count];
        int index = 0;
        // Health reward 
        foreach (PlayerInfo purple_pi in Team1Players)
        {
            DodgeBallAgent_Scout_Waypoint purpleAgent = ((DodgeBallAgent_Scout_Waypoint)purple_pi.Agent);
            if (purpleAgent.HitPointsRemaining > 0)
            {
                healthRewards[index] = max_health_reward * purpleAgent.HitPointsRemaining / purpleAgent.NumberOfTimesPlayerCanBeHit;
                //purpleAgent.AddReward(purpleAgent.NumTimesSeenByOpponent / m_numMajorSteps);
                purpleAgent.AddReward(max_health_reward * purpleAgent.HitPointsRemaining / purpleAgent.NumberOfTimesPlayerCanBeHit);
                //if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF>" + purpleAgent.name + ": HealthReward=" + (max_health_reward * purpleAgent.HitPointsRemaining / purpleAgent.NumberOfTimesPlayerCanBeHit) + "</color>");
            }
            else
            {
                healthRewards[index] = 0f;
            }
            // no bonus yet for all agents being alive -- team based group reward in future
            index++;
        }

        float[] cumRew = new float[Team1Players.Count];
        for (int i = 0; i < Team1Players.Count; ++i)
        {
            DodgeBallAgent_Scout_Waypoint purpleAgent = ((DodgeBallAgent_Scout_Waypoint)Team1Players[i].Agent);
            cumRew[i] = purpleAgent.GetCumulativeReward();
        }

        bool anyAssist = false;
        bool anyRetreat = false;
        foreach (DodgeBallGroup group in groups)
        {
            anyAssist = anyAssist || group.GetAssistanceSignal();
            anyRetreat = anyRetreat || group.GetShelterBound();
        }

        if (REWARD_DEBUG) Debug.Log("E" + m_EpisodeCount + "S" + m_numMajorSteps + ": <color=#FAF># blue alive=" + m_NumberOfBluePlayersRemaining + "; # purple/red alive=" + m_NumberOfPurplePlayersRemaining + "; Any assist signal? " + anyAssist + "; Any Retreat? " + anyRetreat + "; Delay Reward = " + delayReward + "; Any blue at Shelter? " + anyBlueAtShelter + "; Blue at peaks? " + areBlueAtPeaks + "; Health Rewards: " + string.Join(", ", healthRewards) + "; cumulative rewards: " + string.Join(", ", cumRew) + " @ "+ Time.realtimeSinceStartup + "</color>");
#if UNITY_EDITOR
        tabulatedData += m_EpisodeCount + "\t" + m_numMajorSteps + "\t" + m_NumberOfBluePlayersRemaining + "\t" + m_NumberOfPurplePlayersRemaining + "\t" + anyAssist + "\t" + anyRetreat + "\t" + delayReward + "\t" + anyBlueAtShelter + "\t" + areBlueAtPeaks + "\t" + string.Join(", ", healthRewards) + "\t" + string.Join(", ", cumRew) + "\n";
        Debug.Log(tabulatedData);
#endif
    }


    public virtual int GetIndexOfDodgeball(DodgeBall db)
    {
        return AllBallsList.IndexOf(db);
    }

    public virtual List<DodgeBallAgent_Scout_Waypoint> GetAgentsAtWaypoint(WaypointMono waypoint, bool onlyGetNonMoving = false)
    {
        List<DodgeBallAgent_Scout_Waypoint> agentsOnWaypoint = new List<DodgeBallAgent_Scout_Waypoint>();

        List<PlayerInfo> allPlayers = new List<PlayerInfo>(Team0Players);
        allPlayers.AddRange(Team1Players);
        foreach (PlayerInfo pi in allPlayers)
        {
            if ((pi.Agent.gameObject.activeSelf) &&
                (((DodgeBallAgent_Scout_Waypoint)pi.Agent).currentWaypoint.waypoint == waypoint) &&
                (onlyGetNonMoving ? (((DodgeBallAgent_Scout_Waypoint)pi.Agent).currentMovementStatus != DodgeBallAgent_Scout_Waypoint.WAYPOINT_MOVEMENT_STATUS.MOVING) : true)
                )
            {
                agentsOnWaypoint.Add((DodgeBallAgent_Scout_Waypoint)pi.Agent);
            }
        }
        return agentsOnWaypoint;
    }

    public virtual bool AreBlueAgentsAtPeaks()
    {
        List<WaypointMono> tmp_flagWaypoints = new List<WaypointMono>(flagWaypoints);
        int numFlagWaypoints = flagWaypoints.Count;
        int numAliveAgents = 0;
        foreach (PlayerInfo pi in Team0Players)
        {
            if (pi.Agent.HitPointsRemaining > 0)
            {
                numAliveAgents++;
            }
        }

        for (int i = 0; i < numFlagWaypoints; ++i)
        {
            WaypointMono flagWaypoint = flagWaypoints[i];

            foreach (PlayerInfo pi in Team0Players)
            {
                if (pi.Agent.HitPointsRemaining > 0)
                {
                    PeakFlagOccupancyStateData occupancyData = GetOccupancyData((DodgeBallAgent_Scout_Waypoint)pi.Agent);
                    if (!occupancyData.isSelfOnFlagSpot)
                        return false; // One of them is not on the peak and is alive, so automatically know not at peaks
                    else if (occupancyData.occupiedFlagSpotID == i)
                        tmp_flagWaypoints.Remove(flagWaypoint);
                }
            }
        }
        //Debug.Log("<color=orange>AreBlueAgentsAtPeaks(): numAliveAgents=" + numAliveAgents + "; tmp_flagWaypoints.Count=" + tmp_flagWaypoints.Count + "; numFlagWaypoints=" + numFlagWaypoints + "; numAliveAgents=" + numAliveAgents + "</color>");
        return (numAliveAgents > 0) && (tmp_flagWaypoints.Count == (numFlagWaypoints - numAliveAgents));

        /*
        int numAliveAgents = 0;
        int numAgentsOnPeaks = 0;        
        foreach (PlayerInfo pi in Team0Players)
        {
            if (pi.Agent.HitPointsRemaining > 0)
            {
                numAliveAgents++;
                if (GetOccupancyData((DodgeBallAgent_Scout_Waypoint)pi.Agent).isSelfOnFlagSpot)
                {
                    numAgentsOnPeaks++;
                }
            }
        }
        return ((numAliveAgents > 0) && (numAliveAgents == numAgentsOnPeaks));
        */
    }

    public virtual bool AreAnyBlueAgentsAtShelter()
    {
        foreach(DodgeBallGroup group in groups)
        {
            int numAliveAgents = 0;
            foreach(DodgeBallAgent_Scout_Waypoint agent in group.agents)
            {
                if (agent.HitPointsRemaining > 0)
                    numAliveAgents++;
            }
            if (numAliveAgents > 0)
            {
                if (group.GetShelterBound())
                {
                    foreach (DodgeBallAgent_Scout_Waypoint agent in group.agents)
                    {
                        if (AdjacencyMatrixUtility.GetClosestWaypointToPosition(agent.transform.position) ==
                            group.safetyWaypoint
                            )
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    continue;
                }
            }
            else
            {
                continue;
            }
        }
        return false;
    }

    public virtual PeakFlagOccupancyStateData GetOccupancyData(DodgeBallAgent_Scout_Waypoint agent)
    {
        int numFlagWaypoints = flagWaypoints.Count;
        //int teamID = agent.teamID; // 0 is blue, 1 is purple

        // Setup Data
        PeakFlagOccupancyStateData data = new PeakFlagOccupancyStateData();
        data.isSelfOnFlagSpot = false;
        data.occupancyStates = new PEAKFLAG_OCCUPANCY_STATE[numFlagWaypoints];
        for (int i = 0; i < numFlagWaypoints; ++i)
        {
            data.occupancyStates[i] = PEAKFLAG_OCCUPANCY_STATE.EMPTY;
        }

        // Iterate through flag waypoints
        for (int i=0; i<numFlagWaypoints; ++i)
        {
            WaypointMono flagWaypoint = flagWaypoints[i];
            List<DodgeBallAgent_Scout_Waypoint> waypointAgents = GetAgentsAtWaypoint(flagWaypoint, true);
            foreach (DodgeBallAgent_Scout_Waypoint waypointAgent in waypointAgents)
            {
                if (waypointAgent.HitPointsRemaining > 0)
                {
                    if (waypointAgent == agent)
                    {
                        data.isSelfOnFlagSpot = true;
                        data.occupiedFlagSpotID = i;
                    }
                    PEAKFLAG_OCCUPANCY_STATE waypointAgentTeam = GetAgentColor(waypointAgent);
                    switch (data.occupancyStates[i])
                    {
                        case PEAKFLAG_OCCUPANCY_STATE.EMPTY:
                            data.occupancyStates[i] = waypointAgentTeam;
                            break;
                        case PEAKFLAG_OCCUPANCY_STATE.BLUE:
                            if (waypointAgentTeam == PEAKFLAG_OCCUPANCY_STATE.PURPLE)
                                data.occupancyStates[i] = PEAKFLAG_OCCUPANCY_STATE.BOTH;
                            break;
                        case PEAKFLAG_OCCUPANCY_STATE.PURPLE:
                            if (waypointAgentTeam == PEAKFLAG_OCCUPANCY_STATE.BLUE)
                                data.occupancyStates[i] = PEAKFLAG_OCCUPANCY_STATE.BOTH;
                            break;
                        case PEAKFLAG_OCCUPANCY_STATE.BOTH:
                            // Do nothing
                            break;
                    }
                }
            }
        }

        return data;
    }

    public PEAKFLAG_OCCUPANCY_STATE GetAgentColor(DodgeBallAgent_Scout_Waypoint agent)
    {
        return agent.teamID == 0 ? PEAKFLAG_OCCUPANCY_STATE.BLUE : PEAKFLAG_OCCUPANCY_STATE.PURPLE;
    }

    public void CheckBlueAgentsHaveOccupiedPeaks()
    {
        if (AreBlueAgentsAtPeaks())
        {
            var WinAgentGroup = m_Team0AgentGroup;
            var LoseAgentGroup = m_Team1AgentGroup;
            /*WinAgentGroup.AddGroupReward(2.0f - m_TimeBonus * (float)m_ResetTimer / MaxEnvironmentSteps);
            LoseAgentGroup.AddGroupReward(-1.0f);*/
            SetEndOfEpisodeRewards(WinAgentGroup, LoseAgentGroup);
            WinAgentGroup.EndGroupEpisode();
            LoseAgentGroup.EndGroupEpisode();
            print($"Team 0 (BLUE) Won");
            //m_StatsRecorder.Add("Environment/Flag Drops Per Ep", m_NumFlagDrops);
            // Confetti animation
            if (ShouldPlayEffects)
            {
                var winningBase = Team0Base;
                var particles = winningBase.GetComponentInChildren<ParticleSystem>();
                if (particles != null)
                {
                    particles.Play();
                }
            }
            EndGame(0, 0.0f);
        }
    }

    public void CheckBlueAgentsHaveOccupiedShelter()
    {
        if (AreAnyBlueAgentsAtShelter())
        {
            var WinAgentGroup = m_Team1AgentGroup;
            var LoseAgentGroup = m_Team0AgentGroup;
            SetEndOfEpisodeRewards(WinAgentGroup, LoseAgentGroup);
            WinAgentGroup.EndGroupEpisode();
            LoseAgentGroup.EndGroupEpisode();
            print($"Team 1 (PURPLE) Won");
            //m_StatsRecorder.Add("Environment/Flag Drops Per Ep", m_NumFlagDrops);
            // Confetti animation
            if (ShouldPlayEffects)
            {
                var winningBase = Team0Base;
                var particles = winningBase.GetComponentInChildren<ParticleSystem>();
                if (particles != null)
                {
                    particles.Play();
                }
            }
            EndGame(1, 0.0f);
        }
    }

    #region DECISION_TREE

    public DodgeBallAgent_Action CheckState(DodgeBallGroup group)
    {
        DodgeBallAgent_Action groupAction = new DodgeBallAgent_Action();

        if (group == null)
        {
            Debug.Log("CheckState. Group is NULL");
            groupAction.movementDirection = FourConnectedNode.DIRECTION.NONE;
            groupAction.facingDirection = FourConnectedNode.DIRECTION.NONE;
            return groupAction;
        }

        group.SetAssistanceSignal(false); // Turn off assistance at each invocation
        string groupSteps = GetGroupSteps(group);
        if (IS_DEBUG) Debug.Log(group.agents[0].name + ": " + groupSteps + " step: CheckState: " + group.currentScenarioState + "\n"
            + "; health=" + GetGroupHealth(group) + "\n"
            + "; Probability = " + ProbabilityFromOppforAtPoint(group.GetCurrentWaypoint()) + "\n"
            + "; Is in dangerous zone? " + (IsInZone(dangerousProbability, group.GetCurrentWaypoint())) + "\n"
            + "; Is in cautious zone? " + (IsInZone(cautiousProbability, group.GetCurrentWaypoint())) + "\n"
            + "; Do other groups need assistance? " + DoOtherGroupsNeedAssistance(group) + "\n"
            + "; IsGroupEngaged? " + IsGroupEngaged(group) + "\n"
            + "; Opponent in sight? " + IsOpponentInSight(group) + "\n"
            + " @ " + Time.realtimeSinceStartup);

        switch (group.currentScenarioState)
        {
            case SCENARIO_STATE.INITIAL:
                if (GetGroupHealth(group) < 0.25f)
                {
                    group.currentScenarioState = SCENARIO_STATE.RETREAT;
                }
                else
                {
                    group.currentScenarioState = SCENARIO_STATE.FORWARD;
                }
                return CheckState(group);
            case SCENARIO_STATE.RETREAT:
                if (IsInZone(dangerousProbability, group.GetCurrentWaypoint())) // inside dangerous zone
                {
                    SetGroupSpeed(group, false);
                }
                else
                {
                    SetGroupSpeed(group, true);
                }
                group.SetShelterBound(true);
                groupAction.movementDirection = MoveToWaypoint(group, group.safetyWaypoint);
                return groupAction;
            case SCENARIO_STATE.FORWARD:
                if (DoOtherGroupsNeedAssistance(group)
                    && !IsGroupEngaged(group)
                    )
                {
                    group.currentScenarioState = SCENARIO_STATE.ASSIST;
                }
                else
                {
                    group.currentScenarioState = SCENARIO_STATE.MOVE_TO_TARGET;
                }
                return CheckState(group);
            case SCENARIO_STATE.ASSIST:
                if ((GetGroupHealth(group) > 0.75f) &&
                    !IsInZone(cautiousProbability, group.GetCurrentWaypoint()))
                {
                    SetGroupSpeed(group, true);
                }
                else
                {
                    SetGroupSpeed(group, false);
                }
                //group.SetShelterBound(false);
                if (group.GetTarget() != null)
                    groupAction.movementDirection = MoveToWaypoint(group, group.GetTarget().currentWaypoint.waypoint);
                else
                    groupAction.movementDirection = MoveToWaypoint(group, GetOtherGroups(group)[0].GetCurrentWaypoint());
                return groupAction;
            case SCENARIO_STATE.MOVE_TO_TARGET:
                if (IsOpponentInSight(group))
                {
                    group.currentScenarioState = SCENARIO_STATE.OPFOR_IN_SIGHT;
                    return CheckState(group);
                }
                else
                {
                    SetGroupSpeed(group, true);
                    //group.SetShelterBound(false);
                    groupAction.movementDirection = MoveToWaypoint(group, group.goalWaypoint);
                    return groupAction;
                }
            case SCENARIO_STATE.OPFOR_IN_SIGHT:
                if (GetGroupHealth(group) >= 0.75f)
                {
                    SetGroupSpeed(group, false);
                    groupAction.movementDirection = MoveToWaypoint(group, group.goalWaypoint);
                    return groupAction;
                }
                else
                {
                    group.SetAssistanceSignal(true);
                    group.currentScenarioState = SCENARIO_STATE.SEND_ASSIST_SIGNAL;
                    return CheckState(group);
                }
            case SCENARIO_STATE.SEND_ASSIST_SIGNAL:
                if (GetGroupHealth(group) < 0.50f)
                {
                    groupAction.movementDirection = FourConnectedNode.DIRECTION.NONE;
                    groupAction.facingDirection = FourConnectedNode.DIRECTION.NONE;
                    return groupAction;
                }
                else
                {
                    SetGroupSpeed(group, false);
                    //group.SetShelterBound(false);
                    groupAction.movementDirection = MoveToWaypoint(group, group.goalWaypoint);
                    return groupAction;
                }
            default:
                if (IS_DEBUG) Debug.LogError("Unknown state:  " + group.currentScenarioState);
                DodgeBallAgent_Action stationaryAction = new DodgeBallAgent_Action();
                stationaryAction.movementDirection = FourConnectedNode.DIRECTION.NONE;
                stationaryAction.facingDirection = FourConnectedNode.DIRECTION.NONE;
                return stationaryAction;
        }
    }

    public float GetGroupHealth(DodgeBallGroup group)
    {
        float currentHp = 0f;
        float maxHp = 0f;
        foreach (DodgeBallAgent_Scout_Waypoint agent in group.agents)
        {
            currentHp += agent.HitPointsRemaining;
            maxHp += agent.NumberOfTimesPlayerCanBeHit;
        }
        if (IS_DEBUG) Debug.Log(GetGroupSteps(group) + "steps: GetGroupHealth for group " + group.index + "; #agents=" + group.agents.Count + ". curr=" + currentHp + "; maxHp=" + maxHp);
        return currentHp <= 0f ? 0f : currentHp / maxHp;
    }

    public DodgeBallGroup GetGroupForAgent(DodgeBallAgent_Scout_Waypoint agent)
    {
        foreach(DodgeBallGroup group in groups)
        {
            if (group.agents.Contains(agent))
                return group;
        }
        return null;
    }

    public float GetDistanceBetweenGroups(DodgeBallGroup groupA, DodgeBallGroup groupB)
    {
        float minDistance = float.MaxValue;
        foreach(DodgeBallAgent_Scout_Waypoint agentA in groupA.agents)
        {
            foreach (DodgeBallAgent_Scout_Waypoint agentB in groupB.agents)
            {
                float d = Vector3.Distance(agentA.transform.position, agentB.transform.position);
                minDistance = Mathf.Min(minDistance, d);
            }
        }
        return minDistance;
    }

    public void SetGroupSpeed(DodgeBallGroup group, bool isFast)
    {
        foreach (DodgeBallAgent_Scout_Waypoint agent in group.agents)
        {
            agent.ChangeMovementSpeed(isFast);
        }
    }

    public bool IsOpponentInSight(DodgeBallGroup group)
    {
        foreach (DodgeBallAgent_Scout_Waypoint agent in group.agents)
        {
            if (agent.NumberOfOpponentsInSight() > 0)
                return true;
        }
        return false;
    }

    public List<DodgeBallGroup> GetOtherGroups(DodgeBallGroup groupToExclude)
    {
        List<DodgeBallGroup> allGroups = new List<DodgeBallGroup>(groups);
        allGroups.Remove(groupToExclude);
        return allGroups;
    }

    public bool DoOtherGroupsNeedAssistance(DodgeBallGroup thisGroup)
    {
        List<DodgeBallGroup> otherGroups = GetOtherGroups(thisGroup);
        foreach(DodgeBallGroup dbg in otherGroups)
        {
            if (dbg.isSendingAssistanceSignal)
                return true;
        }
        return false;
    }

    public FourConnectedNode.DIRECTION MoveToWaypoint(DodgeBallGroup group, WaypointMono waypoint)
    {
        if (IS_DEBUG) Debug.Log(GetGroupSteps(group) + "steps: MoveToWaypoint: " + waypoint?.gameObject.name ?? "NULL");
        if (waypoint == null) return FourConnectedNode.DIRECTION.NONE;
        foreach(DodgeBallAgent_Scout_Waypoint agent in group.agents)
        {
            if (agent.HitPointsRemaining > 0)
            {
                WaypointMono[] route = AdjacencyMatrixUtility.GetRoute(agent.currentWaypoint.waypoint, waypoint);
                if (route == null)
                {
                    if (IS_DEBUG) Debug.Log(GetGroupSteps(group) + "steps: MoveToWaypoint:  ROUTE is NULL. " + agent.currentWaypoint.waypoint.name + " -> " + waypoint.name);
                    continue;
                }
                if (route.Length <= 1)
                {
                    //agent.MoveToNextWaypoint(0);
                    if (IS_DEBUG) Debug.Log(GetGroupSteps(group) + " steps: MoveToWaypoint:  ROUTE is of length=" + route.Length + ". " + agent.currentWaypoint.waypoint.name + " -> " + waypoint.name);
                    return FourConnectedNode.DIRECTION.NONE;
                }
                else
                {
                    //agent.MoveToNextWaypoint(route[1]);
                    if (IS_DEBUG) Debug.Log(/*GetGroupSteps(group) +*/ " steps: MoveToWaypoint: Next=" + (route[1]?.gameObject.name ?? "NULL") + ". " + agent.currentWaypoint.waypoint.name + "-> " + waypoint.name);
                    return (FourConnectedNode.DIRECTION)agent.GetDirectionFromWaypoint(route[1]);
                }
            }
        }
        return FourConnectedNode.DIRECTION.NONE;
    }

    public bool IsInZone(float probability, WaypointMono waypoint)
    {
        return ProbabilityFromOppforAtPoint(waypoint) < probability;
    }

    public float ProbabilityFromOppforAtPoint(WaypointMono measuredWaypoint)
    {
        List<float> probabilities = new List<float>();
        foreach(PlayerInfo oppforPlayerInfo in Team1Players)
        {
            DodgeBallAgent_Scout_Waypoint agent = (DodgeBallAgent_Scout_Waypoint)oppforPlayerInfo.Agent;
            if (agent.HitPointsRemaining > 0)
            {
                WaypointMono currwp = agent.currentWaypoint.waypoint;
                probabilities.Add(AdjacencyMatrixUtility.GetProbability(currwp, true, measuredWaypoint, true));
            }
        }
        float finalProb = 0f;
        foreach(float probability in probabilities)
        {
            finalProb = Mathf.Max(finalProb, probability);
        }
        return finalProb;
    }

    public bool IsGroupEngaged(DodgeBallGroup group)
    {
        foreach(DodgeBallAgent_Scout_Waypoint agent in group.agents)
        {
            if (agent.IsEngaged())
            {
                return true;
            }
        }
        return false;
    }

    public string GetGroupSteps(DodgeBallGroup group)
    {
        string output = string.Empty;
        foreach(DodgeBallAgent_Scout_Waypoint agent in group.agents)
        {
            if (!string.IsNullOrEmpty(output))
            {
                output += ",";
            }
            output += agent.StepCount.ToString();
        }
        return output;
    }

    #endregion

    // Get the game mode. Use the set one in the dropdown, unles overwritten by
    // environment parameters.
    protected override GameModeType getCurrentGameMode()
    {
        float isCTFparam = MLAgentsUtility.GetWithDefault("is_capture_the_flag", (float)GameMode, (float)GameModeType.Elimination);
        GameModeType newGameMode = isCTFparam > 0.5f ? GameModeType.CaptureTheFlag : GameModeType.Elimination;
        return newGameMode;
    }

    protected override void GetAllParameters()
    {
        //Set time bonus to 1 if Elimination, 0 if CTF
        float defaultTimeBonus = GameMode == GameModeType.CaptureTheFlag ? 0.0f : 1.0f;
        m_TimeBonus = MLAgentsUtility.GetWithDefault("time_bonus_scale", defaultTimeBonus, defaultTimeBonus);
        m_ReturnOwnFlagBonus = MLAgentsUtility.GetWithDefault("return_flag_bonus", 0.0f, 0.0f);
        CTFHitBonus = MLAgentsUtility.GetWithDefault("ctf_hit_reward", CTFHitBonus, CTFHitBonus);
        EliminationHitBonus = MLAgentsUtility.GetWithDefault("elimination_hit_reward", EliminationHitBonus, EliminationHitBonus);
        m_SeenByOpponentReward = MLAgentsUtility.GetWithDefault("seen_by_opponent_step_reward", 0.05f, 0.05f);
        m_StepDamageBasedReward = MLAgentsUtility.GetWithDefault("step_damage_based_reward", 0.25f, 0.25f);

        // Hyperparameters for Scout Mission
        m_MachineGunDamage = MLAgentsUtility.GetWithDefault_int("machine_gun_damage", 10, 10);
    }

    public bool IsInMachineGunZone(WaypointMono waypointToCheck)
    {
        return machineGunZone.Contains(waypointToCheck);
    }
}
