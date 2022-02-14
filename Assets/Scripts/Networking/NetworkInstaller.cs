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

        #region UnityInspector
#if UNITY_EDITOR
        [ShowNativeProperty]
        public ConnectionState connectionState 
            => Manager.FirstPeer != null ? 
               Manager.FirstPeer.ConnectionState : 
               ConnectionState.Disconnected;
        [ShowNativeProperty]
        public NetworkObject.LogLevl logs
        {
            get => NetworkObject.logLevl;
            set => NetworkObject.logLevl = value;
        }
        [Button]
        public void ChangeLogLvL() 
            => logs = logs != NetworkObject.LogLevl.Full ?
                      logs++ :
                      NetworkObject.LogLevl.None;
#endif
        #endregion

        public override void InstallBindings()
        {
            Container.BindInstance(listener, Manager, processor, configuration_default, Servers.ToList());

            Container.BindInstance(subject).WhenInjectedInto<Network>();
            Container.Bind<Network>().AsSingle().NonLazy();
            Container.Bind<NetworkBehavior>().AsSingle().NonLazy();

            Binder.BindFactory(Container);
            Binder.BindObjects(processor, subject);
        }
    }
}
