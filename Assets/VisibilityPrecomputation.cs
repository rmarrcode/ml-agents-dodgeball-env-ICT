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
    public float viewDistance = 10f;
    public float viewAngle = 90f;
    public int numRays = 100;

    private Dictionary<(Vector3 position, Vector3 angle), List<Vector3>> visibilityMap;
    public GameObject visibilityMarkerPrefab;  

    private void Start()
    {
        PrecomputeVisibility();
        obstacleMask = LayerMask.GetMask("obstacleMask");
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
                    if (Physics.Raycast(position, rayDirection, out RaycastHit hit, viewDistance, obstacleMask))
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
        // TODO make tolerance global variable
        List<Vector3> vp = GetVisiblePositions(positionX, angleX);
        //bool visible = vp.Contains(positionY);
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
        List<Vector3> visiblePositions = GetVisiblePositions(position, angle);
        foreach (var visiblePosition in visiblePositions)
        {
            Instantiate(visibilityMarkerPrefab, visiblePosition, Quaternion.identity);
        }
    }

}
