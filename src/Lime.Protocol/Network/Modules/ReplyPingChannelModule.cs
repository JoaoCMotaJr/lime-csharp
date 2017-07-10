using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lime.Protocol.Network.Modules
{
    /// <summary>
    /// Defines a channel module that automatically replies to ping request commands.
    /// </summary>
    /// <seealso cref="ChannelModuleBase{T}.Protocol.Command}" />
    public sealed class ReplyPingChannelModule : ChannelModuleBase<Command>
    {
        public const string PING_MEDIA_TYPE = "application/vnd.lime.ping+json";
        public const string PING_URI = "/ping";
        private static readonly Document PingDocument = new JsonDocument(MediaType.Parse(PING_MEDIA_TYPE));

        private readonly IChannel _channel;

        public ReplyPingChannelModule(IChannel channel)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            _channel = channel;
        }

        public override async Task<Command> OnReceivingAsync(Command envelope, CancellationToken cancellationToken)
        {
            if (!envelope.IsPingRequest()) return envelope;
            if (envelope.To != null && !envelope.To.ToIdentity().Equals(_channel.LocalNode.ToIdentity())) return envelope;

            var pingCommandResponse = new Command
            {
                Id = envelope.Id,
                To = envelope.GetSender(),
                Status = CommandStatus.Success,
                Method = CommandMethod.Get,
                Resource = PingDocument
            };

            await _channel.SendCommandAsync(pingCommandResponse, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }
}