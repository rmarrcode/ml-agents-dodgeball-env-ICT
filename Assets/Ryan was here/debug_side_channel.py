from mlagents_envs.side_channel.side_channel import SideChannel, IncomingMessage
import uuid

class DebugSideChannel(SideChannel):
    def __init__(self):
        super().__init__(uuid.UUID("d26b7e1b-78a5-4dd7-8b9a-799b6c82b5d3"))
        self.messages = []

    def on_message_received(self, msg: IncomingMessage) -> None:
        message = msg.read_string()
        self.messages.append(message)
        print(f"Debug Message from Unity: {message}")

    def get_and_clear_messages(self):
        messages = self.messages[:]
        self.messages = []
        return messages