using UnityEngine;

public class GameManager : MonoBehaviour
{
    public AgentA agent1;  // Reference to the first agent
    public AgentB agent2;  // Reference to the second agent

    void Start()
    {
        if (agent1 != null && agent2 != null)
        {
            agent1.otherAgent = agent2;  // Assign agent2 as the other agent for agent1
            agent2.otherAgent = agent1;  // Assign agent1 as the other agent for agent2
        }
        else
        {
            Debug.LogError("Agents are not assigned in the GameManager.");
        }
    }
}
