using System;
using Zenject;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class Unit : NetworkObject
    {
        #region Factory
        public class Factory : IFactory<NetworkAPI.Unit, Unit>
        {
            Network network;
            Container.Factory factory;

            public Factory(Network network, Container.Factory factory)
            {
                this.network = network ?? throw new ArgumentNullException(nameof(network));
                this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            public Unit Create(NetworkAPI.Unit param)
                => new Unit(network, factory, param);

            public class Async : IFactory<NetworkAPI.Unit, Action<Unit>, Unit>
            {
                Network network;
                Container.Factory factory;

                public Async(Network network, Container.Factory factory)
                {
                    this.network = network ?? throw new ArgumentNullException(nameof(network));
                    this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
                }
                public Unit Create(NetworkAPI.Unit param1, Action<Unit> param2)
                    => new Unit(network, factory, param1,param2);
            }
            public class New : IFactory<NetworkAPI.Unit.AddNew, Unit>
            {
                Network network;
                Container.Factory factory;

                public New(Network network, Container.Factory factory)
                {
                    this.network = network ?? throw new ArgumentNullException(nameof(network));
                    this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
                }
                public Unit Create(NetworkAPI.Unit.AddNew param)
                    => new Unit(network, factory, param);

                public class Async : IFactory<NetworkAPI.Unit.AddNew, Action<Unit>, Unit>
                {
                    Network network;
                    Container.Factory factory;

                    public Async(Network network, Container.Factory factory)
                    {
                        this.network = network ?? throw new ArgumentNullException(nameof(network));
                        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
                    }
                    public Unit Create(NetworkAPI.Unit.AddNew param1, Action<Unit> param2)
                        => new Unit(network, factory, param1, param2);
                }
            }

        }
        #endregion

        #region Properties
        #region public
        private Container conteiner;
        public Container Container => conteiner ?? (init != null ? conteiner = factory.Create(init.items) : null);

        public int HP => param.hp;
        public int Mana => param.mana;

        public ushort Strength => property.Strength;
        public ushort Agility => property.Agility;
        public ushort Intellect => property.Intellect;
        public ushort Wisdom => property.Wisdom;
        public ushort Endurance => property.Endurance;
        public ushort Iniciative => property.Iniciative;

        public int id_Model => wiew.id_Model & 7;
        public int id_Material => wiew.id_Model & 63;
        public int id_view => wiew.id_Model & (int.MaxValue - 63);
        public string id => unit.ID_ToString;
        #endregion

        #region internal
        internal NetworkAPI.UnitParam param => init.param;
        internal NetworkAPI.UnitProperty property => init.property;
        internal NetworkAPI.Container items => init.items;
        internal NetworkAPI.UnitWiew wiew => init.wiew;

        internal readonly NetworkAPI.Unit unit;
        #endregion

        #region private
        private NetworkAPI.Unit.Init init;
        private Container.Factory factory;
        #endregion
        #endregion

        private Unit(Network network, Container.Factory factory, NetworkAPI.Unit unit, Action<Unit> callback = null) : base(network)
        {
            if (unit.id == null) throw new Exception($"Ссылка на {nameof(unit)} пустая!");
            this.unit = unit ?? throw new ArgumentNullException(nameof(unit));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Init(callback);
        }

        private Unit(Network network, Container.Factory factory, NetworkAPI.Unit.AddNew unit, Action<Unit> callback = null) : base(network)
        {
            this.unit = new NetworkAPI.Unit() { };
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            InitNew(unit, callback);
        }

        #region Init
        private void Init(Action<Unit> callback = null) =>
            Init<NetworkAPI.Unit.Load, NetworkAPI.Unit.Init>(
                new NetworkAPI.Unit.Load() { id = unit.id },
                respone => respone.ID_ToString == unit.ID_ToString,
                respone => 
                {
                    init = respone;
                    if (init.items == null || String.IsNullOrEmpty(init.items.ID_ToString))
                        LogError($"Юнит ({init.ID_ToString}) имеет инициализатор с пустым контейнером ");

                },
                callback != null ? (Action)(()=>callback(this)) : null);


        private void InitNew(NetworkAPI.Unit.AddNew token, Action<Unit> callback = null) =>
            Init<NetworkAPI.Unit.AddNew, NetworkAPI.Unit.Init>(
                token,
                respone => true,
                respone => 
                {
                    init = respone;
                    unit.id = respone.id;
                    if (init.items == null || String.IsNullOrEmpty(init.items.ID_ToString))
                        LogError($"Юнит ({init.ID_ToString}) имеет инициализатор с пустым контейнером ");
                },
                callback != null ? (Action)(() => callback(this)) : null);
        #endregion

        #region Func
        public void ChangeParam(int HP, int Mana) 
            => ChangeParam(new NetworkAPI.UnitParam() { hp = HP, mana = Mana });

        private void ChangeParam(NetworkAPI.UnitParam param) 
            => network.Send(
                new NetworkAPI.Unit.ChangeParam()
                {
                    param = init.param = param,
                    id = unit.id
                });
        #endregion
    }
}
