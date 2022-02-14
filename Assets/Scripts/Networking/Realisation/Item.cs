using System;
using UniRx;
using Zenject;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class Item : NetworkObject, IDictionary<string, string>
    {
        #region Factory
        public class Factory : PlaceholderFactory<NetworkAPI.Item, Item>
        {
            public override Item Create(NetworkAPI.Item param)
            {
                var result = base.Create(param);
                result.Init();
                return result;
            }

            public class Async : PlaceholderFactory<NetworkAPI.Item,Action<Item>, Item>
            {
                public override Item Create(NetworkAPI.Item param1, Action<Item> param2)
                {
                    var result = base.Create(param1, param2);
                    result.Init(param2);
                    return result;
                }
            }
        }
        #endregion

        #region Properties
        internal readonly NetworkAPI.Item item_token;
        private readonly ReactiveDictionary<string, string> _data = new ReactiveDictionary<string, string>();
        #region public
        public string this[string key]
        {
            get => _data[key];
            set
            {
                if (_data.ContainsKey(key)) _data.Remove(key);
                _data[key] = value;
                Save();
            }
        }
        public string GUID => _data.ContainsKey("GUID") ? _data["GUID"] : null;
        public int Count => _data.ContainsKey("Count") ? int.Parse(_data["Count"]) : -1;
        public IEnumerable<KeyValuePair<string, string>> data => _data;
        public string id => item_token.ID_ToString;
        public byte[] IDAsByte => item_token.id;
        #endregion
        #endregion

        public Item(NetworkAPI.Item item_token, Network network, [InjectOptional]Action<Item> callback) : base(network) 
            => this.item_token = item_token ?? throw new ArgumentNullException(nameof(item_token));

        #region Inicialise
        private void Init(Action<Item> callback)
            => network.Request<NetworkAPI.Item.Load, NetworkAPI.Item.Init>(
                new NetworkAPI.Item.Load() { id = item_token.id },
                x => x.ID_ToString == item_token.ID_ToString)
                .ObserveOnMainThread()
                .Subscribe(x => { HandlerResponeInit(x); callback.Invoke(this); } );

        private void Init()
        {
            network.Request<NetworkAPI.Item.Load, NetworkAPI.Item.Init>(
                new NetworkAPI.Item.Load() { id = item_token.id },
                x => x.ID_ToString == item_token.ID_ToString)
                .Subscribe(x=> { HandlerResponeInit(x); Await(); });
            Wait();
        }

        private void HandlerResponeInit(NetworkAPI.Item.Init x)
        {
            foreach (var i in Enumerable.Range(0, x.dataKey.Length))
            {
                _data.Add(x.dataKey[i], x.dataValue[i]);
            }
            SubscribeDictionary();
        }

        private void SubscribeDictionary()
        {
            upDate = new NetworkAPI.Item.UpDate()
            {
                id = item_token.id,
                removeKey = new string[] { },
                newdataKey = new string[] { },
                newdataValue = new string[] { }
            };
            _data.ObserveAdd().Subscribe(
                (x)=> 
                {
                    if (upDate.newdataKey.Contains(x.Key))
                    {
                        foreach (var i in Enumerable.Range(0, upDate.newdataKey.Length))
                        {
                            if (upDate.newdataKey[i] == x.Key)
                                upDate.newdataValue[i] = x.Value;
                        }
                    }
                    else
                    {
                        upDate.newdataKey = upDate.newdataKey.Append(x.Key).ToArray();
                        upDate.newdataValue = upDate.newdataValue.Append(x.Value).ToArray();
                    }
                });
            _data.ObserveRemove().Subscribe(
                (x)=> 
                {
                    var list = upDate.removeKey.ToList();
                    if (!list.Contains(x.Key))
                    {
                        list.Add(x.Key);
                        upDate.removeKey = list.ToArray();
                    }
                });
        }

        #endregion

        #region public Interface
        private NetworkAPI.Item.UpDate upDate;

        public void Save()
        {
            if (upDate.newdataKey.Length == 0 & upDate.removeKey.Length == 0) return;
            network.Send(upDate);
            upDate = new NetworkAPI.Item.UpDate()
            {
                id = item_token.id,
                removeKey = new string[] { },
                newdataKey = new string[] { },
                newdataValue = new string[] { }
            };
        }


        #endregion

        #region Dictionary<string,string>

        ICollection<string> IDictionary<string, string>.Keys => _data.Keys;

        ICollection<string> IDictionary<string, string>.Values => _data.Values;

        int ICollection<KeyValuePair<string, string>>.Count => _data.Count;

        bool ICollection<KeyValuePair<string, string>>.IsReadOnly => false;

        string IDictionary<string, string>.this[string key] { get => _data[key]; set => _data[key] = value; }
        void IDictionary<string, string>.Add(string key, string value) => _data.Add(key, value);

        bool IDictionary<string, string>.ContainsKey(string key) => _data.ContainsKey(key);

        bool IDictionary<string, string>.Remove(string key) => _data.Remove(key);

        bool IDictionary<string, string>.TryGetValue(string key, out string value) => _data.TryGetValue(key, out value);

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) => (_data as ICollection<KeyValuePair<string, string>>).Add(item);

        void ICollection<KeyValuePair<string, string>>.Clear() => (_data as ICollection<KeyValuePair<string, string>>).Clear();

        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item) => (_data as ICollection<KeyValuePair<string, string>>).Contains(item);

        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => (_data as ICollection<KeyValuePair<string, string>>).CopyTo(array, arrayIndex);

        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item) => (_data as ICollection<KeyValuePair<string, string>>).Remove(item);

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => _data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();

        #endregion
    }
}
