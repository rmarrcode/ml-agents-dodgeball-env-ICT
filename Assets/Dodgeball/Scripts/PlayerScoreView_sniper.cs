using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class PlayerScoreView_sniper : MonoBehaviour
{
    public DodgeBallAgent dodgeBallAgent;
    private Text textDisplay;
    private Color textColor;

    private void Awake()
    {
        textDisplay = GetComponent<Text>();
        textColor = dodgeBallAgent.teamID == 0 ? Color.blue : Color.magenta;
        textDisplay.color = textColor;
    }

    // Update is called once per frame
    void Update()
    {
        string agent_name = dodgeBallAgent.gameObject.name;
        if (!agent_name.EndsWith(")"))
        {
            agent_name += "_sniper";
        }
        textDisplay.text = agent_name + " => " + dodgeBallAgent.hitScore + ", " + dodgeBallAgent.timesHit;
    }
}
