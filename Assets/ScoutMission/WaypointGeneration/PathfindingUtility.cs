using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PathfindingUtility : MonoBehaviour
{
    public static float CalculatePathLength(Vector3 startPos, Vector3 endPos, int numTries)
    {
        float runningTotal = 0f;
        int numValidPaths = 0;
        for (int i=0; i<numTries; ++i)
        {
            float d = CalculatePathLength(startPos, endPos);
            if (!float.IsNaN(d))
            {
                runningTotal += d;
                numValidPaths++;
            }
        }

        if (numValidPaths == 0)
        {
            return float.NaN;
        }
        else
        {
            return runningTotal / (float)numValidPaths;
        }
    }

    public static float CalculatePathLength(Vector3 startPos, Vector3 endPos)
    {
        NavMeshPath path = new NavMeshPath();
        bool didCalculate = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, path);
        if (didCalculate)
        {
            return PathLength(path);
        }
        else
        {
            return float.NaN;
        }
    }

    public static float PathLength(NavMeshPath path)
    {
        if (path.corners.Length < 2)
            return 0;

        float lengthSoFar = 0.0F;
        for (int i = 1; i < path.corners.Length; i++)
        {
            lengthSoFar += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        return lengthSoFar;
    }
}
