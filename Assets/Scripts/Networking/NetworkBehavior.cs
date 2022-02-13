using System;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace Assets.Scripts.Modules.Networking
{
    public class NetworkBehavior 
    {
        #region ctr Parametrs
        NetManager manager;
        Configuration default_Conf;
        List<Configuration> configurations;
        #endregion

        #region ParamToState

        #region findServer
        private class TryToConnect
        {
           public List<Configuration> NotTry;
        }
        TryToConnect toConnect;
        #endregion

        #region reconnection
        ushort? tryCount = null;
        #endregion

        bool hasConnection => ServerConnection != null ? ServerConnection.ConnectionState == ConnectionState.Connected : false;

        NetPeer ServerConnection => _serverConnection != null ? _serverConnection : _serverConnection = CurentServerConnection();
        NetPeer _serverConnection;

        Configuration CurrentConf;

        #endregion

        NetworkState state;
        private Realisation.CurrentPlayer player;

        delegate void NetworkState(out NetworkState state);

        public event Action<NetPeer> OnConnected;
        public event Action OnDisconected;

        public NetworkBehavior(NetManager manager, Configuration default_Conf, List<Configuration> configurations,Realisation.CurrentPlayer player)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.default_Conf = default_Conf ?? throw new ArgumentNullException(nameof(default_Conf));
            this.configurations = configurations ?? throw new ArgumentNullException(nameof(configurations));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            OnConnected += (x) => Network.CurrentConnection = x;
            OnDisconected += () => Network.CurrentConnection = null;
            manager.Start();
            state = findServer;
            Observable.Timer(TimeSpan.FromSeconds(1))
                        .Repeat().TakeWhile(x=>state != null)
                        .Subscribe((x)=> { state.Invoke(out state); });
        }

        #region State
        private void begin_connection(out NetworkState state)
        {
            if (hasConnection)
            {
                OnConnected?.Invoke(ServerConnection);
                state = default_handler;
                return;
            }
            if (WeTryConnectedToServer())
            {
                NetworkObject.Log("We try Connected to Server");
                state = begin_connection;
                return;
            }
            
            if (_serverConnection != null)
            {
                OnDisconected.Invoke();
                state = reconnection;
                return;
            }
            else
            {
                state = findServer;
                return;
            }
            
        }

        private void findServer(out NetworkState state)
        {
            if (toConnect == null)
            {
                toConnect = new TryToConnect();
                toConnect.NotTry = configurations.ToList();
                ConnectedTo(default_Conf);
                state = begin_connection;
                return;
            }
            else
            {
                var newTry = toConnect.NotTry.FirstOrDefault();
                if (newTry == null)
                {
                    NetworkObject.LogAssertion("Мы не нашли ни одного сервера для соединения");
                    state = null;
                    manager.Stop();
                    return;
                }
                toConnect.NotTry.Remove(newTry);
                ConnectedTo(newTry);
                state = begin_connection;
                return;
            }
        }


        private void reconnection(out NetworkState state)
        {
            if (tryCount == null)
                tryCount = 3;
            if (tryCount == 0)
            {
                throw new Exception("Потрачены все попытки востановить соединение");
            }
            else
            {
                tryCount -= 1;
                NetworkObject.LogAssertion($"Попытка востановить соединение № {tryCount}");
                ConnectedTo(CurrentConf);
                state = begin_connection;
                return;
            }
        }

        private void default_handler(out NetworkState state)
        {
            if (hasConnection)
            {
                state = default_handler;
                if (!player.isInit) player.Init();
            }
            else
                state = begin_connection;
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
        private NetPeer CurentServerConnection()
        {
            List<NetPeer> connectedPeers = new List<NetPeer>();
            manager.GetPeersNonAlloc(connectedPeers,ConnectionState.Connected);
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

        /// <summary>
        /// Ожидаем ли мы соединение с одним из серверов?
        /// </summary>
        /// <returns></returns>
        private bool WeTryConnectedToServer()
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
        #endregion
    }

}
