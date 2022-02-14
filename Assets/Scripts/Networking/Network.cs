using LiteNetLib;
using LiteNetLib.Utils;
using System;
using UniRx;
using static Assets.Scripts.Modules.Networking.NetworkObject;

namespace Assets.Scripts.Modules.Networking
{
    public sealed class Network
    {
        private readonly NetPacketProcessor processor;
        private readonly Subject<object> observable = new Subject<object>();

        internal static NetPeer CurrentConnection;

        public Network(NetPacketProcessor processor, EventBasedNetListener listener, Subject<object> observable)
        {
            this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
            observable.Subscribe(this.observable);
            observable.Subscribe(x=>Log($"New message => {x}"));
            listener.NetworkReceiveEvent += 
                (NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) => 
                {
                    if (peer == CurrentConnection)
                    {
                        try
                        {
                            processor.ReadPacket(reader);
                        }
                        catch (Exception ex)
                        {
                            LogError($"{ex.Message} => {ex.StackTrace}" );
                        }
                    }
                };
        }

        internal void Send<T>(T message, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
            where T : class, NetworkAPI.IRequest, new()
        {
            Log($"Send message => {message}");
            processor.Send<T>(CurrentConnection, message, method);
        }

        internal IObservable<OUT> Request<IN, OUT>(IN message, Predicate<OUT> predicate)
            where IN: class, NetworkAPI.IRequest, new()
            where OUT : class, NetworkAPI.IRespone, new()
        {
            Send<IN>(message, DeliveryMethod.ReliableOrdered);
            return SubscribeRespone<OUT>().Where(x => predicate(x)).Take(1);
        }

        internal IObservable<T> SubscribeRespones<T>(Predicate<T> predicate)
            where T : class, NetworkAPI.IRespone, new() 
            => SubscribeRespone<T>().Where(x => predicate(x));
        
        internal IObservable<T> SubscribeRespone<T>() 
            => observable.Where(x => x is T).Cast<Object, T>();

    }
}
