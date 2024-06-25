using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//[CustomEditor]
[ExecuteInEditMode]
public class FCNEditor : EditorWindow
{
    bool myBool = false;
    GameObject waypointA, waypointB;
    public FourConnectedNode.DIRECTION additionDirection;

    [MenuItem("Window/Waypoint Connection Editor")]
    static void Init()
    {
        FCNEditor window = (FCNEditor)EditorWindow.GetWindow(typeof(FCNEditor));
        window.Show();
    }

    void OnGUI()
    {
        myBool = EditorGUILayout.Toggle("Test", myBool);
        
        if (GUILayout.Button("Reset Waypoints"))
        {
            waypointA = null;
            waypointB = null;
            Debug.Log("Reset waypoints");
        }
        GUILayout.Label("Waypoint A: " + (waypointA == null ? "" : waypointA.name));
        if (GUILayout.Button("Select Waypoint A"))
        {
            GameObject go = GetSelectedObject();
            if (go != null)
                waypointA = go;
        }
        GUILayout.Label("Waypoint B: " + (waypointB == null ? "" : waypointB.name));
        if (GUILayout.Button("Select Waypoint B"))
        {
            GameObject go = GetSelectedObject();
            if (go != null)
                waypointB = go;
        }

        if (GUILayout.Button("Remove Connection"))
        {
            if (waypointA != null && waypointB != null)
            {
                AdjacencyMatrixUtility amu = (AdjacencyMatrixUtility)FindObjectOfType(typeof(AdjacencyMatrixUtility));
                amu.RemoveConnection(waypointA.transform, waypointB.transform);
            }
        }

        additionDirection = (FourConnectedNode.DIRECTION)EditorGUILayout.EnumPopup("Added Node Direction for node A:", additionDirection);
        if (GUILayout.Button("Add Connection"))
        {
            if (waypointA != null && waypointB != null)
            {
                AdjacencyMatrixUtility amu = (AdjacencyMatrixUtility)FindObjectOfType(typeof(AdjacencyMatrixUtility));
                amu.AddConnection(waypointA.transform, waypointB.transform, additionDirection);
            }
        }

        if (GUILayout.Button("Remove WaypointA"))
        {
            if (waypointA != null)
            {
                AdjacencyMatrixUtility amu = (AdjacencyMatrixUtility)FindObjectOfType(typeof(AdjacencyMatrixUtility));
                amu.RemoveWaypointAndConnections(waypointA.transform);
            }
        }

        if (GUILayout.Button("Remove WaypointB"))
        {
            if (waypointB != null)
            {
                AdjacencyMatrixUtility amu = (AdjacencyMatrixUtility)FindObjectOfType(typeof(AdjacencyMatrixUtility));
                amu.RemoveWaypointAndConnections(waypointB.transform);
            }
        }

    }

    /*void OnDrawGizmosSelected()
    {
        if (waypointA != null && waypointB != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(waypointA.transform.position, waypointB.transform.position);
        }
    }

    void OnDrawGizmos()
    {
        if (waypointA != null && waypointB != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(waypointA.transform.position, waypointB.transform.position);
        }
    }*/

    void Update()
    {
        if (myBool)
        {
            GetSelectedObject();
            AdjacencyMatrixUtility amu = (AdjacencyMatrixUtility)FindObjectOfType(typeof(AdjacencyMatrixUtility));
            Debug.Log("amu=" + amu.gameObject.name);
            myBool = false;
        }
    }

    static GameObject GetSelectedObject()
    {
        GameObject sel = Selection.activeGameObject;
        if (sel.GetComponent<WaypointMono>() != null)
        {
            Debug.Log("Selected Object = " + sel);
            return sel;
        }
        else if (sel.transform.parent.GetComponent<WaypointMono>() != null)
        {
            Debug.Log("Selected Object Parent = " + sel.transform.parent.gameObject);
            return sel.transform.parent.gameObject;
        }
        return null;
    }
}
