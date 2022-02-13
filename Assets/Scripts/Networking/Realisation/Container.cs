using System;
using System.Collections.Generic;
using UniRx;
using Zenject;
using System.Linq;

namespace Assets.Scripts.Modules.Networking.Realisation
{

    /// <summary>
    /// Предоставляет данные о контейнере с сервера 
    /// </summary>
    public sealed class Container : NetworkObject
    {
        #region Factory
        /// <summary>
        /// Создаём представление синхронно с обновлениями
        /// </summary>
        public class Factory : PlaceholderFactory<NetworkAPI.Container, Container>
        {
            public override Container Create(NetworkAPI.Container param)
            {
                var result = base.Create(param);
                result.Init();
                result.Subscribe();//Внимание! Подписка без инициализатора ломает сервер отправляя запрос с пустым ID 
                return result;
            }
            /// <summary>
            /// Создаём представление синхронно без обновлений (Данные актуальны только на момент создания)
            /// </summary>
            public class NoUpDate : PlaceholderFactory<NetworkAPI.Container, Container>
            {
                public override Container Create(NetworkAPI.Container param)
                {
                    var result = base.Create(param);
                    result.Init();
                    return result;
                }
            }
            /// <summary>
            /// Создаём представление асинхронно с обновлениями
            /// </summary>
            public class Async : PlaceholderFactory<NetworkAPI.Container,Action<Container>, Container>
            {
                public override Container Create(NetworkAPI.Container param1, Action<Container> param2)
                {
                    var result = base.Create(param1, param2);
                    //result.Subscribe();
                    result.Init((x)=> { x.Subscribe(); param2.Invoke(x); });
                    return result;
                }
                /// <summary>
                /// Создаём представление асинхронно с безобновлений (Данные актуальны только на момент создания)
                /// </summary>
                public class NoUpDate : PlaceholderFactory<NetworkAPI.Container, Action<Container>, Container>
                {
                    public override Container Create(NetworkAPI.Container param1, Action<Container> param2)
                    {
                        var result = base.Create(param1, param2);
                        result.Init(param2);
                        return result;
                    }
                }
            }
        }
        #endregion
        
        internal readonly NetworkAPI.Container container;
        private IDisposable Subscription_dispose;
        private ReactiveCollection<Item> _items = new ReactiveCollection<Item>();
        private Item.Factory.Async factory;

        public IObservable<Item> Added => _items.ObserveAdd().ObserveOnMainThread().Select(x=>x.Value);
        public IObservable<Item> Removed => _items.ObserveRemove().ObserveOnMainThread().Select(x=>x.Value);
        public IEnumerable<Item> items => _items.AsEnumerable();
        
        public Container(Network network, Item.Factory.Async factory,NetworkAPI.Container container, [InjectOptional]Action<Container> callBack):base(network)
        {
            if (container.id == null || String.IsNullOrEmpty(container.ID_ToString)) throw new Exception( $"Ссылка на {nameof(container)} пустая!");
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }



        #region Initialisation
        private void Init()
        {
            RequstInit()
                .Subscribe(
                (x) =>
                {
                    ResponeInitHandler(x);
                    Await();
                });
            Wait();
        }

        private void Init(Action<Container> callBack)
            => RequstInit()
                .ObserveOnMainThread()
                .Subscribe(
                (x) =>
                {
                    ResponeInitHandler(x);
                    callBack.Invoke(this);
                });

        private IObservable<NetworkAPI.Container.Init> RequstInit() =>
            network.Request<NetworkAPI.Container.Load, NetworkAPI.Container.Init>(
                            new NetworkAPI.Container.Load() { id = container.id },
                            (x) => x.ID_ToString == container.ID_ToString);

        private void ResponeInitHandler(NetworkAPI.Container.Init token)
        {
            Log($"Container ({container.ID_ToString}) Инициализирован:");
            if (token.items != null && token.items.Length > 0)
                foreach (var i in token.GetItems())
                {
                    factory.Create(i,value=> _items.Add(value));
                    Log($"      предмет ({i.ID_ToString})");
                }
        }
        
        private void Subscribe()
        {
            Subscription_dispose =
            network.SubscribeRespones<NetworkAPI.Container.UpDate>
                (x => x.ID_ToString == container.ID_ToString)
                .Subscribe(upDate=>ResponeUpDateHandler(upDate));
            network.Send(new NetworkAPI.Container.Subscribe() { id = container.id });
        }

        private void ResponeUpDateHandler(NetworkAPI.Container.UpDate upDate)
        {
            Log($"Обновление содержимого Container({upDate.ID_ToString}):");
            if (upDate.Remove != null && upDate.Remove.Length > 0)
                foreach (NetworkAPI.Item i in upDate.GetRemovedItems())
                {
                    if(_items.Any(x=> x.id == i.ID_ToString))
                    {
                        _items.Remove(_items.Where(x=> x.id == i.ID_ToString).First());
                        Log($"      удаление Item({i.ID_ToString})");
                    }
                }
            if (upDate.AddItem != null && upDate.AddItem.Length > 0)
                foreach (NetworkAPI.Item i in upDate.GetAddedItems())
                {
                    factory.Create(i, value => _items.Add(value));
                    Log($"      добавление Item({i.ID_ToString})");
                }
        }
        #endregion


        #region Public interfase
        public void Unsubscribe()
        {
            Subscription_dispose?.Dispose();
            network.Send(new NetworkAPI.Container.Unsubscribe() { id =container.id});
            _items.Dispose();
            _items = new ReactiveCollection<Item>();
        }
        
        public void Transaction(Container to,params Item[] items) => Transaction(to.container, items.Select(x=>x.item_token).ToArray());
        internal void Transaction(NetworkAPI.Container to, NetworkAPI.Item[] items) 
            => network.Send(
                new NetworkAPI.Container.Transaction()
                {
                    id = container.id,
                    to = to,
                    items = items.Select(x => x.ID_ToString).ToArray()
                });

        public void AddItems(Item[] items) => AddItems(items.Select(x => x.item_token).ToArray());
        public void AddItem(params Item[] items) => AddItems(items.Select(x=>x.item_token).ToArray());
        internal void AddItems(NetworkAPI.Item[] items) => 
            network.Send(
                new NetworkAPI.Container.UpDate()
                {
                    id = container.id,
                    AddItem = items.Select(x => x.ID_ToString).ToArray()
                });

        public void AddNewItems(Dictionary<string,string> data)
        {
            if (data.ContainsKey("Count") && _items.Any(x => data["GUID"] == x.GUID))
            {
                var item = _items.Where(x => data["GUID"] == x["GUID"]).First();
                item["Count"] = (int.Parse(item["Count"]) + int.Parse(data["Count"])).ToString();
                item.Save();
            }
            else
            {
                network.Send(new NetworkAPI.Item.CreateNew()
                {
                    container = container,
                    dataKey = data.Keys.ToArray(),
                    dataValue = data.Values.ToArray()
                });
            }
        }

        public bool HasCount(string GUID,int count)
        {
            if (_items.Any(x => GUID == x["GUID"] && (x as IDictionary<string,string>).ContainsKey("Count")))
            {
                var item = _items.Where(x => GUID == x["GUID"]).First();
                return int.Parse(item["Count"]) >= count;
            }
            return false;
        }

        public void RemoveCountItem(string GUID, int count)
        {
            if (_items.Any(x => GUID == x["GUID"] && (x as IDictionary<string, string>).ContainsKey("Count")))
            {
                var item = _items.Where(x => GUID == x["GUID"] && (x as IDictionary<string, string>).ContainsKey("Count")).First();
                var result = int.Parse(item["Count"]) - count;
                if (result < 0)
                {
                    RemoveItem(item);
                    return;
                }
                item["Count"] = result.ToString();
                item.Save();
            }

        }

        public void RemoveItems(Item[] items) => RemoveItems(items.Select(x => x.item_token).ToArray());
        public void RemoveItem(params Item[] items) => RemoveItems(items.Select(x=>x.item_token).ToArray());
        public void RemoveItem(params NetworkAPI.Item[] items) => RemoveItems(items);
        public void RemoveItems(NetworkAPI.Item[] items) 
            => network.Send(new NetworkAPI.Container.UpDate()
            {
                id = container.id,
                Remove = items.Select(x => x.ID_ToString).ToArray()
            });
        #endregion

    }
}
