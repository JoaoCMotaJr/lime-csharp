﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Timers;
using Lime.Protocol;
using Lime.Protocol.Network;
using Lime.Protocol.Security;
using Lime.Transport.Http.Storage;
using Timer = System.Timers.Timer;

namespace Lime.Transport.Http
{
    public sealed class HttpTransportProvider : IHttpTransportProvider, IDisposable
    {
        private readonly bool _useHttps;
        private readonly ConcurrentDictionary<string, ITransportSession> _transportDictionary;
        private readonly IEnvelopeStorage<Message> _messageStorage;
        private readonly IEnvelopeStorage<Notification> _notificationStorage;
        private readonly Timer _expirationTimer;
        private readonly TimeSpan _expirationInactivityInterval;
        private readonly TimeSpan _closeTransportTimeout;

        public HttpTransportProvider(bool useHttps, IEnvelopeStorage<Message> messageStorage, IEnvelopeStorage<Notification> notificationStorage, 
            TimeSpan expirationInactivityInterval, TimeSpan expirationTimerInterval = default(TimeSpan), TimeSpan closeTransportTimeout = default(TimeSpan))
        {
            _useHttps = useHttps;
            _messageStorage = messageStorage;
            _notificationStorage = notificationStorage;
            _transportDictionary = new ConcurrentDictionary<string, ITransportSession>();
            _expirationInactivityInterval = expirationInactivityInterval;
            _closeTransportTimeout = closeTransportTimeout != default(TimeSpan) ? closeTransportTimeout : TimeSpan.FromSeconds(60);

            if (expirationTimerInterval.Equals(default(TimeSpan)))
            {
                expirationTimerInterval = TimeSpan.FromSeconds(5);
            }           
            _expirationTimer = new Timer(expirationTimerInterval.TotalMilliseconds);
            _expirationTimer.Elapsed += ExpirationTimer_Elapsed;
            _expirationTimer.Start();
        }

        public ITransportSession GetTransport(IPrincipal requestPrincipal, bool cacheInstance)
        {
            var identity = (HttpListenerBasicIdentity)requestPrincipal.Identity;
            var transportKey = GetTransportKey(identity);

            ITransportSession transport;

            if (cacheInstance)
            {
                transport = _transportDictionary.GetOrAdd(
                    transportKey,
                    k =>
                    {
                        var newTransport = CreateTransport(identity);
                        newTransport.Closing += (sender, e) =>
                        {
                            ITransportSession t;
                            _transportDictionary.TryRemove(k, out t);
                        };
                        return newTransport;
                    });
            }
            else if (!_transportDictionary.TryRemove(transportKey, out transport))
            {
                transport = CreateTransport(identity);
            }
            
            return transport;
        }

        public event EventHandler<TransportEventArgs> TransportCreated;

        /// <summary>
        /// Creates a new instance
        /// of tranport for the
        /// specified identity
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        private ServerHttpTransport CreateTransport(HttpListenerBasicIdentity httpIdentity)
        {
            var identity = Identity.Parse(httpIdentity.Name);
            var plainAuthentication = new PlainAuthentication();
            plainAuthentication.SetToBase64Password(httpIdentity.Password);

            var transport = new ServerHttpTransport(identity, plainAuthentication, _useHttps, _messageStorage, _notificationStorage, _expirationInactivityInterval);
            TransportCreated.RaiseEvent(this, new TransportEventArgs(transport));
            return transport;
        }

        /// <summary>
        /// Gets a hashed key based on
        /// the identity and password.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        private static string GetTransportKey(HttpListenerBasicIdentity identity)
        {
            return string.Format("{0}:{1}", identity.Name, identity.Password).ToSHA1HashString();
        }

        private async void ExpirationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {            
            _expirationTimer.Stop();

            try
            {
                var expiredTransportSessions = _transportDictionary
                    .Values
                    .Where(s => s.Expiration <= DateTimeOffset.UtcNow)
                    .ToArray();

                foreach (var expiredSession in expiredTransportSessions)
                {
                    using (var tcs = new CancellationTokenSource(_closeTransportTimeout))
                    {
                        var cancellationToken = tcs.Token;
                        bool finished;

                        try
                        {
                            await expiredSession.FinishAsync(cancellationToken);
                            finished = true;
                        }
                        catch (OperationCanceledException)
                        {
                            finished = false;
                        }
                        catch (InvalidOperationException)
                        {
                            finished = false;
                        }

                        if (!finished)
                        {
                            // Force closing the transport
                            await ((ITransport) expiredSession).CloseAsync(cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                _expirationTimer.Start();
            }
        }

        public void Dispose()
        {
            _expirationTimer.Dispose();
        }
    }
}
