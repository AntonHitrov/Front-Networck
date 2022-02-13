using System;
using System.Collections.Generic;
using System.Linq;
using Zenject;
using UniRx;
using System.Collections;
using NetworkAPI;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class Group :NetworkObject
    {
        #region Factory
        public class Factory : PlaceholderFactory<NetworkAPI.Group, Group>
        {
            public override Group Create(NetworkAPI.Group param)
            {
                var result = base.Create(param);
                result.Init();
                return result;
            }
            public class Async : PlaceholderFactory<NetworkAPI.Group,Action<Group>, Group>
            {
                public override Group Create(NetworkAPI.Group param1, Action<Group> param2)
                {
                    var result = base.Create(param1, param2);
                    result.Init(param2);
                    return result;
                }
            }
        }
        #endregion

        private readonly NetworkAPI.Group group;
        private readonly Unit.Factory unitFactory;
        private readonly Unit.Factory.Async unitFactoryAsync;

        private readonly ReactiveCollection<Unit> units = new ReactiveCollection<Unit>();

        #region public Properties
        public IObservable<Unit> add => units.ObserveAdd().Select(x=>x.Value).ObserveOnMainThread();
        public IObservable<Unit> removed => units.ObserveRemove().Select(x => x.Value).ObserveOnMainThread();
        public IEnumerable<Unit> Units { get { if (_units == null) Wait(); return _units; } private set => _units = value; }
        private IEnumerable<Unit> _units;
        public Unit First => Units.First();
        public string id => group.ID_ToString;
        #endregion

        public Group(Network network, NetworkAPI.Group group, Unit.Factory.Async unitFactoryAsync, Unit.Factory unitFactory,[InjectOptional]Action<Group> callback) : base(network)
        {
            this.group = group ?? throw new ArgumentNullException(nameof(group));
            this.unitFactory = unitFactory ?? throw new ArgumentNullException(nameof(unitFactory));
            this.unitFactoryAsync = unitFactoryAsync ?? throw new ArgumentNullException(nameof(unitFactoryAsync));
        }

        #region Initializer
        private void Init(Action<Group> callback = null) 
            => Init<NetworkAPI.Group.Load, NetworkAPI.Group.Init>(
                new NetworkAPI.Group.Load() { id = group.id },
                message => message.ID_ToString == group.ID_ToString,
                init    => Units = new LoaderUnits(units,init, unitFactory),
                callback != null ? (Action)(()=>callback(this)) : null);
        #endregion

        #region Public Interface
        public void Add(Unit unit)
        {
            if (!units.Any(token => token.id == unit.id))
                AddUnit(unit);
        }
        private void AddUnit(Unit unit) 
            => network.Request<NetworkAPI.Group.AddUnit, NetworkAPI.Group.AddUnit>(
                new NetworkAPI.Group.AddUnit()
                {
                    id = group.id,
                    unit = new NetworkAPI.Unit() { ID_ToString = unit.id }
                }, x => x.ID_ToString == group.ID_ToString)
                .Subscribe(
                (x) =>
                {
                    if (!units.Any(token => token.id == x.unit.ID_ToString))
                    {
                        unitFactoryAsync.Create(x.unit, u => units.Add(u));
                    }
                });

        public void Remove(Unit unit)
        {
            if (units.Any(token => token.id == unit.id))
                RemoveUnit(unit);
        }
        private void RemoveUnit(Unit unit) 
            => network.Request<NetworkAPI.Group.RemoveUnit, NetworkAPI.Group.RemoveUnit>(
                new NetworkAPI.Group.RemoveUnit()
                {
                    id = group.id,
                    unit = new NetworkAPI.Unit() { ID_ToString = unit.id }
                }, x => x.ID_ToString == group.ID_ToString)
                .Subscribe(
                (x) =>
                {
                    if (units.Any(token => token.id == x.unit.ID_ToString))
                    {
                        units.Remove(units.Where(u => u.id == x.unit.ID_ToString).First());
                    }
                });
        #endregion


        private class LoaderUnits : IEnumerable<Unit>
        {
            private ReactiveCollection<Unit> units;
            private NetworkAPI.Group.Init init;
            private Unit.Factory factory;
            private bool isLoaded;
            private IEnumerable<Unit> Load
            {
                get
                {
                    foreach (NetworkAPI.Unit value in init.Getunits())
                        units.Add(factory.Create(value));
                    isLoaded = true;
                    return units;
                }
            }

            internal LoaderUnits(ReactiveCollection<Unit> units, NetworkAPI.Group.Init init, Unit.Factory factory)
            {
                this.units = units ?? throw new ArgumentNullException(nameof(units));
                this.init = init ?? throw new ArgumentNullException(nameof(init));
                this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            public IEnumerator<Unit> GetEnumerator() => isLoaded ? units.GetEnumerator() : Load.GetEnumerator();


            IEnumerator IEnumerable.GetEnumerator() => isLoaded ? units.GetEnumerator() : Load.GetEnumerator();
        }
    }
}
