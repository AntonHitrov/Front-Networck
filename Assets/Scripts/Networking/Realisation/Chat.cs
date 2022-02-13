using System;
using UniRx;
using Zenject;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class Chat :NetworkObject
    {
        public class Factory : PlaceholderFactory<NetworkAPI.Chat, Chat> { }

        private readonly NetworkAPI.Chat chat_token;
        private readonly CurrentPlayer player;

        private Subject<NetworkAPI.Chat.NewMessage> _newMessage = new Subject<NetworkAPI.Chat.NewMessage>();
        public IObservable<NetworkAPI.Chat.NewMessage> NewMessage => _newMessage.ObserveOnMainThread();


        public Chat(NetworkAPI.Chat chat_token, CurrentPlayer player, Network network):base(network)
        {
            this.chat_token = chat_token ?? throw new ArgumentNullException(nameof(chat_token));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            network.Send(new NetworkAPI.Chat.Subscribe() { name = chat_token.name });
            network.SubscribeRespones<NetworkAPI.Chat.NewMessage>((x) => x.name == chat_token.name).Subscribe(_newMessage);
        }
        

        public void Close()
        {
            network.Send(new NetworkAPI.Chat.Unsubscribe() { name = chat_token.name },LiteNetLib.DeliveryMethod.ReliableOrdered);
            _newMessage?.OnCompleted();
            _newMessage = null;
        }

        public void Send(string message)
            => network.Send(
                new NetworkAPI.Chat.Send()
                {
                    name = chat_token.name,
                    message = message,
                    player = new NetworkAPI.Player()
                    {
                        id = player.id
                    }
                });
    }
}
