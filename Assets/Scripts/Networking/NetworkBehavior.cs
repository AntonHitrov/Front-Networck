using System;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace Assets.Scripts.Modules.Networking
{
    public class NetworkBehavior 
    {
        delegate void NetworkState(out NetworkState state);

        #region Properties

        #region private
        private readonly NetManager manager;
        private readonly Configuration default_Conf;
        private readonly List<Configuration> configurations;
        private NetworkState state;
        private Realisation.CurrentPlayer player;
        private Configuration CurrentConf;
        private List<Configuration> toConnect;
        private ushort? tryCount;
        private bool hasConnection => ServerConnection != null ? 
                                      ServerConnection.ConnectionState == ConnectionState.Connected : 
                                      false;
        private NetPeer _serverConnection;
        private NetPeer ServerConnection => _serverConnection != null ?
                                            _serverConnection :
                                            _serverConnection = GetCurentServerConnection;
        #endregion
       
        #region piblic
        public event Action<NetPeer> OnConnected;
        public event Action OnDisconected;
        #endregion

        #endregion

        public NetworkBehavior(NetManager manager, Configuration default_Conf, List<Configuration> configurations,Realisation.CurrentPlayer player)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.default_Conf = default_Conf ?? throw new ArgumentNullException(nameof(default_Conf));
            this.configurations = configurations ?? throw new ArgumentNullException(nameof(configurations));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            OnConnected += (peer) => Network.CurrentConnection = peer;
            OnDisconected += () => Network.CurrentConnection = null;
            manager.Start();
            state = FindServer;
            Observable.Timer(TimeSpan.FromSeconds(1))
                      .Repeat()
                      .TakeWhile( _ => state != null)
                      .Subscribe( _ => state.Invoke(out state));
        }

        #region State
        private void BeginConnection(out NetworkState state)
        {
            if (hasConnection)
            {
                OnConnected?.Invoke(ServerConnection);
                state = DefaultHandler;
                return;
            }
            if (WeTryConnectedToServer)
            {
                NetworkObject.Log("We try Connected to Server");
                state = BeginConnection;
                return;
            }
            
            if (_serverConnection != null)
            {
                OnDisconected.Invoke();
                state = Reconnection;
                return;
            }
            else
            {
                state = FindServer;
                return;
            }
            
        }

        private void FindServer(out NetworkState state)
        {
            if (toConnect == null)
            {
                toConnect = configurations.ToList();
                ConnectedTo(default_Conf);
                state = BeginConnection;
                return;
            }
            else
            {
                var newTry = toConnect.FirstOrDefault();
                if (newTry == null)
                {
                    NetworkObject.LogAssertion("Мы не нашли ни одного сервера для соединения");
                    state = null;
                    manager.Stop();
                    return;
                }
                toConnect.Remove(newTry);
                ConnectedTo(newTry);
                state = BeginConnection;
                return;
            }
        }


        private void Reconnection(out NetworkState state)
        {
            if (tryCount == null)
                tryCount = 3;
            if (tryCount == 0)
            {
                throw new Exception("Потрачены все попытки востановить соединение");
            }
            else
            {
                NetworkObject.LogAssertion($"Попытка востановить соединение № {--tryCount}");
                ConnectedTo(CurrentConf);
                state = BeginConnection;
                return;
            }
        }

        private void DefaultHandler(out NetworkState state)
        {
            if (hasConnection)
            {
                state = DefaultHandler;
                if (!player.isInit) player.Init();
            }
            else
            {
                state = BeginConnection;
            }
        }
        #endregion

        #region Func
        /// <summary>
        /// Открываем соединение с сервером
        /// </summary>
        /// <param name="configuration"></param>
        private void ConnectedTo(Configuration configuration)
        {
            CurrentConf = configuration;
            manager.Connect(configuration.addres, configuration.port, configuration.key);
        }
        
        /// <summary>
        /// Получить готовое соединение с сервером
        /// </summary>
        /// <returns></returns>
        private NetPeer GetCurentServerConnection
        {
            get
            {
                List<NetPeer> connectedPeers = new List<NetPeer>();
                manager.GetPeersNonAlloc(connectedPeers, ConnectionState.Connected);
                foreach (var peer in connectedPeers)
                    if (default_Conf.addres == peer.EndPoint.Address.ToString())
                        return peer;
                foreach (var peer in connectedPeers)
                {
                    var addres = peer.EndPoint.Address.ToString();
                    if (configurations.Where(x => x.addres == addres).Count() >= 1)
                    {
                        return peer;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Ожидаем ли мы соединение с одним из серверов?
        /// </summary>
        /// <returns></returns>
        private bool WeTryConnectedToServer
        {
            get
            {
                List<NetPeer> peers = new List<NetPeer>();
                manager.GetPeersNonAlloc(peers, ConnectionState.Outgoing);
                foreach (var peer in peers)
                    if (default_Conf.addres == peer.EndPoint.Address.ToString())
                        return true;
                foreach (var peer in peers)
                {
                    var addres = peer.EndPoint.Address.ToString();
                    if (configurations.Where(x => x.addres == addres).Count() >= 1)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        #endregion
    }
}
