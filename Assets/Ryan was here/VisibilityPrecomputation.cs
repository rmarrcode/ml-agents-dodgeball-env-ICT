using System.Collections.Generic;
using UnityEngine;

public class VisibilityPrecomputation : MonoBehaviour
{
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSize = 1f;
    public List<Vector3> angles = new List<Vector3>
    {
        new Vector3(0, 0, 0),
        new Vector3(0, 90, 0),
        new Vector3(0, 180, 0),
        new Vector3(0, 270, 0)
    };

    public LayerMask obstacleMask;
    public float viewDistance = 100f;
    public float viewAngle = 90f;
    public int numRays = 100;

    private Dictionary<(Vector3 position, Vector3 angle), List<Vector3>> visibilityMap;
    public GameObject visibilityMarkerPrefab;  

    public static VisibilityPrecomputation Instance { get; private set; }
    private List<GameObject> instantiatedMarkers = new List<GameObject>();


    private void Awake() { 
        if (Instance != null && Instance != this) 
        { 
            Destroy(this); 
        } 
        else 
        { 
            Instance = this; 
        } 
    }
    // private void Awake()
    // {
    //     // Ensure only one instance exists
    //     if (Instance == null)
    //     {
    //         Instance = this;
    //         // Optionally, you can keep this object persistent across scenes:
    //         DontDestroyOnLoad(gameObject);
    //     }
    //     else
    //     {
    //         Destroy(gameObject); // Destroy duplicate instances
    //     }
    // }

    private void Start()
    {
        PrecomputeVisibility();
        obstacleMask = LayerMask.GetMask("obstacleMask");
        Vector3 position = new Vector3(6.5f, .5f, 3.5f);
        Vector3 angle = new Vector3(0f, 270f, 0f);
        // Vector3 targetPosition = new Vector3(5.5f, .5f, 4.5f);

        //HighlightVisiblePositions(position, angle);
        // foreach (Vector3 v in visibilityMap[(position, angle)])
        // {
        //     Debug.LogFormat("pos {0}", v);
        // }
        // Vector3 targetPosition = new Vector3(5.5f, .5f, 4.5f);
        // Vector3 rayDirection = (targetPosition - position).normalized;
        // bool gets_hit = Physics.Raycast(position, rayDirection, out RaycastHit hit, viewDistance, obstacleMask);
        // Debug.LogFormat("collider {0} point {1} getshit{2}", hit.collider.ToString(), hit.point.ToString(), gets_hit);

        // if (gets_hit)
        // {
        //     if (hit.collider != null && Vector3.Distance(position, hit.point) > Vector3.Distance(position, targetPosition))
        //     {
        //         Debug.Log("A");
        //     }
        // }
        // else
        // {
        //     Debug.Log("B");
        // }

        List<Vector3> visiblePositions = ComputeVisiblePositions(position, Quaternion.Euler(angle) * Vector3.forward);

        // Debug.Log("--------------------");
        // foreach (Vector3 vector in visibilityMap[(position, angle)])
        // {
        //     Debug.Log(vector);
        // }
    }

    public void PrecomputeVisibility()
    {
        visibilityMap = new Dictionary<(Vector3 position, Vector3 angle), List<Vector3>>();
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector3 position = new Vector3(x * cellSize + .5f, 0.5f, z * cellSize + .5f);
                foreach (Vector3 angle in angles)
                {
                    List<Vector3> visiblePositions = ComputeVisiblePositions(position, Quaternion.Euler(angle) * Vector3.forward);
                    //List<Vector3> visiblePositions = ComputeVisiblePositions(position, angle);
                    visibilityMap[(position, angle)] = visiblePositions;
                }
            }
        }
        Debug.Log("Visibility precomputation completed.");
    }

    private List<Vector3> ComputeVisiblePositions(Vector3 position, Vector3 direction)
    {
        List<Vector3> visiblePositions = new List<Vector3>();
        float halfAngle = viewAngle / 2f;
        for (float x = 0.5f; x <= 9.5f; x += cellSize)
        {
            for (float z = 0.5f; z <= 9.5f; z += cellSize)
            {
                Vector3 targetPosition = new Vector3(x, position.y, z);
                Vector3 rayDirection = (targetPosition - position).normalized;
                float angleToTarget = Vector3.Angle(direction, rayDirection);
                if (angleToTarget <= halfAngle)
                {
                    bool gets_hit = Physics.Raycast(position, rayDirection, out RaycastHit hit, viewDistance, obstacleMask);
                    if (gets_hit)
                    {
                        if (hit.collider != null && Vector3.Distance(position, hit.point) > Vector3.Distance(position, targetPosition))
                        {
                            visiblePositions.Add(targetPosition);
                        }
                    }
                    else
                    {
                        visiblePositions.Add(targetPosition);
                    }
                }
            }
        }
        return visiblePositions;
    }

    public bool AgentXSpotsAgentY(Vector3 positionX, Vector3 angleX, Vector3 positionY)
    {        
        List<Vector3> vp = GetVisiblePositions(positionX, angleX);

        foreach (Vector3 point in vp)
        {
            if (Vector3.Distance(point, positionY) <= .001)
            {
                return true;
            }
        }
        return false;
    }

    public List<Vector3> GetVisiblePositions(Vector3 position, Vector3 angle)
    {
        if (visibilityMap.TryGetValue((position, angle), out List<Vector3> visiblePositions))
        {
            return visiblePositions;
        }
        return new List<Vector3>();
    }

    public void PrintVisibilityMap(Vector3 position, Vector3 angle)
    {
        List<Vector3> visiblePositions = GetVisiblePositions(position, angle);
        Debug.Log($"Position: {position}, Angle: {angle}");
        foreach (var visiblePosition in visiblePositions)
        {
            Debug.Log($"  Visible Position: {visiblePosition}");
        }
    }

    public void AddVisibilityEntry(Vector3 position, Vector3 angle, List<Vector3> visiblePositions)
    {
        visibilityMap[(position, angle)] = visiblePositions;
    }

    public void ModifyVisibilityEntry(Vector3 position, Vector3 angle, Vector3 newVisiblePosition)
    {
        var key = (position, angle);
        if (visibilityMap.ContainsKey(key))
        {
            visibilityMap[key].Add(newVisiblePosition);
        }
    }


    public void HighlightVisiblePositions(Vector3 position, Vector3 angle)
    {
        DestroyVisibleMarkers();
        List<Vector3> visiblePositions = GetVisiblePositions(position, angle);
        // Debug.LogFormat("position {0} angle {1} no_positions {2}", position, angle, visiblePositions.Count);
        // foreach (Vector3 v in visiblePositions) {
        //     Debug.LogFormat("visible position {0}", v);
        // }
        foreach (var visiblePosition in visiblePositions)
        {
            Vector3 orbPosition = new Vector3(visiblePosition.x, visiblePosition.y + 1f, visiblePosition.z);
            GameObject marker = Instantiate(visibilityMarkerPrefab, orbPosition, Quaternion.identity);
            instantiatedMarkers.Add(marker); 
        }
    }

    private void DestroyVisibleMarkers()
    {
        foreach (var marker in instantiatedMarkers)
        {
            Destroy(marker); 
        }
        instantiatedMarkers.Clear(); 
    }

}
