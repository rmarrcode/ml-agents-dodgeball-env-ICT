using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(RayPerceptionSensorComponent3D))]
public class RayComponentTester : MonoBehaviour
{
    RayPerceptionSensorComponent3D[] sensors;

    public int numExtents = 0;
    public string detectableTags = "";
    [TextArea(15,100)]
    public string extents = "";
    [TextArea(15, 100)]
    public string rayOutputString = "";


    void Awake()
    {
        sensors = GetComponents<RayPerceptionSensorComponent3D>();
    }

    // Update is called once per frame
    void Update()
    {
        detectableTags = "";
        extents = "";
        rayOutputString = "";

        int sensorID = 0;
        foreach (RayPerceptionSensorComponent3D sensor in sensors)
        {
            rayOutputString += "SensorID " + sensorID + " ------------------------\n";
            RayPerceptionInput rpi = sensor.GetRayPerceptionInput();
            numExtents = rpi.Angles.Count;
            detectableTags = string.Join(",", rpi.DetectableTags);

            for (int i = 0; i < numExtents; ++i)
            {
                (Vector3 a, Vector3 b) = rpi.RayExtents(i);
                extents += i + ": " + a.ToString() + "," + b.ToString() + "\n";
            }

            ISensor[] ss = sensor.CreateSensors();

            RayPerceptionOutput rpo = RayPerceptionSensor.Perceive(rpi);

            RayPerceptionOutput.RayOutput[] ro = rpo.RayOutputs;
            //Debug.Log("#sensors=" + ss.Length + "; sensor.RaySensor.RayPerceptionOutput null? " + (ro == null));

            if (ro != null)
            {
                for (int j = 0; j < ro.Length; ++j)
                {
                    //float[] fa = new float[] { };
                    //ro[j].ToFloatArray(0, j, fa);
                    rayOutputString += j + ": " + ro[j].HitTaggedObject + "; " + ro[j].HitFraction + "; " + (ro[j].HitGameObject?.name ?? "NULL") /*"; " + string.Join(",", fa)+*/ + "\n";
                }
            }
            sensorID++;
        }
    }
}
