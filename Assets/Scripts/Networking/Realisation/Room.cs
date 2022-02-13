using System;
using System.Collections.Generic;
using UniRx;
using Zenject;
using System.Linq;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class Room :NetworkObject
    {
        #region Factory
        public class Factory : PlaceholderFactory<NetworkAPI.Room, Room> { }
        #endregion
        
        #region public Properties
        public IObservable<NetworkAPI.Player> ToEnter => _players?.ObserveAdd().Select(x=>x.Value).ObserveOnMainThread();
        public IObservable<NetworkAPI.Player> ToLeave => _players?.ObserveRemove().Select(x => x.Value).ObserveOnMainThread();
        public IEnumerable<NetworkAPI.Player> players => _players;
        private NetworkAPI.Chat chat;
        private Chat _chat;
        public Chat Chat { get => _chat ?? (_chat = chat_factory.Create(chat)); }
        #endregion

        private readonly NetworkAPI.Room room;
        private readonly Chat.Factory chat_factory;
        private ReactiveCollection<NetworkAPI.Player> _players = new ReactiveCollection<NetworkAPI.Player>();

        internal Room(NetworkAPI.Room room, Chat.Factory chat_factory, Network network) : base(network)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.chat_factory = chat_factory;
        }

        #region public Interface
        public void Enter()
        {
            network.Send(new NetworkAPI.Room.Enter() { id = room.id });
            network.SubscribeRespones<NetworkAPI.Room.Init>(x=> x.id == room.id)
                .Subscribe(
                x=>
                {
                    foreach (NetworkAPI.Player p in x.GetPlayers())
                        _players.Add(p);
                    chat = x.chat;
                });
            network.SubscribeRespones<NetworkAPI.Room.PlayerEnter>(x => x.id == room.id)
                   .Subscribe(x=> _players.Add(x.player));
            network.SubscribeRespones<NetworkAPI.Room.PlayerLeave>(x => x.id == room.id)
                   .Subscribe(x => _players.Remove(x.player));
        }

        public void Leave()
        {
            network.Send(new NetworkAPI.Room.Leave() { id = room.id });
            _players.Clear();
            _players.Dispose();
            _players = new ReactiveCollection<NetworkAPI.Player>();
        }
        #endregion
    }
}
