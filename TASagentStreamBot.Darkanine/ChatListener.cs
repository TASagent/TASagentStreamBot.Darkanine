using TASagentTwitchBot.Plugin.XInput;

namespace TASagentTwitchBot.Darkanine
{
    public class ChatListener
    {
        private readonly Core.ICommunication communication;
        private readonly IButtonPressDispatcher buttonPressDispatcher;

        public ChatListener(
            Core.ICommunication communication,
            IButtonPressDispatcher buttonPressDispatcher)
        {
            this.communication = communication;
            this.buttonPressDispatcher = buttonPressDispatcher;

            communication.ReceiveMessageHandlers += ReceiveMessageHandler;
        }

        private void ReceiveMessageHandler(Core.IRC.TwitchChatter chatter)
        {
            if (chatter.User.AuthorizationLevel <= Core.Commands.AuthorizationLevel.Restricted)
            {
                //Ignore restricted users
                return;
            }

            switch (chatter.Message.ToUpperInvariant())
            {
                case "F":
                    //Press F
                    buttonPressDispatcher.TriggerKeyPress(DirectXKeyStrokes.DIK_F, 50);
                    break;

                case "GG":
                    communication.SendPublicChatMessage($"Shut up, @{chatter.User.TwitchUserName}!");
                    break;

                default:
                    //Do Nothing
                    break;
            }
        }
    }
}
