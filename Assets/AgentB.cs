using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class AgentB : Agent
{
    private Rigidbody agentRigidbody;
    private VisibilityPrecomputation visibilityPrecomputation;
    public AgentA otherAgent; 
    //public GameObject otherAgentObject;
    public GameObject plane;

    public override void Initialize()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        if (agentRigidbody == null)
        {
            agentRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        agentRigidbody.freezeRotation = true;
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        visibilityPrecomputation = FindObjectOfType<VisibilityPrecomputation>();
    }

    private HashSet<Vector3> obstacles = new HashSet<Vector3>
    {
        new Vector3(3f, 0f, 0f),
        new Vector3(3f, 0f, 1f),
        new Vector3(3f, 0f, 2f),
        new Vector3(3f, 0f, 3f),
        new Vector3(3f, 0f, 4f),
        new Vector3(3f, 0f, 5f),
        new Vector3(4f, 0f, 7f),
        new Vector3(6f, 0f, 9f),
        new Vector3(6f, 0f, 8f),
        new Vector3(6f, 0f, 7f),
        new Vector3(6f, 0f, 6f),
        new Vector3(6f, 0f, 5f),
        new Vector3(6f, 0f, 4f),
        new Vector3(5f, 0f, 2f),
    };

    public override void OnEpisodeBegin()
    {
        //visibilityPrecomputation.PrecomputeVisibility();
        Vector3 testPosition = new Vector3(6.5f, .5f, .5f);
        Vector3 testAngle = new Vector3(0, 0, 0);
        //visibilityPrecomputation.PrintVisibilityMap(testPosition, testAngle);
        //visibilityPrecomputation.HighlightVisiblePositions(testPosition, testAngle);

        transform.localPosition = testPosition;
        transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(transform.localEulerAngles);
    }

    private float moveCooldown = 0.2f;
    private float nextMoveTime = 0f;

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (Time.time < nextMoveTime)
        {
            return;
        }
        nextMoveTime = Time.time + moveCooldown;
        int action = actions.DiscreteActions[0];
        Vector3 currentPosition = transform.localPosition;
        float moveStep = 1f;
        Vector3 moveDirection = Vector3.zero;

        switch (action)
        {
            case 1:
                moveDirection = Vector3.right;
                currentPosition.x += moveStep;
                break;
            case 2:
                moveDirection = Vector3.left;
                currentPosition.x -= moveStep;
                break;
            case 3:
                moveDirection = Vector3.forward;
                currentPosition.z += moveStep;
                break;
            case 4:
                moveDirection = Vector3.back;
                currentPosition.z -= moveStep;
                break;
        }

        Vector3 adjustedCurrentPosition = currentPosition;
        adjustedCurrentPosition.x -= .5f;
        adjustedCurrentPosition.y -= .5f;
        adjustedCurrentPosition.z -= .5f;

        bool contains = ContainsVector3(obstacles, adjustedCurrentPosition);

        if (!contains)
        {
            if (adjustedCurrentPosition.x >= -.5 && adjustedCurrentPosition.x <= 9.5 && adjustedCurrentPosition.z >= -.5 && adjustedCurrentPosition.z <= 9.5)
            {
                transform.localPosition = currentPosition;
                if (moveDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(moveDirection);
                }
            }
        }

        visibilityPrecomputation.HighlightVisiblePositions(transform.localPosition, transform.localEulerAngles);

        if (IsOtherAgentVisible()) 
        {
            otherAgent.Eliminate();
            SetReward(1f);
            EndEpisode();
        }
        
    }

    private bool IsOtherAgentVisible()
    {
        // rotation???
        bool visible = visibilityPrecomputation.AgentXSpotsAgentY(transform.localPosition, transform.localEulerAngles, otherAgent.transform.localPosition);
        return visible;
    }

    // TODO how are ties handled?
    public void Eliminate()
    {
        // if (IsOtherAgentVisible())
        // {
        //     Debug.Log("seen");
        //     Debug.Log(gameObject.name + " sees " + otherAgent.gameObject.name);
        //     otherAgent.Eliminate();
        //     SetReward(1f);
        //     EndEpisode();
        // }
        //StartCoroutine(ChangePlaneColorTemporarily(Color.red, .5f));
        Debug.Log(gameObject.name + " is eliminated");
        SetReward(-1f);
        EndEpisode();
    }

    private IEnumerator ChangePlaneColorTemporarily(Color newColor, float duration)
    {
        if (plane != null)
        {
            Renderer planeRenderer = plane.GetComponent<Renderer>();
            if (planeRenderer != null)
            {
                Color originalColor = planeRenderer.material.color; 
                planeRenderer.material.color = newColor; 
                yield return new WaitForSeconds(duration);
                planeRenderer.material.color = originalColor; 
            }
        }
    }

    private bool ContainsVector3(HashSet<Vector3> set, Vector3 value, float tolerance = 0.01f)
    {
        foreach (Vector3 vec in set)
        {
            if (Vector3.Equals(vec, value))
            {
                return true;
            }
            if (Vector3.Distance(vec, value) < tolerance)
            {
                return true;
            }
        }
        return false;
    }

    public override void Heuristic(in ActionBuffers actionBuffers)
    {
        var discreteActions = actionBuffers.DiscreteActions;
        discreteActions[0] = 0;
        if (Input.GetKey(KeyCode.D))
        {
            discreteActions[0] = 1;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActions[0] = 2;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            discreteActions[0] = 3;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActions[0] = 4;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Goal>(out Goal goal))
        {
            SetReward(1f);
            EndEpisode();
        }
    }
}
