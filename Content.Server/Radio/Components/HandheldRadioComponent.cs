using System.Linq;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Robust.Shared.Player;

namespace Content.Server.Radio.Components
{
    [RegisterComponent]
    [ComponentProtoName("Radio")]
    [ComponentReference(typeof(IRadio))]
    [ComponentReference(typeof(IListen))]
#pragma warning disable 618
    public sealed class HandheldRadioComponent : Component, IListen, IRadio
#pragma warning restore 618
    {
        private ChatSystem? _chatSystem;
        private RadioSystem? _radioSystem;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private bool _radioOn;
        [DataField("channels", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<RadioChannelPrototype>))]
        private HashSet<string> _channels = new();

        public int BroadcastFrequency => IoCManager.Resolve<IPrototypeManager>()
            .Index<RadioChannelPrototype>(BroadcastChannel).Frequency;

        // TODO: Assert in componentinit that channels has this.
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("broadcastChannel", customTypeSerializer: typeof(PrototypeIdSerializer<RadioChannelPrototype>))]
        public string BroadcastChannel { get; set; } = "Common";

        [ViewVariables(VVAccess.ReadWrite)] [DataField("listenRange")] public int ListenRange { get; private set; } = 7;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool RadioOn
        {
            get => _radioOn;
            private set
            {
                _radioOn = value;
                Dirty();
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            _radioSystem = sysMan.GetEntitySystem<RadioSystem>();
            _chatSystem = sysMan.GetEntitySystem<ChatSystem>();

            RadioOn = false;
        }

        public void Speak(string message)
        {
            _chatSystem?.TrySendInGameICMessage(Owner, message, InGameICChatType.Speak, false);
        }

        public bool Use(EntityUid user)
        {
            RadioOn = !RadioOn;

            var message = Loc.GetString("handheld-radio-component-on-use",
                                        ("radioState", Loc.GetString(RadioOn ? "handheld-radio-component-on-state" : "handheld-radio-component-off-state")));
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedPopupSystem>()
                .PopupEntity(message, Owner, Filter.Entities(user));

            return true;
        }

        public bool CanListen(string message, EntityUid source, RadioChannelPrototype? prototype)
        {
            if (prototype != null && !_channels.Contains(prototype.ID)
                || !_prototypeManager.HasIndex<RadioChannelPrototype>(BroadcastChannel))
            {
                return false;
            }

            return RadioOn
                   && IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedInteractionSystem>().InRangeUnobstructed(Owner, source, range: ListenRange);
        }

        public void Receive(string message, RadioChannelPrototype channel, EntityUid speaker)
        {
            if (_channels.Contains(channel.ID) && RadioOn)
            {
                Speak(message);
            }
        }

        public void Listen(string message, EntityUid speaker, RadioChannelPrototype? prototype)
        {
            // if we can't get the channel, we need to just use the broadcast frequency
            if (prototype == null
                && !_prototypeManager.TryIndex(BroadcastChannel, out prototype))
            {
                return;
            }

            Broadcast(message, speaker, prototype);
        }

        public void Broadcast(string message, EntityUid speaker, RadioChannelPrototype channel)
        {
            _radioSystem?.SpreadMessage(this, speaker, message, channel);
        }
    }
}
