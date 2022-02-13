using NaughtyAttributes;
using Zenject;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Linq;
using UniRx;

namespace Assets.Scripts.Modules.Networking
{
    public class NetworkInstaller : MonoInstaller<NetworkInstaller>
    {
        public Configuration configuration_default;
        public Configuration[] Servers;

        private EventBasedNetListener listener = new EventBasedNetListener();
        private NetPacketProcessor processor = new NetPacketProcessor();
        private Subject<object> subject = new Subject<object>();
        private NetManager _manager;
        internal NetManager Manager
        {
            get => 
                _manager != null ? 
                _manager : 
                _manager = new NetManager(listener)
                {
                    UnsyncedDeliveryEvent = true,
                    UnsyncedEvents = true,
                    UnsyncedReceiveEvent = true,
                    AutoRecycle = true
                };
        }
        

        private bool hasManager => Manager != null;
        private bool hasFirstPeer => hasManager ? Manager.FirstPeer != null : false;
        [ShowNativeProperty]
        public ConnectionState connectionState => hasFirstPeer ? Manager.FirstPeer.ConnectionState : ConnectionState.Disconnected;


        [ShowNativeProperty]
        public NetworkObject.LogLevl logs { get => NetworkObject.logLevl; set => NetworkObject.logLevl = value; }
        [Button]
        public void ChangeLogLvL() => logs = logs != NetworkObject.LogLevl.Full ? logs++ : NetworkObject.LogLevl.None;


        public override void InstallBindings()
        {
            Container.BindInstance(listener);
            Container.BindInstance(Manager);
            Container.BindInstance(processor);
            Container.BindInstance(configuration_default);
            Container.BindInstance(Servers.ToList());
            Container.Bind<Network>().AsSingle().NonLazy();
            Container.BindInstance(subject).WhenInjectedInto<Network>();
            Container.Bind<NetworkBehavior>().AsSingle().NonLazy();
            Binder.BindFactory(Container);
            Binder.BindObjects(processor, subject);
        }
        
        
    }
}
