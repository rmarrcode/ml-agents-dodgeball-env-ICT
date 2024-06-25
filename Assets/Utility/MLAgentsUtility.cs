using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
/*using Ride;
using Ride.Entities;
using Ride.Behaviour;
using Ride.WorldState;
using Ride.Movement;*/

#if RIDE_ML_AGENTS
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
#endif

public class PathReplayData
{
    public Vector2? waypointCoordinates = null;
    public List<int> movementList = new List<int>();
    public List<int> directionList = new List<int>();
}

public class MLAgentsUtility : MonoBehaviour
{
    private static bool useJSON =
#if UNITY_EDITOR    
        true;
#else
        false; //true; // True when building for STANDALONE
#endif    

    private static bool DEBUGOVERRIDE =
#if UNITY_EDITOR
        true;
#else
        false; //true; // True when building for STANDALONE
#endif

    public TextAsset distancesIndexed;
    public static Dictionary<string, int> coordinateConversion = new Dictionary<string, int>();

    private static string[] start_pos_red_0 = new string[] {
        "(11,0)",
        "(11,1)",
        "(11,2)",
        "(11,3)",
        "(11,4)",
        "(12,3)",
        "(12,4)",
        "(12,5)",
        "(13,3)",
        "(13,4)",
        "(13,5)",
        "(14,3)",
        "(14,4)",
        "(14,5)",
        "(14,6)",
        "(14,7)"
    };

    private static readonly string[][] start_pos_red = new string[][] 
    {
        start_pos_red_0
    };

    
    /*static string json_src = "'num_red_agents': 1," +
                "'red_maxhealth': 30000," +
                //"'red_random_start_pos': 0," +
                "'bonus_spot': 1007," +
                "'oneHotOverride': 1," +
                "'red_initwaypoint_0': 1006, 'red_initwaypoint_1': 1204," +
                "'red_initwaypoint_2': 1204, 'red_initwaypoint_3': 1204," +
                "'num_blue_teams': 1," +
                "'blue_maxhealth': 100," +
                "'blue_team_path_0': 0," +
                "'max_step': 50," +
                "'mask_actions': 1," +
                "'range_based_step': 0," +
                "'time_penalty': 0," + "'time_penalty_linear': 0," +"'time_penalty_quadratic': 0," +
                "'enemy_seen_reward': 0," +
                "'turn_corner_reward': 0," +
                "'unitydebug': 1, 'rldebug': 1," +
                "'is_survival_win_condition': 1," +
                "'damage_reward_multiplier': 0," +
                "'timescale': 10, 'timeout_seconds': 30," +
                "'red_numammoclips': 0, 'blue_numammoclips': 3," +
                "'size_blue_team': 1";*/                    
    static string json_src = // This can be overriden by a text file
        //TeamCombatArea.GetScenarioSubType() == TeamCombatArea.ScenarioSubType.FIGURE_8_HIDE ?
                "'num_red_agents': 1," +
                "'red_maxhealth': 100," +
		        "'bonus_spot': 1303," +
                "'bonus_spot1': 1203," +
                "'NiR_Step_Bonus': 0," +
        		"'NiS_Step_Bonus': 0," +
                "'iR_or_iS_Step_Bonus': -2," +
		        "'oneHotOverride': 1," +
                "'red_initwaypoint_0': 1407, 'red_initwaypoint_1': 1204," +
                "'red_initwaypoint_2': 1204, 'red_initwaypoint_3': 1204," +
                "'initwaypoint_randompoolsize': 1," +
                "'red_initwaypoint_0r1': 1302," +
                "'red_initwaypoint_0r2': 1202," +
                "'red_initwaypoint_0r3': 1401," +
                "'red_initwaypoint_0r4': 1301," +
                "'red_initwaypoint_0r5': 1201," +
                "'red_initwaypoint_0r6': 1405," +
                "'red_initwaypoint_0r7': 1304," +
                "'red_initwaypoint_0r8': 1102," +
                "'red_initwaypoint_0r9': 1101," +
                "'red_initwaypoint_0r10': 1100," +
                "'num_blue_teams': 1," +
                "'blue_maxhealth': 100," +
                "'blue_team_path_0': 0," +
                "'max_step': 35," +
                "'mask_actions': 1," +
		        "'use_terrain_observations': 0," +
		        "'range_based_step': 0," +
                "'time_penalty': 0, 'time_penalty_linear': 0, 'time_penalty_quadratic': 0," +
		        "'team_lose_reward': 0, 'team_win_base_reward': 0, 'team_win_health_reward_multiplier': 0," +
                "'full_health_bonus': 0," + // Fig8Only
		        "'team_win_isalive_reward': 0, 'team_win_numaliveteammates_reward_multiplier': 0," +
                "'unitydebug': 0, 'rldebug': 1," +
                "'is_survival_win_condition': 1," +
                "'damage_reward_multiplier': 0," +
                "'timescale': 1, 'timeout_seconds': 30," +
                "'agent_speed': 350," +
                "'red_numammoclips': 0, 'blue_numammoclips': 0," +
                "'size_blue_team': 1,'WriteLogs': 1";
            // NOTE: This (^^^) can be overriden by a text file


/*                :
                "'num_red_agents': 1," +
                "'red_maxhealth': 30000," +
		        "'bonus_spot': 1007," +
		        "'oneHotOverride': 1," +
                "'red_initwaypoint_0': 1006, 'red_initwaypoint_1': 1204," +
                "'red_initwaypoint_2': 1204, 'red_initwaypoint_3': 1204," +
                "'num_blue_teams': 1," +
                "'blue_maxhealth': 100," +
                "'blue_team_path_0': 0," +
                "'max_step': 5," +
                "'mask_actions': 1," +
		        "'use_terrain_observations': 0," +
		        "'range_based_step': 0," +
                "'time_penalty': 0, 'time_penalty_linear': 0, 'time_penalty_quadratic': 0," +
		        "'team_lose_reward': 0, 'team_win_base_reward': 0, 'team_win_health_reward_multiplier': 0," +
		        "'team_win_isalive_reward': 0, 'team_win_numaliveteammates_reward_multiplier': 0," +
                "'unitydebug': 0, 'rldebug': 1," +
                "'is_survival_win_condition': 1," +
                "'damage_reward_multiplier': 0," +
                "'timescale': 10, 'timeout_seconds': 30," +
                "'red_numammoclips': 0, 'blue_numammoclips': 3," +
                "'size_blue_team': 1";*/

    public TextAsset unityJSONdata;

    public void Awake()
    {
        if (unityJSONdata != null)
        {
            json_src = unityJSONdata.text;
        }

        string[] lines = distancesIndexed.text.Split('\n');
        for (int i=0; i<lines.Length; ++i)
        {
            string coord = lines[i].Trim().Split('\t')[0].Trim();
            Debug.Log("Input: " + coord);
            coordinateConversion.Add(coord, i+1);
        }
    }

    [Header("Testing")]
    public bool test = false;
    public Transform test_transform;
    private void Update()
    {
        /*if (test)
        {
            Debug.Log("Height of " + test_transform.name + " = " + Globals.api.terrainSystem.GetTerrainHeight(new RideVector3(test_transform.position)));
            test = false;
        }*/
    }

    private static Dictionary<string,float> ConvertJSON(string json)
    {
        string js = json.Replace("'","").Replace(": ", ":");
        string[] parts = js.Split(',');

        Dictionary<string,float> dic = new Dictionary<string, float>();
        foreach(string s in parts)
        {
            string[] ab = s.Trim().Split(':');
            dic.Add(ab[0].Trim(), float.Parse(ab[1].Trim()));
        }
        return dic;
    }

    public static float GetWithDefault(string key, float defaultValue, float overrideValue)
    {
        if (useJSON)
        {
            Dictionary<string,float> dic = ConvertJSON(json_src);
            if (dic.ContainsKey(key))
            {
                return dic[key];
            }
            else
            {
                return defaultValue;
            }
        }
        else if (DEBUGOVERRIDE)
        {
            return overrideValue;
        }
        else
        {
            return Academy.Instance.EnvironmentParameters.GetWithDefault(key, defaultValue);
        }
    }

    public static bool GetWithDefault_bool(string key, bool defaultValue, bool overrideValue)
    {
        if (useJSON)
        {
            Dictionary<string,float> dic = ConvertJSON(json_src);
            if (dic.ContainsKey(key))
            {
                return dic[key] != 0;
            }
            else
            {
                return defaultValue;
            }
        }
        else if (DEBUGOVERRIDE)
        {
            return overrideValue;
        }
        else
        {
            return Academy.Instance.EnvironmentParameters.GetWithDefault(key, defaultValue ? 1f : 0f) != 0;
        }
    }

    public static int GetWithDefault_int(string key, int defaultValue, int overrideValue)
    {
        if (useJSON)
        {
            Dictionary<string,float> dic = ConvertJSON(json_src);
            if (dic.ContainsKey(key))
            {
                return (int)dic[key];
            }
            else
            {
                return defaultValue;
            }
        }
        else if (DEBUGOVERRIDE)
        {
            return overrideValue;
        }
        else
        {
            return Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault(key, (float)defaultValue));
        }
    }

    public static string StringifyVector(float[] vectorAction)
    {
        string vas = "[";
        for (int i = 0; i < vectorAction.Length; ++i)
        {
            vas += vectorAction[i].ToString() + ", ";
        }
        vas += "]";
        return vas;
    }    

    private static Dictionary<string,int> weaponDic = new Dictionary<string, int>() { {"Ak47",0},{"M4",1},{"M2Browning",2} };
    public static int GetWeaponObservationValue(string weaponStr)
    {
        foreach(KeyValuePair<string,int> kvp in weaponDic)
        {
            if (weaponStr.ToLower().Contains(kvp.Key.ToLower()))
                return kvp.Value;
        }
        return -1;
    }

    public static string GetRandomStartCoordinate(int index)
    {
        string[] pos_array = start_pos_red[Mathf.Clamp(index,0,start_pos_red.Length-1)];
        return pos_array[Random.Range(0, pos_array.Length)];
    }

    public static float GetMovementSpeed()
    {
        //return GetWithDefault("agent_speed", 3.5f, 3.5f);
        return 25f;//3.5f;
    }

    /*public static bool IsNearbyOtherAgents(IApi api, RideID agent, float radius)
    {        
        if (api.agentSystem.AgentExists(agent))
        {
            IEnumerable<RideID> others = api.scenarioSystem.GetAgents();
            RideVector3 position = api.agentSystem.GetAgentPosition(agent);
            foreach (RideID other in others)
            {
                if (other != agent)
                {
                    RideVector3 opos = api.agentSystem.GetAgentPosition(other);
                    //UnityEngine.Debug.Log(other + " is " + RideVector3.Distance(position, opos) + " units away!");
                    if (RideVector3.Distance(position, opos) <= radius)
                        return true;
                }
            }
        }
        return false;
    }*/

    public static PathReplayData GeneratePathReplayData(string inputData)
    {
        PathReplayData prd = new PathReplayData();
        prd.movementList.Add(0);
        prd.directionList.Add(0);

        string[] lines = inputData.Split('\n');
        foreach(string line in lines)
        {
            if (!string.IsNullOrEmpty(line.Trim()))
            {
                string[] parts = line.Split('\t');
                if (prd.waypointCoordinates == null)
                {
                    string wpstr = parts[3].Split('(')[1].Split(')')[0];
                    string[] coordstr = wpstr.Split(',');
                    prd.waypointCoordinates = new Vector2(float.Parse(coordstr[0]), float.Parse(coordstr[1]));
                }
                string[] data = parts[2].Split(';');
                prd.movementList.Add(int.Parse(data[0]));
                prd.directionList.Add(int.Parse(data[1]));
            }
        }

        return prd;
    }

    /*public static RideID GetID(GameObject go)
    {
        return Globals.api.systemAccessSystem.GetSystem<IGameObjectSystem>().GetObject(go);
    }

    public static void SetDebug(bool isSet)
    {
        Ride.AI.MLAgentsMovementCoordinator.IS_DEBUG = isSet;
        Ride.AI.HasReachedWaypointNode.IS_DEBUG = isSet;
        Ride.AI.TeamCombatArea.IS_DEBUG = isSet;
        Ride.AI.TeamCombatMLAgent.IS_DEBUG = isSet;
        Ride.AI.MLAgentsMovementCoordinator.IS_DEBUG = isSet;
        Ride.AI.AttackTargetNode.IS_DEBUG = isSet;
        Ride.AI.StopMovingNode.IS_DEBUG = isSet;
        Ride.AI.IsEnemyVisibleNode.IS_DEBUG = isSet;
    }*/
}
