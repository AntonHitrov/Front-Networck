using System;
using System.Threading;
using UnityEngine;
using Object = System.Object;
using UniRx;

namespace Assets.Scripts.Modules.Networking
{

    public abstract class NetworkObject
    {
        #region Sync
        private readonly Object locker = new object();
        private bool hasWait;
        protected internal void Wait(TimeSpan time)
        {
            lock (locker)
            {
                hasWait = true;
                if (!Monitor.Wait(locker, time)) LogError($"Объект {this} разблокирован по завершению ожидания");
            }
        }
        protected internal void Wait() => Wait(TimeSpan.FromSeconds(5));
        protected internal void Await()
        {
            if(hasWait)
                lock (locker)
                {
                    hasWait = false;
                    Monitor.PulseAll(locker);
                }
        }
        #endregion
        
        protected readonly Network network;
        protected NetworkObject(Network network) 
            => this.network = network ?? throw new ArgumentNullException(nameof(network));
       
        #region Inicializer
        protected internal void Init<In,Out>(In request, Predicate<Out> predicate,Action<Out> responeHandler,Action callback = null)
            where In : class, NetworkAPI.IRequest, new()
            where Out : class, NetworkAPI.IRespone, new()
        {
            network.Request<In, Out>(request, predicate)
                   .Subscribe(x =>
                   {
                       responeHandler(x);
                       Await();
                       Observable.Timer(TimeSpan.FromSeconds(0.5))
                                 .ObserveOnMainThread()
                                 .Subscribe(_ => callback?.Invoke());
                   });
            if (callback == null)
                Wait();
        }
        #endregion

        #region Logs
        public static LogLevl logLevl = LogLevl.Errors;
        public enum LogLevl :int{ None,Errors, Assertion, Full }

        protected internal static void Log(string message)
        {
            if (logLevl >= LogLevl.Full)
                Debug.Log(message);
        }

        protected internal static void LogError(string message)
        {
            if (logLevl >= LogLevl.Errors)
                Debug.LogError(message);
        }

        protected internal static void LogAssertion(string message)
        {
            if(logLevl >= LogLevl.Assertion)
                Debug.LogAssertion(message);
        }
        #endregion
    }

}
