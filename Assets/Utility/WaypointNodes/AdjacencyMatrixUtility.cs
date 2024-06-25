using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Ride.Movement;
using UnityEditor;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class FourConnectedNode
{
    public enum DIRECTION
    {
        NONE = 0,
        NORTH = 1,
        SOUTH = 2,
        WEST = 3,
        EAST = 4
    }

    public static readonly Dictionary<DIRECTION, Vector2> directionVectors = new Dictionary<DIRECTION, Vector2>()
    {
        {DIRECTION.NONE,  new Vector2( 0f, 0f)}, // NONE
        {DIRECTION.NORTH, new Vector2( 0f, 1f)}, // NORTH
        {DIRECTION.SOUTH, new Vector2( 0f,-1f)}, // SOUTH
        {DIRECTION.WEST,  new Vector2(-1f, 0f)}, // WEST
        {DIRECTION.EAST,  new Vector2( 1f, 0f)}  // EAST
    };

    public FourConnectedNode north = null, east = null, south = null, west = null;
    public WaypointMono waypoint;
    public Transform t;


    public FourConnectedNode(WaypointMono wp)
    {
        waypoint = wp;
        t = wp.transform;
    }

    public FourConnectedNode(Transform tform)
    {
        t = tform;
    }

    public FourConnectedNode(Transform tform, FourConnectedNode n, FourConnectedNode e, FourConnectedNode s, FourConnectedNode w)
    {
        t = tform;
        north = n;
        east = e;
        south = s;
        west = w;
    }

    public static DIRECTION GetOppositeDirection(DIRECTION dir)
    {
        switch (dir)
        {
            case DIRECTION.NORTH:
                return DIRECTION.SOUTH;
            case DIRECTION.SOUTH:
                return DIRECTION.NORTH;
            case DIRECTION.WEST:
                return DIRECTION.EAST;
            case DIRECTION.EAST:
                return DIRECTION.WEST;
            default:
                return DIRECTION.NONE;
        }
    }

    /// <summary>
    /// Returns added node
    /// </summary>
    /// <param name="node"></param>
    /// <param name="dir"></param>
    /// <returns></returns>
    public FourConnectedNode AddNode(FourConnectedNode node, DIRECTION dir)
    {
        if (this != node && node != null && !AlreadyHasNode(node))
        {
            switch (dir)
            {
                case DIRECTION.NORTH:
                    north = node;
                    node.south = this;
                    break;
                case DIRECTION.EAST:
                    east = node;
                    node.west = this;
                    break;
                case DIRECTION.SOUTH:
                    south = node;
                    node.north = this;
                    break;
                case DIRECTION.WEST:
                    west = node;
                    node.east = this;
                    break;
            }
        }
        return node;
    }

    public FourConnectedNode AddNodeOnlyIfNullForBoth(FourConnectedNode node, DIRECTION dir)
    {
        if (this != node && node != null && !AlreadyHasNode(node))
        {
            switch (dir)
            {
                case DIRECTION.NORTH:
                    if (north == null && node.south == null)
                    {
                        north = node;
                        node.south = this;
                    }
                    break;
                case DIRECTION.EAST:
                    if (east == null && node.west == null)
                    {
                        east = node;
                        node.west = this;
                    }
                    break;
                case DIRECTION.SOUTH:
                    if (south == null && node.north == null)
                    {
                        south = node;
                        node.north = this;
                    }
                    break;
                case DIRECTION.WEST:
                    if (west == null && node.east == null)
                    {
                        west = node;
                        node.east = this;
                    }
                    break;
            }
        }
        return node;
    }

    public bool AlreadyHasNode(FourConnectedNode node)
    {
        return (north == node ||
                east == node ||
                south == node ||
                west == node);
    }

    public DIRECTION GetNodeDirection(FourConnectedNode node)
    {
        if (AlreadyHasNode(node))
        {
            if (north == node)
                return DIRECTION.NORTH;
            else if (east == node)
                return DIRECTION.EAST;
            else if (south == node)
                return DIRECTION.SOUTH;
            else if (west == node)
                return DIRECTION.WEST;
            else
                return DIRECTION.NONE;
        }
        else
        {
            return DIRECTION.NONE;
        }
    }

    public FourConnectedNode GetNodeFromDirection(DIRECTION dir)
    {
        switch (dir)
        {
            case DIRECTION.NORTH:
                return north;
            case DIRECTION.EAST:
                return east;
            case DIRECTION.SOUTH:
                return south;
            case DIRECTION.WEST:
                return west;
            default:
                return null;
        }
    }

    public FourConnectedNode[] GetNeighboringNodes()
    {
        return new FourConnectedNode[4] { north, south, west, east };
    }

    public string GetID()
    {
        return t.gameObject.name.Split('_')[1];
    }

    public Vector2 GetIDasVector()
    {
        //Debug.Log("<color=magenta>GetIDAsVector: " + GetID() + "</color>");
        string[] id = GetID().Replace("(","").Replace(")","").Split(',');
        return new Vector2(float.Parse(id[0]),float.Parse(id[1]));
    }

    public string CheckMismatches()
    {
        string output = "";
        if (north != null && north.south != null && GetID() != north.south.GetID()) output += "N";
        if (east != null && east.west != null && GetID()  != east.west.GetID()) output += "E";
        if (south != null && south.north != null && GetID() != south.north.GetID()) output += "S";
        if (west != null && west.north != null && GetID()  != west.east.GetID()) output += "W";
        return output;
    }

    public string ToSaveString()
    {
        return GetID() + "\t" + 
            (north == null ? "null" : north.GetID()) + "\t" + 
            (south == null ? "null" : south.GetID()) + "\t" + 
            (west  == null ? "null" :  west.GetID()) + "\t" + 
            (east  == null ? "null" :  east.GetID()) + "\t" + 
            CheckMismatches();
    }

    public void DrawDebugLines()
    {
        if (north != null) Debug.DrawLine(t.position, north.t.position, Color.cyan, 1e6f, false);
        if (east  != null) Debug.DrawLine(t.position,  east.t.position, Color.green, 1e6f, false);
        if (south != null) Debug.DrawLine(t.position, south.t.position, Color.red, 1e6f, false);
        if (west  != null) Debug.DrawLine(t.position,  west.t.position, Color.yellow, 1e6f, false);
    }

    public void DrawGizmoLines(float zOffset)
    {
#if UNITY_EDITOR
        Vector3 z = new Vector3(0f, zOffset, 0f);
        if (t != null)
        {
            if (north != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(t.position + z, north.t.position + z);
                Handles.Label(0.5f * (t.position + z + north.t.position + z), Vector3.Distance(t.position + z, north.t.position + z).ToString(".0"));
            }
            if (east != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(t.position + z, east.t.position + z);
                Handles.Label(0.5f * (t.position + z + east.t.position + z), Vector3.Distance(t.position + z, east.t.position + z).ToString(".0"));
            }
            if (south != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(t.position + z, south.t.position + z);
            }
            if (west != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(t.position + z, west.t.position + z);
            }
        }
#endif
    }

    public void DrawInGameLines(float zOffset)
    {
#if !NOGUI
        Vector3 z = new Vector3(0f, zOffset, 0f);
        if (north != null)
        {
            Transform lrp = GameObject.FindWithTag("inGameLineParent")?.transform;
            if (lrp != null)
            {
                GameObject lrgo = new GameObject(t.gameObject.name + " - " + north.t.gameObject.name + "_line");
                lrgo.transform.parent = lrp;
                LineRenderer lr = lrgo.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startColor = Color.cyan;
                lr.endColor = Color.cyan;
                lr.SetPosition(0, t.position + z);
                lr.SetPosition(1, north.t.position + z);
            }
        }
        if (east != null)
        {
            //Gizmos.color = Color.green;
            //Gizmos.DrawLine(t.position + z, east.t.position + z);
            Transform lrp = GameObject.FindWithTag("inGameLineParent")?.transform;
            if (lrp != null)
            {
                GameObject lrgo = new GameObject(t.gameObject.name + " - " + east.t.gameObject.name + "_line");
                lrgo.transform.parent = lrp;
                LineRenderer lr = lrgo.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startColor = Color.green;
                lr.endColor = Color.green;
                lr.SetPosition(0, t.position + z);
                lr.SetPosition(1, east.t.position + z);
            }
        }
        if (south != null)
        {
            //Gizmos.color = Color.red;
            //Gizmos.DrawLine(t.position + z, south.t.position + z);
            Transform lrp = GameObject.FindWithTag("inGameLineParent")?.transform;
            if (lrp != null)
            {
                GameObject lrgo = new GameObject(t.gameObject.name + " - " + south.t.gameObject.name + "_line");
                lrgo.transform.parent = lrp;
                LineRenderer lr = lrgo.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startColor = Color.red;
                lr.endColor = Color.red;
                lr.SetPosition(0, t.position + z);
                lr.SetPosition(1, south.t.position + z);
            }
        }
        if (west != null)
        {
            //Gizmos.color = Color.yellow;
            //Gizmos.DrawLine(t.position + z, west.t.position + z);
            Transform lrp = GameObject.FindWithTag("inGameLineParent")?.transform;
            if (lrp != null)
            {
                GameObject lrgo = new GameObject(t.gameObject.name + " - " + west.t.gameObject.name + "_line");
                lrgo.transform.parent = lrp;
                LineRenderer lr = lrgo.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startColor = Color.yellow;
                lr.endColor = Color.yellow;
                lr.SetPosition(0, t.position + z);
                lr.SetPosition(1, west.t.position + z);
            }
        }
#endif
    }
}

public struct FCNDistancePair
{
    public FourConnectedNode FCNode;
    public float distance;

    public FCNDistancePair(FourConnectedNode fcn, float d)
    {
        FCNode = fcn;
        distance = d;
    }
}

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class AdjacencyMatrixUtility : MonoBehaviour
{
    static bool IS_DEBUG = false;

    private bool isInited = false;
    public Transform nodeParent;
    private static Dictionary<Transform, FourConnectedNode> fcndic = new Dictionary<Transform, FourConnectedNode>();
    private Dictionary<string, FourConnectedNode> fcndic_coord = new Dictionary<string, FourConnectedNode>();    
    public GameObject nodePrefab, nodeLabelPrefab;

    private static readonly float searchRadius = 75f;

    public bool loadData = true;
    public bool checkData = false;
    public TextAsset loadTextAsset, locationTextAsset;

    public TextAsset CrawlCrawlMGDistances, CrawlStandMGDistances, StandCrawlMGDistances, StandStandMGDistances;
    private static Dictionary<WaypointMono, float> MGDistanceDictionary = new Dictionary<WaypointMono, float>();
    
    void Awake() { Init(); }

//#if UNITY_EDITOR
    [Header("Testing")]
    public string test_fileID;
    public bool test = false;
    public bool test_load = false, test_create = false;
    public WaypointMono testNodeA, testNodeB;
    void Update()
    {
        if (test)
        {
            /*Vector3 p0 = testNodeA.position;
            Vector3 p1 = testNodeB.position;
            float d = Vector3.Distance(p0, p1);
            float a = Mathf.Atan2(p1.x-p0.x, p1.z-p0.z)*Mathf.Rad2Deg;
            Debug.Log("Dist=" + d + "; a=" + a);*/
            //WaypointMono[] route = GetRoute(testNodeA, testNodeB);
            //Debug.Log(WaypointArrayToString(route));
            /*Debug.Log("fcndic_coord null? " + (fcndic_coord == null));
            Debug.Log("fcndic_coord size= " + (fcndic_coord.Count));*/
            //CreateDistanceFiles(test_fileID);
            //if (testNodeA != null && testNodeB != null)
            //    Debug.Log("Probability of hit from " + testNodeA + " to " + testNodeB + " = " + GetProbability(testNodeA, true, testNodeB, true));

            //DeployWaypointPrefabs();

            //FillOutWaypointFields();
            UpdateWaypointPrefabs();

            test = false;
        }

        if (test_load)
        {
            fcndic.Clear();
            fcndic_coord.Clear();
            SetupFCNDic();
            LoadNodesFromString(loadTextAsset.text);
            //ShowConnections();
            test_load = false;
        }

        if (test_create)
        {
            fcndic.Clear();
            fcndic_coord.Clear();
            SetupFCNDic();
            SetNearestNodes_NoDuplicates();
            System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/FCnodes_scout4-noDup_NSWE.txt", SaveNodesToString());

            test_create = false;
        }
    }
//#endif

    void Init()
    {
        if (!isInited && Application.isPlaying)
        {
            /*
            foreach(Transform child in nodeParent)
            {
                FourConnectedNode fcn = new FourConnectedNode(child.GetComponent<WaypointMono>());
                fcndic.Add(child,fcn);
                fcndic_coord.Add(child.name.Split('_')[1], fcn);
                Debug.Log("fcndic_coord. Add: " + child.name.Split('_')[1] + "; fcn=" + fcn.ToSaveString());
                GameObject labelGO = (GameObject)Instantiate(nodeLabelPrefab, child);
                labelGO.transform.localPosition = Vector3.zero;
                labelGO.GetComponent<TextMesh>().text = child.name.Split('_')[1];
            }*/
            SetupFCNDic();
            /*
            
            string[] lines = StandStandMGDistances.text.Split('\n');
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line.Trim()))
                {
                    string[] parts = line.Split('\t');
                    MGDistanceDictionary.Add(fcndic_coord[parts[0].Split('_')[1]].waypoint, float.Parse(parts[1]));
                }
            }*/

            if (checkData)
            {
#if UNITY_EDITOR
                // Go through loaded text asset and remove nulls and then re-save
                string newFCNData = GenerateUpdatedData(loadTextAsset.text);
                System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/FCnodes_scout_grid-fix1_NSWE.txt",newFCNData);
#endif
            }
            else if (loadData)
            {
                LoadNodesFromString(loadTextAsset.text);
            }
            else
            {
#if UNITY_EDITOR
                SetNearestNodes();
                System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/FCnodes_scout2_NSWE.txt",SaveNodesToString());
#endif
            }
#if UNITY_EDITOR
            ShowConnections();
#endif
            ShowConnectionsInGame();

            //Debug.Log(SaveNodesToString());

            isInited = true;
        }
        //Debug.Log("AdjacencyMatrixUtility isInited");
    }

    void SetupFCNDic()
    {
        foreach (Transform child in nodeParent)
        {
            FourConnectedNode fcn = new FourConnectedNode(child.GetComponent<WaypointMono>());
            fcndic.Add(child, fcn);
            fcndic_coord.Add(child.name.Split('_')[1], fcn);
        }
    }

    string GenerateUpdatedData(string oldFCNData)
    {
        string newFCNData = string.Empty;
        string[] lines = oldFCNData.Trim().Split('\n');
        foreach(string line in lines)
        {
            string[] parts = line.Split('\t');
            if (GetNodeFromString(parts[0]) != null)
            {
                newFCNData += parts[0];
                for (int i=1; i<=4; ++i)
                {
                    newFCNData += "\t";
                    if (parts[i].Contains("null")
                        || GetNodeFromString(parts[i]) != null)
                    {
                        newFCNData += parts[i];
                    }
                    else
                    {
                        newFCNData += "null";
                    }
                }
                newFCNData += "\n";
            }
        }
        //Debug.LogError("Generated Updated data: " + newFCNData);
        return newFCNData;        
    }

    void SetNearestNodes()
    {
        foreach(KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            if (kvp.Value.north == null) kvp.Value.AddNode(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.NORTH), FourConnectedNode.DIRECTION.NORTH);
            if (kvp.Value.east  == null) kvp.Value.AddNode(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.EAST), FourConnectedNode.DIRECTION.EAST);
            if (kvp.Value.south == null) kvp.Value.AddNode(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.SOUTH), FourConnectedNode.DIRECTION.SOUTH);
            if (kvp.Value.west  == null) kvp.Value.AddNode(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.WEST), FourConnectedNode.DIRECTION.WEST);
        }
    }

    void SetNearestNodes_NoDuplicates()
    {
        foreach (KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            if (kvp.Value.north == null) kvp.Value.AddNodeOnlyIfNullForBoth(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.NORTH), FourConnectedNode.DIRECTION.NORTH);
            if (kvp.Value.east == null) kvp.Value.AddNodeOnlyIfNullForBoth(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.EAST), FourConnectedNode.DIRECTION.EAST);
            if (kvp.Value.south == null) kvp.Value.AddNodeOnlyIfNullForBoth(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.SOUTH), FourConnectedNode.DIRECTION.SOUTH);
            if (kvp.Value.west == null) kvp.Value.AddNodeOnlyIfNullForBoth(GetNearestNode(kvp.Value, FourConnectedNode.DIRECTION.WEST), FourConnectedNode.DIRECTION.WEST);
        }
    }

    FourConnectedNode GetNearestNode(FourConnectedNode sourceNode, FourConnectedNode.DIRECTION dir)
    {
        float offset = 45f;
        switch (dir)
        {
            case (FourConnectedNode.DIRECTION.NORTH):
                return GetNearestNode(sourceNode, -45f+offset, 45f+offset);
            case (FourConnectedNode.DIRECTION.EAST):
                return GetNearestNode(sourceNode, 45f+offset, 135f+offset);
            case (FourConnectedNode.DIRECTION.SOUTH):
                return GetNearestNode(sourceNode, -135f+offset, 135f+offset);
            case (FourConnectedNode.DIRECTION.WEST):
                return GetNearestNode(sourceNode, -135f+offset, -180);//-45f+offset);
        }
        return null;
    }

    public static WaypointMono GetWaypointInDirection(WaypointMono sourceWaypoint, FourConnectedNode.DIRECTION dir)
    {
        if (dir == FourConnectedNode.DIRECTION.NONE)
            return sourceWaypoint;

        //Debug.Log("GetWaypointInDirection: source=" + sourceWaypoint.gameObject.name);
        FourConnectedNode fcn_source = fcndic[sourceWaypoint.transform];
        FourConnectedNode fcn_dest = fcn_source.GetNodeFromDirection(dir);
        
        if (fcn_dest == null)
        {
            return null;
        }
        else
        {
            return fcn_dest.waypoint;
        }        
    }

    public static FourConnectedNode.DIRECTION GetDirectionBetweenWaypoints(WaypointMono sourceWaypoint, WaypointMono destinationWaypoint)
    {
        if (fcndic == null)
        {
            Debug.Log("Is FCN Dic null? " + (fcndic == null));
        }

        FourConnectedNode fcn_source = fcndic[sourceWaypoint.transform];
        FourConnectedNode fcn_dest = fcndic[destinationWaypoint.transform];

        return fcn_source.GetNodeDirection(fcn_dest);
    }

    // start in direction of look 
    FourConnectedNode GetNearestNode(FourConnectedNode sourceNode, float angleA, float angleB)
    {
        float minDist = float.PositiveInfinity;
        FourConnectedNode closest = null;
        foreach(KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            Vector3 p0 = sourceNode.t.position;
            Vector3 p1 = kvp.Value.t.position;
            float d = Vector3.Distance(p0, p1);            
            if (d > 0.01f && d <= searchRadius)
            {
                float a = Mathf.Atan2(p1.x-p0.x, p1.z-p0.z)*Mathf.Rad2Deg;
                if (angleA <= -135) // angleB >= 135
                {
                    if (a <= angleA || a >= angleB)
                    {
                        //return kvp.Value;
                        if (d < minDist)
                        {
                            closest = kvp.Value;
                            minDist = d;
                        }
                    }
                }
                else
                {
                    if (angleA <= a && a <= angleB)
                    {
                        //return kvp.Value;
                        if (d < minDist)
                        {
                            closest = kvp.Value;
                            minDist = d;
                        }
                    }
                }
            }
        }
        return closest;
    }

#if UNITY_EDITOR
    // Utilize https://docs.unity3d.com/ScriptReference/Debug.DrawLine.html
    void ShowConnections()
    {
        foreach(KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            kvp.Value.DrawDebugLines();
        }
    }
#endif

    void ShowConnectionsInGame()
    {
        Debug.Log("ShowConnectionsInGame");
        foreach (KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            kvp.Value.DrawInGameLines(0f);
        }
    }

    string SaveNodesToString()
    {
        string output = "";
        foreach(KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            output += kvp.Value.ToSaveString() + "\n";
        }
        return output;
    }

    void LoadNodesFromString(string loadString)
    {
        string[] lines = loadString.Split('\n');
        Dictionary<string, FourConnectedNode> fcndic_copy = new Dictionary<string, FourConnectedNode>(fcndic_coord);
        foreach (string line in lines)
        {
            string[] parts = line.Split('\t');
            if (IS_DEBUG)
            {
                Debug.Log("LoadNodesFromString: " + line + "; parts=" + string.Join("\n",parts));
                foreach (string part in parts)
                {
                    Debug.Log("LoadNodesFromString: " + part + " in fcn_coord? " + fcndic_coord.ContainsKey(part));
                }
            }
            if (fcndic_coord.ContainsKey(parts[0]))
            {
                FourConnectedNode fcn = fcndic_coord[parts[0]];
                if (!parts[1].Contains("null")) fcn.north = fcndic_coord[parts[1].Trim()];
                if (!parts[2].Contains("null")) fcn.south = fcndic_coord[parts[2].Trim()];
                if (!parts[3].Contains("null")) fcn.west = fcndic_coord[parts[3].Trim()];
                if (!parts[4].Contains("null")) fcn.east = fcndic_coord[parts[4].Trim()];
            }
            else
            {
                Debug.Log("<color=yellow>Key not found: " + line + "</color>");
            }
        }
    }

    public void DeployWaypointPrefabs()
    {
        Dictionary<string, string> locDic = new Dictionary<string, string>();
        Dictionary<string, WaypointMono> waypointDic = new Dictionary<string, WaypointMono>();
        string[] lines = locationTextAsset.text.Split('\n');
        foreach (string line in lines)
        {
            string[] parts = line.Split('\t');
            locDic.Add(parts[0], parts[1]);

            GameObject nodeObject = Instantiate(nodePrefab, nodeParent, false);
            nodeObject.name = "Waypoint_" + parts[0];
            WaypointMono wm = nodeObject.GetComponent<WaypointMono>();
            wm.SetPosition(parts[1]);
            waypointDic.Add(wm.waypointID, wm);
        }

        string[] nodeConnsLines = loadTextAsset.text.Split('\n');
        foreach(string line in nodeConnsLines)
        {
            string[] parts = line.Split('\t');
            if (waypointDic.ContainsKey(parts[0]))
            {
                WaypointMono wm = waypointDic[parts[0]];
                for (int i = 1; i <= 4; ++i)
                {
                    if (parts[i][0].ToString() == "(")
                    {
                        wm.neighbors.Add((FourConnectedNode.DIRECTION)i, waypointDic[parts[i]]);
                    }
                }
            }
            else
            {
                Debug.Log("Missing key: " + parts[0]);
            }
        }
    }

    public void UpdateWaypointPrefabs()
    {
        Dictionary<string, WaypointMono> waypointDic = new Dictionary<string, WaypointMono>();
        foreach (Transform child in transform)
        {
            WaypointMono wm = child.GetComponent<WaypointMono>();
            wm.neighbors.Clear();
            child.GetComponent<Waypoint>().outs.Clear();
            waypointDic.Add(child.gameObject.name.Split('_')[1].Trim(), wm);
            if (IS_DEBUG) Debug.Log("Added: " + child.gameObject.name.Split('_')[1].Trim());
        }
        

        string[] nodeConnsLines = loadTextAsset.text.Split('\n');
        foreach (string line in nodeConnsLines)
        {
            string[] parts = line.Split('\t');
            if (waypointDic.ContainsKey(parts[0].Trim()))
            {
                WaypointMono wm = waypointDic[parts[0].Trim()];
                for (int i = 1; i <= 4; ++i)
                {
                    if (parts[i].Trim()[0].ToString() == "(")
                    {
                        if (waypointDic.ContainsKey(parts[i].Trim()))
                        {
                            wm.neighbors.Add((FourConnectedNode.DIRECTION)i, waypointDic[parts[i].Trim()]);
                            wm.GetComponent<Waypoint>().outs.Add(new WaypointPercent(waypointDic[parts[i].Trim()].GetComponent<Waypoint>()));
                        }
                        else
                        {
                            Debug.LogError(parts[0].Trim() + "-> Waypoint Missing: " + parts[i].Trim());
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("Missing key: " + parts[0].Trim());
            }
        }
    }


    void CreateDistanceFiles(string fileID)
    {
        string indexedOutput = "";
        string distanceList = "";
        foreach(KeyValuePair<string, FourConnectedNode> kvp in fcndic_coord)
        {
            if (!string.IsNullOrEmpty(kvp.Key))
            {
                float n_dist = kvp.Value.north == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.north.t.position);
                float e_dist = kvp.Value.east == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.east.t.position);
                float s_dist = kvp.Value.south == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.south.t.position);
                float w_dist = kvp.Value.west == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.west.t.position);
                indexedOutput += kvp.Key + "\t" +
                    (kvp.Value.north == null ? "null" : kvp.Value.north.GetID()) + ";" + n_dist + "\t" +
                    (kvp.Value.east  == null ? "null" : kvp.Value.east.GetID()) + ";" + e_dist + "\t" +
                    (kvp.Value.south == null ? "null" : kvp.Value.south.GetID()) + ";" + s_dist + "\t" +
                    (kvp.Value.west  == null ? "null" : kvp.Value.west.GetID()) + ";" + w_dist + "\n";
                distanceList +=
                    (float.IsNaN(n_dist) ? "" : n_dist + "\n") +
                    (float.IsNaN(e_dist) ? "" : e_dist + "\n") +
                    (float.IsNaN(s_dist) ? "" : s_dist + "\n") +
                    (float.IsNaN(w_dist) ? "" : w_dist + "\n");
            }
        }
        System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + fileID + "_Distances_Indexed.txt", indexedOutput.Trim());
        System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + fileID + "_Distances_List.txt", distanceList.Trim());
    }

    static Dictionary<FourConnectedNode, FCNDistancePair[]> CreateDistanceDic()
    {
        Dictionary<FourConnectedNode, FCNDistancePair[]> dic = new Dictionary<FourConnectedNode, FCNDistancePair[]>();
        foreach (KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            FCNDistancePair[] pairs = new FCNDistancePair[4];

            float n_dist = kvp.Value.north == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.north.t.position);
            float s_dist = kvp.Value.south == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.south.t.position);
            float w_dist = kvp.Value.west == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.west.t.position);
            float e_dist = kvp.Value.east == null ? float.NaN : Vector3.Distance(kvp.Value.t.position, kvp.Value.east.t.position);

            pairs[0] = new FCNDistancePair(kvp.Value.north, n_dist);
            pairs[1] = new FCNDistancePair(kvp.Value.south, s_dist);
            pairs[2] = new FCNDistancePair(kvp.Value.west, w_dist);
            pairs[3] = new FCNDistancePair(kvp.Value.east, e_dist);

            dic.Add(kvp.Value, pairs);
        }
        return dic;
    }

    public FourConnectedNode GetNodeFromString(string coord)
    {
        if (fcndic_coord.ContainsKey(coord))
            return fcndic_coord[coord];
        else
            return null;
    }

    public static WaypointMono[] GetRoute(WaypointMono start, WaypointMono end)
    {
        if (IS_DEBUG) Debug.Log("GetRoute: fcnDic null? " + (fcndic == null) + "; start? " + (start == null) + "; end? " + (end == null));
        if ((start != null) && (end != null) && fcndic.ContainsKey(start.transform) && fcndic.ContainsKey(end.transform))
        {
            //Debug.Log("GetWaypointInDirection: source=" + sourceWaypoint.gameObject.name);
            FourConnectedNode fcn_source = fcndic[start.transform];
            FourConnectedNode fcn_dest = fcndic[end.transform];
            
            if (fcn_source == null || fcn_dest == null)
            {
                if (IS_DEBUG) Debug.Log("GetRoute: fcn_Source==null? " + (fcn_source == null) + "; dest? " + (fcn_dest == null));
                return null;
            }
            else
            {
                // Dirty alternative to Dijkstra's Algorithm
                // -- compare distance from start to end with end to start, find the shorter one
                List<WaypointMono> routeList = new List<WaypointMono>() { start };
                GetShortestRoute(fcn_source, fcn_dest, routeList);

                List<WaypointMono> reverseRouteList = new List<WaypointMono>() { end };
                GetShortestRoute(fcn_dest, fcn_source, reverseRouteList);

                if (routeList.Count <= reverseRouteList.Count)
                {
                    return routeList.ToArray();
                }
                else // If the reverse is shorter, reverse it to get it from start to end
                {
                    reverseRouteList.Reverse();
                    return reverseRouteList.ToArray();
                }
                
            }
        }
        else
        {
            if (IS_DEBUG) Debug.Log("GetRoute: start null? " + (start == null) + "; end null? " + (end == null) + "; fcndic.ContainsKey(start.transform)? " + fcndic.ContainsKey(start.transform) +"; fcndic.ContainsKey(end.transform)? " + fcndic.ContainsKey(end.transform));
            return null;
        }
    }

    private static List<WaypointMono> GetShortestRoute(FourConnectedNode currentStart, FourConnectedNode end, List<WaypointMono> runningList)
    {
        /*
        // ATTEMPT AT DIJKSTRA'S ALGORITHM
        Dictionary<FourConnectedNode, FCNDistancePair[]> distanceDic = CreateDistanceDic();
        int V = distanceDic.Count;
        Dictionary<FourConnectedNode, float> dist = new Dictionary<FourConnectedNode, float>();
        //Dictionary<FourConnectedNode, bool> sptSet = new Dictionary<FourConnectedNode, bool>();
        List<FourConnectedNode> unexplored = new List<FourConnectedNode>();

        // Initialize dictionary
        foreach (KeyValuePair<FourConnectedNode, FCNDistancePair[]> kvp in distanceDic)
        {
            dist[kvp.Key] = float.PositiveInfinity;
            //sptSet[kvp.Key] = false;
            unexplored.Add(kvp.Key);
        }

        // Initialize start node
        dist[currentStart] = 0f;
        //sptSet[currentStart] = true;
        unexplored.Remove(currentStart);


        FourConnectedNode[] neighbors = currentStart.GetNeighboringNodes();
        FCNDistancePair[] pairs = distanceDic[currentStart];
        foreach (FCNDistancePair pair in pairs)        
        {
            if (pair.FCNode != null)
            {
                dist[pair.FCNode] = pair.distance;
                //sptSet[pair.FCNode] = true;
                unexplored.Remove(pair.FCNode);
            }
        }

        while (unexplored.Count > 0)
        {
            // Sort the explored by their weight in ascending order.
            unexplored.Sort((x, y) => dist[x].CompareTo(dist[y]));

            // Get the lowest weight in unexplored.
            FourConnectedNode current = unexplored[0];

            // Note: This is used for games, as we just want to reduce compuation, better way will be implementing A*
            
            // If we reach the end node, we will stop.
            //if(current == end)
            //{   
            //    return end;
            //}

            unexplored.Remove(current);

            FCNDistancePair[] currentNeighborPairs = distanceDic[current];
            foreach (FCNDistancePair fcnpair in currentNeighborPairs)
            {
                if (fcnpair.FCNode != null)
                {
                    if (unexplored.Contains(fcnpair.FCNode))
                    {
                        float distance = fcnpair.distance;
                        distance += dist[current];

                        if (distance < dist[)
                    }
                }
            }

        }*/

        // for each of these, check out its neighbors until all its neighbors are marked as true in sptSet

        
        if (Vector3.Distance(currentStart.t.position, end.t.position) < 0.1f)
        {
            return runningList;
        }
        else
        {
            float minDist = float.PositiveInfinity;
            FourConnectedNode closest = null;
            for (int i=1; i<=4; ++i)
            {
                FourConnectedNode.DIRECTION dir = (FourConnectedNode.DIRECTION)i;
                FourConnectedNode dn = currentStart.GetNodeFromDirection(dir);
                if (dn != null && !runningList.Contains(dn.waypoint))
                {
                    float d = Vector3.Distance(end.waypoint.transform.position, dn.waypoint.transform.position);
                    if (d < minDist) // Need Dijkstra's algorithm
                    {
                        minDist = d;
                        closest = dn;
                    }
                }
            }
            if (IS_DEBUG)
            {
                if (closest == null)
                {
                    Debug.Log("<color=red>closest is null! Running list=" + WaypointArrayToString(runningList.ToArray()) + "; mindist=" + minDist + "; currentStart=" + currentStart.GetID() + "</color>");
                }
                else
                {
                    Debug.Log("<color=magenta>minDist=" + minDist + "; closest=" + closest.waypoint.gameObject.name + "</color>");
                }
            }
            if (closest == null)
            {
                return runningList;
            }
            else
            {
                runningList.Add(closest.waypoint);
                return GetShortestRoute(closest, end, runningList);
            }
        }
        
    }

    public static WaypointMono GetClosestWaypointToPosition(Vector3 position, float maxDistance = 5f)
    {
        float minDist = float.PositiveInfinity;
        FourConnectedNode[] fcna = new FourConnectedNode[fcndic.Count];
        fcndic.Values.CopyTo(fcna, 0);
        FourConnectedNode closestFCN = fcna[0];
        foreach(KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            float d = Vector3.Distance(kvp.Key.position, position);
            if (d < minDist)
            {
                minDist = d;
                closestFCN = kvp.Value;
            }
        }
        if (minDist > maxDistance) return null;
        if (IS_DEBUG) Debug.Log("Closest waypoint to " + position + " is " + closestFCN.waypoint);
        return closestFCN.waypoint;
    }

    public static string WaypointArrayToString(WaypointMono[] waypointArray)
    {
        string output = string.Empty;
        for (int i=0; i<waypointArray.Length; ++i)
        {
            output += waypointArray[i].gameObject.name.Split('_')[1] + ";";
        }
        return output;
    }

    public int GetNumberOfWaypoints()
    {
        return nodeParent.childCount;
    }

    public static float GetMGProbabilityToWaypoint(WaypointMono waypoint)
    {
        return MGDistanceDictionary[waypoint];
    }

    public static bool IsWaypointInCautiousZone(WaypointMono waypoint)
    {
        return GetMGProbabilityToWaypoint(waypoint) >= 0.5f;
    }

    public static bool IsWaypointInDangerousZone(WaypointMono waypoint)
    {
        return GetMGProbabilityToWaypoint(waypoint) >= 0.9f;
    }

    /// <summary>
    /// This should use AK47-based measurements, but M4 are being used for now
    /// as they have the same range and damage
    /// </summary>
    public static float GetProbability(WaypointMono origin, bool origin_standing, WaypointMono target, bool target_standing)
    {
        if (target == null) return 0f;

        string originCoord = origin.name.Split('_')[1];
        string resourceName = "NodeAK47Probabilities\\" + originCoord + "toNodes_" +
            (origin_standing ? "stand" : "crawl") + "-" +
            (target_standing ? "stand" : "crawl");
        TextAsset probFile = Resources.Load<TextAsset>(resourceName);
        if (IS_DEBUG) Debug.Log("GetProbability: " + resourceName + "; probFile=" + probFile);
        string[] lines = probFile.text.Split('\n');
        foreach(string line in lines)
        {
            if (!string.IsNullOrEmpty(line.Trim()))
            {
                string[] parts = line.Trim().Split('\t');
                //Debug.Log("parts=" + string.Join(",", parts) + "; target.name= " + target?.name);
                if (parts[1].Split('_')[1] == target.name.Split('_')[1])
                {
                    return float.Parse(parts[2]);
                }
            }
        }
        return float.NaN;
    }

    private void FillOutWaypointFields()
    {
        foreach(Transform child in transform)
        {
            WaypointMono waypointMono = child.GetComponent<WaypointMono>();
            Waypoint waypoint = child.GetComponent<Waypoint>();

            foreach(KeyValuePair<FourConnectedNode.DIRECTION,WaypointMono> neighbor in waypointMono.neighbors)
            {
                WaypointPercent wp = new WaypointPercent(neighbor.Value.GetComponent<Waypoint>());
                wp.probability = 0;
                waypoint.outs.Add(wp);
            }
        }
    }

    public static Vector3[] GetInterpolatedPoints(Vector3 startingPoint, Vector3 endingPoint, int numSegments)
    {
        Vector3[] interpolatedPoints = new Vector3[numSegments + 1]; // e.g., if numSegments = 3, then start point, a point at each third, and endingPoint -> numSegments+1
        Vector3 atob_n = (endingPoint - startingPoint).normalized;
        float atob_l = Vector3.Distance(startingPoint, endingPoint);
        interpolatedPoints[0] = startingPoint;
        for (int i=1; i<numSegments; ++i)
        {
            interpolatedPoints[i] = startingPoint + ((float)i / (float)numSegments) * atob_l * atob_n;
        }
        interpolatedPoints[numSegments] = endingPoint;
        return interpolatedPoints;
    }


#if UNITY_EDITOR
    #region EditorMode
    void ShowGizmoConnections()
    {
        foreach (KeyValuePair<Transform, FourConnectedNode> kvp in fcndic)
        {
            kvp.Value.DrawGizmoLines(0f);
            kvp.Value.DrawGizmoLines(100f);
        }
    }

    void OnDrawGizmos()
    {
        ShowGizmoConnections();
    }

    public void RemoveConnection(Transform waypointA, Transform waypointB)
    {
        string[] lines = loadTextAsset.text.Split('\n');
        string coordA = waypointA.gameObject.name.Split('_')[1];
        string coordB = waypointB.gameObject.name.Split('_')[1];

        string newLoadFile = string.Empty;
        if (fcndic_coord.ContainsKey(coordA) && fcndic_coord.ContainsKey(coordB))
        {
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts[0] == coordA)
                {
                    newLoadFile += parts[0] + "\t";
                    for (int i = 1; i<=4; ++i)
                    {
                        if (parts[i] == coordB)
                        {
                            newLoadFile += "null";
                        }
                        else
                        {
                            newLoadFile += parts[i];
                        }
                        newLoadFile += (i < 4) ? "\t" : "\n";
                    }
                }
                else if (parts[0] == coordB)
                {
                    newLoadFile += parts[0] + "\t";
                    for (int i = 1; i <= 4; ++i)
                    {
                        if (parts[i] == coordA)
                        {
                            newLoadFile += "null";
                        }
                        else
                        {
                            newLoadFile += parts[i];
                        }
                        newLoadFile += (i < 4) ? "\t" : "\n";
                    }
                }
                else
                {
                    newLoadFile += line + "\n";
                }
            }
        }

        //Debug.Log(newLoadFile.Trim());

        fcndic.Clear();
        fcndic_coord.Clear();
        SetupFCNDic();
        LoadNodesFromString(newLoadFile.Trim());

        System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + loadTextAsset.name + ".txt", newLoadFile.Trim());
        if (IS_DEBUG) Debug.Log("Removal done. " + Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + loadTextAsset.name + ".txt");

        HandleUtility.Repaint();
    }

    public void AddConnection(Transform waypointA, Transform waypointB, FourConnectedNode.DIRECTION nodeADirection)
    {
        if (nodeADirection != FourConnectedNode.DIRECTION.NONE)
        {
            string loadedText = loadTextAsset.text;

            string coordA = waypointA.gameObject.name.Split('_')[1];
            string coordB = waypointB.gameObject.name.Split('_')[1];

            Debug.Log("1) fcndic_coord contains " + coordA + "? " + fcndic_coord.ContainsKey(coordA) +
                "; fcndic_coord contains " + coordB + "? " + fcndic_coord.ContainsKey(coordB));

            //if (!fcndic_coord.ContainsKey(coordA))
            if (!loadedText.Contains(coordA))
            {
                loadedText += "\n" + coordA + "\tnull\tnull\tnull\tnull";
                //fcndic_coord.Add(coordA, new FourConnectedNode(waypointA));
            }
            if (!loadedText.Contains(coordB))
            {
                loadedText += "\n" + coordB + "\tnull\tnull\tnull\tnull";
                //fcndic_coord.Add(coordB, new FourConnectedNode(waypointB));
            }

            Debug.Log("LoadedText:" + loadedText);

            string[] lines = loadedText.Split('\n');
            FourConnectedNode.DIRECTION nodeBDirection = FourConnectedNode.GetOppositeDirection(nodeADirection);

            string newLoadFile = string.Empty;

            Debug.Log("2) fcndic_coord contains " + coordA + "? " + fcndic_coord.ContainsKey(coordA) +
                    "; fcndic_coord contains " + coordB + "? " + fcndic_coord.ContainsKey(coordB));
            if (fcndic_coord.ContainsKey(coordA) && fcndic_coord.ContainsKey(coordB))
            {
                foreach (string line in lines)
                {
                    string[] parts = line.Split('\t');
                    if (parts[0] == coordA)
                    {
                        newLoadFile += parts[0] + "\t";
                        for (int i = 1; i <= 4; ++i)
                        {
                            if (i == (int)nodeADirection)
                            {
                                if (parts[i].Contains("null"))
                                {
                                    newLoadFile += coordB;
                                }
                                else
                                {
                                    Debug.Log(coordA + " already has a connection in " + nodeADirection);
                                    return;
                                }

                            }
                            else
                            {
                                newLoadFile += parts[i];
                            }
                            newLoadFile += (i < 4) ? "\t" : "\n";
                        }
                    }
                    else if (parts[0] == coordB)
                    {
                        newLoadFile += parts[0] + "\t";
                        for (int i = 1; i <= 4; ++i)
                        {
                            if (i == (int)nodeBDirection)
                            {
                                if (parts[i].Contains("null"))
                                {
                                    newLoadFile += coordA;
                                }
                                else
                                {
                                    Debug.Log(coordB + " already has a connection in " + nodeBDirection);
                                    return;
                                }
                            }
                            else
                            {
                                newLoadFile += parts[i];
                            }
                            newLoadFile += (i < 4) ? "\t" : "\n";
                        }
                    }
                    else
                    {
                        newLoadFile += line + "\n";
                    }
                }
            }

            Debug.Log(newLoadFile.Trim());

            fcndic.Clear();
            fcndic_coord.Clear();
            SetupFCNDic();
            LoadNodesFromString(newLoadFile.Trim());

            System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + loadTextAsset.name + ".txt", newLoadFile.Trim());
            Debug.Log("Add done. " + Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + loadTextAsset.name + ".txt");

            HandleUtility.Repaint();
        }
    }

    public void RemoveWaypointAndConnections(Transform waypointA)
    {
        string[] lines = loadTextAsset.text.Split('\n');
        string coordA = waypointA.gameObject.name.Split('_')[1];

        string newLoadFile = string.Empty;
        if (fcndic_coord.ContainsKey(coordA))
        {
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts[0] == coordA) // Remove its entry by not writing it
                {
                    // Skip
                    continue;
                }
                else if (line.Contains(coordA)) // it's a connection, so nullify it
                {
                    newLoadFile += parts[0] + "\t";
                    for (int i = 1; i <= 4; ++i)
                    {
                        if (parts[i] == coordA)
                        {
                            newLoadFile += "null";
                        }
                        else
                        {
                            newLoadFile += parts[i];
                        }
                        newLoadFile += (i < 4) ? "\t" : "\n";
                    }
                }
                else // not involved in this line, so leave it
                {
                    newLoadFile += line + "\n";
                }
            }

            Object.DestroyImmediate(waypointA.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        //Debug.Log(newLoadFile.Trim());

        fcndic.Clear();
        fcndic_coord.Clear();
        SetupFCNDic();
        LoadNodesFromString(newLoadFile.Trim());

        System.IO.File.WriteAllText(Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + loadTextAsset.name + ".txt", newLoadFile.Trim());
        Debug.Log("Removal done. " + Application.dataPath + "/ML-Agents/STE_HA3T/RazishWargaming/Data/" + loadTextAsset.name + ".txt");

        HandleUtility.Repaint();
    }

    #endregion
#endif
}
