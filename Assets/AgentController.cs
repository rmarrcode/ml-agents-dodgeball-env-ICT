using UnityEngine;

public class AgentController : MonoBehaviour
{
    public float moveSpeed = 2f;
    private Vector3 targetPosition;
    private bool[,] blockedSpaces;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            MoveToTarget();
        }
        else
        {
            HandleInput();
        }
    }

    void MoveToTarget()
    {
        Vector3 direction = (targetPosition - transform.position).normalized;

        if (direction != Vector3.zero && Vector3.Angle(transform.forward, direction) > 0.1f) 
        {
            Vector3 up = Vector3.up;
            Quaternion lookRotation = Quaternion.LookRotation(direction, up);
            Debug.Log("Look Rotation Euler Angles: " + lookRotation.eulerAngles);
            transform.rotation = lookRotation;
        }

        // Move towards the target position
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            targetPosition += Vector3.forward * 1.0f;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            targetPosition += Vector3.back * 1.0f;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            targetPosition += Vector3.left * 1.0f;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            targetPosition += Vector3.right * 1.0f;
        }
    }
}
