using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LiteNetLib.Utils;
using NetworkAPI;
using Zenject;

namespace Assets.Scripts.Modules.Networking
{
    static internal class Binder
    {
        #region Messages
        private static MethodInfo MethodTo(string name) => typeof(Binder).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
        private static IEnumerable<Type> MessageTypes => NetworkTypes.Where(type => type.IsSealed);
        private static IEnumerable<Type> ValueTypes => NetworkTypes.Where(type => !type.IsSealed && type.GetInterfaces().Contains(typeof(INetSerializable)));
        private static IEnumerable<Type> NetworkTypes => typeof(APIObject).Assembly.GetTypes()
                                                         .Where(type => type.Namespace == nameof(NetworkAPI))
                                                         .Where(type => type.IsClass && !type.IsAbstract);

        internal static void BindObjects(NetPacketProcessor packetProcessor, IObserver<object> observer)
        {
            if (packetProcessor == null) throw new ArgumentNullException(nameof(packetProcessor));
            if (observer == null) throw new ArgumentNullException(nameof(observer));

            object[] Param = new object[] { packetProcessor, observer };
            Bind(Param, MethodTo(nameof(BindValue)), ValueTypes).Count();
            Bind(Param, MethodTo(nameof(BindMessage)), MessageTypes).Count();
        }

        private static IEnumerable<object> Bind(object[] Param, MethodInfo BinderMessages, IEnumerable<Type> types) 
            => from type in types let method = BinderMessages.MakeGenericMethod(type) select method.Invoke(null, Param);
        internal static void BindValue<T>(NetPacketProcessor packetProcessor, IObserver<object> observer)
            where T : class, INetSerializable, new() => packetProcessor.RegisterNestedType(() => new T());
        internal static void BindMessage<T>(NetPacketProcessor packetProcessor, IObserver<object> observer)
            where T : class, new() => packetProcessor.Subscribe((x) => observer.OnNext(x), () => new T());
        #endregion

        #region Factory
        private static IEnumerable<Type> factory => from type in typeof(NetworkObject).Assembly.GetExportedTypes()
                                                    where type.GetInterfaces().Contains(typeof(IFactory))
                                                    select type;

        internal static void BindFactory(DiContainer Container)
        {
            Container.Bind<Realisation.CurrentPlayer>().AsSingle().Lazy();
            foreach (Type type in factory)
            {
                try
                {
                    
                    if (type.BaseType.Name.Contains("PlaceholderFactory"))
                        BuilderBinderFactory(type, Container);
                    else
                        Container.BindInterfacesAndSelfTo(type).AsSingle();
                    NetworkObject.Log($"Factory binded {type}");
                }
                catch(Exception exception)
                {
                    NetworkObject.LogError( $"{exception.Message} from {type}"  );

                }
            }
            
            
        }

        private static ConcreteIdArgConditionCopyNonLazyBinder BuilderBinderFactory(Type factory, DiContainer Container) 
            => BindFactory(Container, GetInputsParametrs(factory, factory.GetMethod("Create"))).AsSingle();

        private static ConcreteIdArgConditionCopyNonLazyBinder AsSingle(this object result) 
            => (ConcreteIdArgConditionCopyNonLazyBinder)result.GetType().GetMethod("AsSingle").Invoke(result, new object[] { });

        private static IEnumerable<Type> GetInputsParametrs(Type factory, MethodInfo method) 
            => method
                .GetParameters()
                .Select(x => x.ParameterType)
                .Append(method.ReturnParameter.ParameterType)
                .Append(factory);

        private static object BindFactory(DiContainer Container, IEnumerable<Type> inputs)
            => typeof(DiContainer)
                .GetMember(nameof(Container.BindFactory), MemberTypes.Method, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Select(x => (MethodInfo)x)
                .Where(x => x.GetGenericArguments().Length == inputs.Count())
                .First()
                .MakeGenericMethod(inputs.ToArray())
                .Invoke(Container, new object[] { });

        private static object BindIFactory(DiContainer Container, IEnumerable<Type> inputs) 
            => typeof(DiContainer)
                .GetMember(nameof(Container.BindIFactory), MemberTypes.Method, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Select(x => (MethodInfo)x)
                .Where(x => x.GetGenericArguments().Length == inputs.Count())
                .First()
                .MakeGenericMethod(inputs.ToArray())
                .Invoke(Container, new object[] { });
        #endregion
    }
}
