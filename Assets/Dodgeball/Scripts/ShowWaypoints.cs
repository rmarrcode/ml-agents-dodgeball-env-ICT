using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowWaypoints : MonoBehaviour
{
    // Start is called before the first frame update
    public bool debug = false;
    public GameObject waypoints;
    void Start()
    {
        Debug.Log(waypoints.transform.childCount);
        for (int i = 0; i < waypoints.transform.childCount; i++)
        {
            GameObject wp = waypoints.transform.GetChild(i).gameObject;
            for (int j = 0; j < 8; j++)
            {
                if (wp.GetComponent<WaypointView>().neighbors[j] != null)
                {
                    Debug.Log("here");
                    Debug.DrawLine(wp.transform.position, wp.GetComponent<WaypointView>().neighbors[j].transform.position, Color.white, 100f);
                }
            }
        }
    }
}
