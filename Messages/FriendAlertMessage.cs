using CommunityToolkit.Mvvm.Messaging.Messages;

namespace FENS_Connect.Messages
{
    public class FriendAlertMessage : ValueChangedMessage<IDictionary<string, string>>
    {
        public FriendAlertMessage(IDictionary<string, string> value)
            : base(value)
        {
        }
    }
}