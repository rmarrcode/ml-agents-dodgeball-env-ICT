using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class WaypointMono : SerializedMonoBehaviour
{
    //Waypoint m_data = new Waypoint();
    public bool hideAtStart = true;
    public GameObject mesh;
    public Dictionary<FourConnectedNode.DIRECTION, WaypointMono> neighbors = new Dictionary<FourConnectedNode.DIRECTION, WaypointMono>();
    public string waypointID 
    { 
        get
        {
            return gameObject.name.Split('_')[1];
        } 
    }

/*
#pragma warning disable CS0649
    [SerializeField] [BitMask(typeof(WaypointFlags))] WaypointFlags m_flags;
    [SerializeField] float m_radius;
#pragma warning restore CS0649
*/

    public Vector3 position = new Vector3();

    public Quaternion rotation = new Quaternion();

    //public WaypointFlags flags { get { return m_data.flags; } set { m_flags = m_data.flags = value; } }
    public float radius;// { get { return m_data.radius; } set { m_data.radius = value; } }


    public void SetPosition(string positionString)
    {
        string[] strCoords = positionString.Remove(positionString.Length - 1, 1).Remove(0,1).Split(',');
        Debug.Log("PositionString=" + positionString + "|" + string.Join("|",strCoords));

        position = new Vector3(float.Parse(strCoords[0].Trim()), float.Parse(strCoords[1].Trim()), float.Parse(strCoords[2].Trim()));
        transform.position = position;
    }


    //public Waypoint data { get { return m_data; } }
    //public IEnumerable<RideParameter> tags { get { return m_tags; } }
    /*public void Awake()
    {
        flags = m_flags;
        radius = m_radius;
        m_data.attributes = attributes = m_attributes; // TODO: fix this
        m_data.position = transform.position;
        //foreach (EntityTag tag in m_tags)
        //{
        //    if (!string.IsNullOrEmpty(tag.tag))
        //    {
        //        tags.Add(tag.tag, tag.value);
        //    }
        //}
    }

    public void Init(RideID id, Vector3 position, Quaternion rotation, WaypointFlags flags, float radius)
    {
        this.id = id;
        this.position = position;
        this.rotation = rotation;
        this.flags = flags;
        this.name = "Waypoint" + id.ToString();
        this.radius = radius;
    }*/

    void Start()
    {
        if (hideAtStart)
        {
            if (mesh != null)
            {
                mesh.SetActive(!hideAtStart);
            }

        }
    }
}
