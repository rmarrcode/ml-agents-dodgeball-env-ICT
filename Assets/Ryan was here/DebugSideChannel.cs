using System;
using System.Collections.Generic;
using Unity.MLAgents.SideChannels;
using UnityEngine;

public class DebugSideChannel : SideChannel
{
    public DebugSideChannel()
    {
        ChannelId = new Guid("d26b7e1b-78a5-4dd7-8b9a-799b6c82b5d3");
    }

    protected override void OnMessageReceived(IncomingMessage msg)
    {
        // Handle messages from Python if needed
    }

    public void SendDebugMessage(string message)
    {
        using (var msg = new OutgoingMessage())
        {
            msg.WriteString(message);
            QueueMessageToSend(msg);
        }
    }
}
