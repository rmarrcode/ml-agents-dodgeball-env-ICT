using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
//using Ride;
//using Ride.AI;

public class AutomaticWaypointGenerator : MonoBehaviour
{

    public GameObject waypointPrefab;
    public float gridSpacing = 1.0f;
    public float maxHeightDifferential = 2.25f;

    public Transform startingPoint, endingPoint;

    //private NavMesh navMesh;
    private List<Vector3> waypoints = new List<Vector3>();

    private List<List<GameObject>> waypointGrid = new List<List<GameObject>>();
    private List<GameObject> invalidWaypoints = new List<GameObject>();

    public bool deployWaypoints = false;

    public bool showConnections = true, showWaypointMarkers = false;

    [Header("Serialization")]
    public bool saveWaypointData = false;
    public string waypointSaveLocation = string.Empty;

    private void Start()
    {
        if (showConnections || !showWaypointMarkers)
        {
            foreach(Transform wp in transform)
            {
                WaypointView wv = wp.GetComponent<WaypointView>();
                if (!showWaypointMarkers)
                    wv.DisableMarker();
                if (showConnections)
                    wv.DrawDebugConnections();
            }
        }
    }

    public void Update()
    {
        if (saveWaypointData)
        {
            SaveWaypointData(waypointSaveLocation);
            saveWaypointData = false;
        }

        if (deployWaypoints)
        {
            Init();
            deployWaypoints = false;
        }
    }

    void SaveWaypointData(string location)
    {
        string output = string.Empty;

        int areaNum = 0;
        int index = 0;
        foreach(Transform child in transform)
        {
            string[] line = new string[5];
            line[0] = areaNum.ToString();
            line[1] = (index++).ToString();
            line[2] = child.transform.position.ToString();
            string[] coord = child.name.Replace("(", "").Replace(")", "").Split(',');
            line[3] = coord[0];
            line[4] = coord[1];
            output += string.Join("\t", line) + "\n";
        }
        System.IO.File.WriteAllText(location, output);
    }

    void Init()
    {
        //navMesh = GetComponent<NavMesh>();
        GenerateWaypoints(startingPoint.position);
        PrintWaypoints();
    }

    void GenerateWaypoints(Vector3 startPosition)
    {
        int gridSizeX = Mathf.Abs(Mathf.FloorToInt((endingPoint.position.x - startingPoint.position.x) / gridSpacing));
        int gridSizeZ = Mathf.Abs(Mathf.FloorToInt((endingPoint.position.z - startingPoint.position.z) / gridSpacing));
        for (int z = 0; z <= gridSizeZ; z++)
        {
            waypointGrid.Add(new List<GameObject>());
            for (int x = 0; x <= gridSizeX; x++)
            {
                Vector3 point = new Vector3(
                    startPosition.x + x * gridSpacing, 
                    startPosition.y, 
                    startPosition.z + z * gridSpacing);
                /*Vector3 point = new Vector3(
                    _point.x,
                    Globals.api.terrainSystem.GetHeightAboveTerrain(_point),
                    _point.z
                    );*/
                GameObject waypoint = null;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(point, out hit, 100.0f, NavMesh.AllAreas))
                {
                    waypoint = Instantiate(waypointPrefab, hit.position, Quaternion.identity);
                    waypoint.name = "(" + x + "," + z + ")";
                    waypoints.Add(hit.position);
                }
                else 
                {
                    waypoint = Instantiate(waypointPrefab, point, Quaternion.identity);
                    waypoint.name = "(" + x + "," + z + ")_X";
                    waypoint.transform.GetChild(0).GetComponent<Renderer>().enabled = false;
                    invalidWaypoints.Add(waypoint);
                }
                waypointGrid[z].Add(waypoint);
                WaypointView wv = waypoint.GetComponent<WaypointView>();
                if (x-1 >= 0 && (x-1 < waypointGrid[z].Count) && z < waypointGrid.Count)
                {
                    int ii = z;
                    int jj = x - 1;
                    int dir = (int)WaypointView.WaypointViewDirs.W;
                    int oppdir = (int)WaypointView.WaypointViewDirs.E;
                    ConnectWaypoint(wv, ii, jj, dir, oppdir);
                }
                if (z-1 >= 0)
                {
                    if ((x - 1 >= 0) && (x -1 < waypointGrid[z-1].Count))
                    {
                        int ii = z - 1;
                        int jj = x - 1;
                        int dir = (int)WaypointView.WaypointViewDirs.SW;
                        int oppdir = (int)WaypointView.WaypointViewDirs.NE;
                        ConnectWaypoint(wv, ii, jj, dir, oppdir);
                    }

                    if (x < waypointGrid[z - 1].Count)
                    {
                        int ii = z - 1;
                        int jj = x;
                        int dir = (int)WaypointView.WaypointViewDirs.S;
                        int oppdir = (int)WaypointView.WaypointViewDirs.N;
                        ConnectWaypoint(wv, ii, jj, dir, oppdir);
                    }

                    if (x + 1 < waypointGrid[z - 1].Count)
                    {
                        int ii = z - 1;
                        int jj = x + 1;
                        int dir = (int)WaypointView.WaypointViewDirs.SE;
                        int oppdir = (int)WaypointView.WaypointViewDirs.NW;
                        ConnectWaypoint(wv, ii, jj, dir, oppdir);
                    }
                }
                waypoint.transform.SetParent(transform);
            }
        }

        foreach(GameObject invalidWP in invalidWaypoints)
        {
            Destroy(invalidWP);
        }

        StartCoroutine(_DrawDebug());
        IEnumerator _DrawDebug()
        {
            yield return new WaitForEndOfFrame();
            foreach (List<GameObject> lgo in waypointGrid)
            {
                foreach (GameObject go in lgo)
                {
                    if (go != null && go.transform.GetChild(0).GetComponent<Renderer>().enabled)
                        go.GetComponent<WaypointView>().DrawDebugConnections();
                }
            }
        }
    }

    void ConnectWaypoint(WaypointView wv, int ii, int jj, int dir, int oppdir)
    {
        if (Mathf.Abs(wv.transform.position.y - waypointGrid[ii][jj].transform.position.y) <= maxHeightDifferential)
        {
            float pl = PathfindingUtility.CalculatePathLength(wv.transform.position, waypointGrid[ii][jj].transform.position, 5);
            float d = Vector3.Distance(wv.transform.position, waypointGrid[ii][jj].transform.position);
            Debug.Log("pl=" + pl + "; d=" + d + "; pl/d=" + (pl/d));
            if (!float.IsNaN(pl) && (pl < 1.25f * d))
            {
                wv.neighbors[dir] = waypointGrid[ii][jj].GetComponent<WaypointView>();
                waypointGrid[ii][jj].GetComponent<WaypointView>().neighbors[oppdir] = wv;
            }
        }
    }

    void PrintWaypoints()
    {
        string output = "";
        foreach (Vector3 waypoint in waypoints)
        {
            output += string.Format("({0:F1}, {1:F1}, {2:F1})\n", waypoint.x, waypoint.y, waypoint.z);
        }
        Debug.Log(output);
    }

    public WaypointView[] GetRandomWaypointViews(int amt = 2)
    {
        WaypointView[] wvs = new WaypointView[amt];
        for (int i=0; i<amt; ++i)
        {
            wvs[i] = transform.GetChild(Random.Range(0, transform.childCount)).GetComponent<WaypointView>();
        }
        return wvs;
    }
}