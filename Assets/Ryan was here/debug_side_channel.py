from mlagents_envs.side_channel.side_channel import SideChannel, IncomingMessage
import uuid

class DebugSideChannel(SideChannel):
    def __init__(self):
        super().__init__(uuid.UUID("d26b7e1b-78a5-4dd7-8b9a-799b6c82b5d3"))
        self.messages = []
        self.last_state = []

    def on_message_received(self, msg: IncomingMessage) -> None:
        message = msg.read_string()
        state = message.split(',')
        state = [float(x) for x in state]
        self.last_state = state
        
    # TODO need to fix
    def get_last_state(self):
        if self.last_state == []:
            raise (Exception("final state empty"))
        state = self.last_state
        #self.last_state = []
        return state