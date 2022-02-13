using System;
using UniRx;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class CurrentPlayer:NetworkObject
    {
        internal byte[] id => construct?.id;

        #region private properties
        private NetworkAPI.Player.Construct construct;
        private bool isLoaded => construct != null;
        private bool HasPrivateKey => PlayerPrefs.HasKey(nameof(CurrentPlayer));
        private string PrivateKey => String.IsNullOrEmpty(_privateKey) ? _privateKey = PlayerPrefs.GetString(nameof(CurrentPlayer)) : _privateKey;
        private string _privateKey;
        private void SavePrivateKey(string key) => PlayerPrefs.SetString(nameof(CurrentPlayer), key);
        #endregion

        #region public propreties
        public Container Container => container.Invoke(); 
        private readonly Func<Container> container;

        public Group Group => group.Invoke();  
        private readonly Func<Group> group;

        public Room Room => room.Invoke();
        private Func<Room> room;
        #region utill
        private Func<T> BuildGeter<T>(Func<T> factory) where T:class
        {
            T value = null;
            Func<T> builder = () => value = factory.Invoke();
            Func<T> builderAndWait = () => { Wait(); return builder.Invoke(); };
            return () => value != null ? value : isLoaded ? builder.Invoke() : builderAndWait.Invoke();
        }

        #endregion
        #endregion


        internal CurrentPlayer(Network network,DiContainer diContainer) : base(network)
        {
            container = BuildGeter(() => diContainer.Resolve<Container.Factory>().Create(construct.container) );
            group = BuildGeter(()=> diContainer.Resolve<Group.Factory>().Create(construct.group));
            room = BuildGeter(() => construct.room != null ? diContainer.Resolve<Room.Factory>().Create(construct.room) : null);
        }

        #region Initialise
        internal bool isInit { get; private set; }
        /// <summary>
        /// Загружаем или создаём нового игрока
        /// </summary>
        internal void Init()
        {
            if (isInit) return;
            Log("Begin init current player");
            if (HasPrivateKey)
            {
                network
                    .Request<NetworkAPI.Player.Init, NetworkAPI.Player.Construct>
                    (
                            new NetworkAPI.Player.Init() { UserPrivateID = PrivateKey },
                            x => x.ID_ToString == PrivateKey
                    ).Subscribe(
                    (player) =>
                    {
                        construct = player;
                        Log("inited current player");
                        Await();
                    });
            }
            else
            {
                network.Request<NetworkAPI.Player.Registrate, NetworkAPI.Player.Construct>
                    (
                        new NetworkAPI.Player.Registrate()
                        {
                            UserPrivateID = "HZ"
                        },
                        x => true
                    ).Subscribe(
                        (player) =>
                        {
                            Log("inited current player");

                            construct = player;
                            Observable
                            .Timer(TimeSpan.FromSeconds(1))
                            .ObserveOnMainThread()
                            .Subscribe((_)=>{ SavePrivateKey(player.ID_ToString); });
                            Await();
                        });
            }
            isInit = true;
        }
        #endregion




    }
}
