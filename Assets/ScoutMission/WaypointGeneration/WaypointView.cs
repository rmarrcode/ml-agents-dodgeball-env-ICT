using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointView : MonoBehaviour
{
    public int index;
    private static int numDirs = 8;
    public int xCoordinate, zCoordinate;
    public Vector3 pos = Vector3.zero;
    public bool taken = false;
    public enum WaypointViewDirs
    {
        N = 0,
        NE = 1,
        E = 2,
        SE = 3,
        S = 4,
        SW = 5,
        W = 6,
        NW = 7
    }
    [Header("0:N, 1:NE, 2:E, 3:SE, 4:S, 5:SW, 6:W, 7:NW")]
    public WaypointView[] neighbors = new WaypointView[numDirs];
    public bool listOff = false;
    //private string prevString = "";

    public double occlusionFromStart = -1.0;


    public void Start(){
        #if !UNITY_EDITOR
        transform.GetChild(0).gameObject.SetActive(false);
        #endif
    }

    /*public void Update(){
        if (listOff){
            ListOff();
            listOff = false;
        }
        // string text = GetComponent<TextMesh>().text;
        // if (!text.Equals("") && !text.Equals(preString)){
        //     GetComponent<TextMesh>().text = "";
        // }
        // prevString = GetComponent<TextMesh>().text;

    }*/

    public void DisableMarker()
    {
        foreach(Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
    }

    public int GetOppositeDirection(int currentDir)
    {
        return (currentDir+numDirs/2) % numDirs;
    }

    public WaypointView GetNearestWaypointToAngle(float theta){
        WaypointView nearestWaypoint = null;
        float nearestAngle = (float) Math.PI; //180 degrees away is max angle distance
        int bestIndex = -1; //only for debugging

        for (int i=0; i<8; i++){//foreach (WaypointView neighbor in neighbors){
            WaypointView neighbor = neighbors[i];
            if (neighbor==null) {
                // Debug.Log("neighbor " + i + " was null.");
                continue;
            }
            float deltaX = neighbor.transform.position.x - transform.position.x;
            float deltaZ = neighbor.transform.position.z - transform.position.z;

            //TODO: fix if this can return more than pi ...it's -pi to pi, so the range is 2pi
            float theta2 = Mathf.Atan2(deltaZ, deltaX); //angle from x axis
            float deltaTheta = GetDeltaTheta(theta2, theta);
            Debug.DrawRay(transform.position, ((float) Math.PI - deltaTheta) * (neighbor.transform.position - transform.position), Color.white);            

            if (deltaTheta < nearestAngle){
                nearestAngle = deltaTheta;
                nearestWaypoint = neighbor;
                // Debug.Log("neighbor " + i + " is now the best, as it was better than " + bestIndex);
                bestIndex = i;
            } else {
                // Debug.Log("neighbor " + i + " is worse than " + bestIndex + " because " + deltaTheta + " is larger than " + nearestAngle);
            }
        }
        return nearestWaypoint;
    }

    private float GetDeltaTheta(float theta1, float theta2){
        float deltaTheta = theta2 - theta1;
        if (deltaTheta > Math.PI){
            deltaTheta = (float)(2*Math.PI - deltaTheta);
        } else if (deltaTheta < -Math.PI){
            deltaTheta = (float)(-2*Math.PI + deltaTheta);
        }
        // Debug.Log("Angles: " + theta2 + " - " + theta1 + " = " + deltaTheta);
        return Math.Abs(deltaTheta);
    }

    public float[] GetAnglesOfNeighbors(){ //in order of neighbors
        float[] angles = new float[numDirs];

        for (int i=0; i<numDirs; i++){
            if (neighbors[i] == null) angles[i] = -1;
            else {
                angles[i] = Mathf.Atan2(neighbors[i].transform.position.z - transform.position.z,
                    neighbors[i].transform.position.x - transform.position.x);
            }
        }
        return angles;
    }

    /*public void ListOff(){
        // Debug.Log("Neighbors for waypoint " + index + ":");
        for(int i=0; i< numDirs; i++){
            if (neighbors[i] == null){
                // Debug.Log("\tNeighbor " + i + " = " + neighbors[i] + ", is null");
            } else {
                // Debug.Log("\tNeighbor " + i + " = " + neighbors[i] + ", is not null");
            }
            
        }
    }*/

    public void DrawDebugConnections()
    {
        if (neighbors[(int)WaypointViewDirs.N] != null)
        {
            Debug.DrawLine(transform.position, neighbors[(int)WaypointViewDirs.N].transform.position, Color.red, 1e6f);
        }

        if (neighbors[(int)WaypointViewDirs.NE] != null)
        {
            Debug.DrawLine(transform.position, neighbors[(int)WaypointViewDirs.NE].transform.position, Color.yellow, 1e6f);
        }

        if (neighbors[(int)WaypointViewDirs.E] != null)
        {
            Debug.DrawLine(transform.position, neighbors[(int)WaypointViewDirs.E].transform.position, Color.green, 1e6f);
        }

        if (neighbors[(int)WaypointViewDirs.SE] != null)
        {
            Debug.DrawLine(transform.position, neighbors[(int)WaypointViewDirs.SE].transform.position, Color.magenta, 1e6f);
        }
    }
}
