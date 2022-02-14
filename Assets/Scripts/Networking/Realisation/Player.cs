using System;
using Zenject;

namespace Assets.Scripts.Modules.Networking.Realisation
{
    public sealed class Player :NetworkObject
    {
        #region Factory
        public class Factory : PlaceholderFactory<NetworkAPI.Player, Player>
        {
            public override Player Create(NetworkAPI.Player param)
            {
                var result = base.Create(param);
                result.Init();
                return result;
            }
            
            public class Async : PlaceholderFactory<NetworkAPI.Player, Action<Player>, Player>
            {
                public override Player Create(NetworkAPI.Player param1, Action<Player> param2)
                {
                    var result = base.Create(param1, param2);
                    result.Init(param2);
                    return result;
                }
            }
        }
        #endregion

        #region Properties
        #region public Properties
        public NetworkAPI.Player.Info info { get; private set; }
        public bool loaded => info != null;
        #endregion
        private readonly NetworkAPI.Player token;
        #endregion

        public Player(Network network, NetworkAPI.Player token, [InjectOptional] Action<Player> callback) : base(network) 
            => this.token = token ?? throw new ArgumentNullException(nameof(token));

        #region Initializer
        private void Init(Action<Player> callback = null) 
            => Init<NetworkAPI.Player.GetInfo, NetworkAPI.Player.Info>
                (new NetworkAPI.Player.GetInfo() { id = token.id },
                (x) => x.ID_ToString == token.ID_ToString,
                (result) => info = result,
                callback != null ? (Action)(()=>callback(this)) : null
                );
        #endregion
    }
}
