﻿using System;
using Xunit;
using Moq;
using Lime.Protocol.Network;
using Lime.Protocol.Server;
using System.Threading.Tasks;
using System.Threading;
using Lime.Protocol.Security;
using Lime.Protocol.Util;
using Shouldly;

namespace Lime.Protocol.UnitTests.Server
{
    
    public class ServerChannelTests
    {
        #region Private fields

        private Mock<TransportBase> _transport;
        private TimeSpan _sendTimeout;

        #endregion

        #region Scenario
        
        public ServerChannelTests()
        {            
            _transport = new Mock<TransportBase>();
            _transport
                .Setup(t => t.IsConnected)
                .Returns(true);            
            _sendTimeout = TimeSpan.FromSeconds(30);
        }


        #endregion

        private ServerChannel GetTarget(SessionState state = SessionState.New, Node remoteNode = null, Guid sessionId = default(Guid), Node serverNode = null, TimeSpan? remotePingInterval = null, TimeSpan? remoteIdleTimeout = null)
        {
            if (sessionId == Guid.Empty)
            {
                sessionId = Guid.NewGuid();
            }

            if (serverNode == null)
            {
                serverNode = Dummy.CreateNode();
            }

            return new TestServerChannel(
                state,
                sessionId,
                serverNode,
                _transport.Object,
                _sendTimeout,
                remoteNode,
                remotePingInterval,
                remoteIdleTimeout);
        }

        #region ReceiveNewSessionAsync

        [Fact]
        [Trait("Category", "ReceiveNewSessionAsync")]
        public async Task ReceiveNewSessionAsync_NewState_ReadsTransport()
        {
            var target = GetTarget(SessionState.New);

            var session = Dummy.CreateSession();
            var cancellationToken = Dummy.CreateCancellationToken();

            _transport
                .Setup(t => t.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<Envelope>(session))
                .Verifiable();

            var actual = await target.ReceiveNewSessionAsync(cancellationToken);

            Assert.Equal(session, actual);
            _transport.Verify();
        }

        [Fact]
        [Trait("Category", "ReceiveNewSessionAsync")]
        public async Task ReceiveNewSessionAsync_NotNewState_ThrowsInvalidOperationException()
        {
            // Arrange
            var target = GetTarget(SessionState.Established);
            var cancellationToken = Dummy.CreateCancellationToken();

            // Act
            var actual = await target.ReceiveNewSessionAsync(cancellationToken).ShouldThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region NegotiateSessionAsync

        [Fact]
        [Trait("Category", "NegotiateSessionAsync")]
        public async Task NegotiateSessionAsync_NewStateValidOptions_CallsTransportAndReadsFromBuffer()
        {
            var session = Dummy.CreateSession(SessionState.Negotiating);


            var target = GetTarget(sessionId: session.Id);           

            var compressionOptions = new SessionCompression[] { SessionCompression.None };
            var encryptionOptions = new SessionEncryption[] { SessionEncryption.None, SessionEncryption.TLS };

            var cancellationToken = Dummy.CreateCancellationToken();

            _transport
                .Setup(t => t.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<Envelope>(session))
                .Verifiable();

            var actual = await target.NegotiateSessionAsync(compressionOptions, encryptionOptions, cancellationToken);

            _transport.Verify();

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Negotiating &&
                                        e.CompressionOptions == compressionOptions &&
                                        e.EncryptionOptions == encryptionOptions &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To == null &&
                                        e.SchemeOptions == null &&
                                        e.Compression == null &&
                                        e.Encryption == null &&
                                        e.Authentication == null &&
                                        e.Id == target.SessionId),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            Assert.Equal(SessionState.Negotiating, target.State);
            Assert.Equal(session, actual);
        }

        [Fact]
        [Trait("Category", "NegotiateSessionAsync")]
        public async Task NegotiateSessionAsync_InvalidStateValidOptions_ThrowsInvalidOperationException()
        {
            // Arrange
            var target = GetTarget(SessionState.Negotiating);
            var compressionOptions = new SessionCompression[] { SessionCompression.None };
            var encryptionOptions = new SessionEncryption[] { SessionEncryption.None, SessionEncryption.TLS };
            var cancellationToken = Dummy.CreateCancellationToken();

            // Act
            var actual =
                await
                    target.NegotiateSessionAsync(compressionOptions, encryptionOptions, cancellationToken)
                        .ShouldThrowAsync<InvalidOperationException>();
        }

        [Fact]
        [Trait("Category", "NegotiateSessionAsync")]
        public async Task NegotiateSessionAsync_NullCompressionOptions_ThrowsArgumentNullException()
        {
            var target = GetTarget();
            SessionCompression[] compressionOptions = null;
            var encryptionOptions = new SessionEncryption[] { SessionEncryption.None, SessionEncryption.TLS };
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.NegotiateSessionAsync(compressionOptions, encryptionOptions, cancellationToken)
                        .ShouldThrowAsync<ArgumentNullException>();
        }

        [Fact]
        [Trait("Category", "NegotiateSessionAsync")]
        public async Task NegotiateSessionAsync_EmptyCompressionOptions_ThrowsArgumentNullException()
        {
            var target = GetTarget();
            var compressionOptions = new SessionCompression[0];
            var encryptionOptions = new SessionEncryption[] { SessionEncryption.None, SessionEncryption.TLS };
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.NegotiateSessionAsync(compressionOptions, encryptionOptions, cancellationToken)
                        .ShouldThrowAsync<ArgumentException>();
        }

        [Fact]
        [Trait("Category", "NegotiateSessionAsync")]
        public async Task NegotiateSessionAsync_NullEncryptionOptions_ThrowsArgumentNullException()
        {
            var target = GetTarget();
            var compressionOptions = new SessionCompression[] { SessionCompression.None };
            SessionEncryption[] encryptionOptions = null;
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.NegotiateSessionAsync(compressionOptions, encryptionOptions, cancellationToken)
                        .ShouldThrowAsync<ArgumentNullException>();
        }

        [Fact]
        [Trait("Category", "NegotiateSessionAsync")]
        public async Task NegotiateSessionAsync_EmptyEncryptionOptions_ThrowsArgumentException()
        {
            var target = GetTarget();
            var compressionOptions = new SessionCompression[] { SessionCompression.None };
            var encryptionOptions = new SessionEncryption[0];
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.NegotiateSessionAsync(compressionOptions, encryptionOptions, cancellationToken)
                        .ShouldThrowAsync<ArgumentException>();
        }

        #endregion

        #region SendNegotiatingSessionAsync

        [Fact]
        [Trait("Category", "SendNegotiatingSessionAsync")]
        public async Task SendNegotiatingSessionAsync_NegotiatingState_CallsTransport()
        {
            var target = GetTarget(SessionState.Negotiating);
            var sessionCompression = SessionCompression.GZip;
            var sessionEncryption = SessionEncryption.TLS;

            await target.SendNegotiatingSessionAsync(
                sessionCompression,
                sessionEncryption);

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Negotiating &&
                                        e.SchemeOptions == null &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To == null &&
                                        e.CompressionOptions == null &&
                                        e.Compression == sessionCompression &&
                                        e.EncryptionOptions == null &&
                                        e.Encryption == sessionEncryption &&
                                        e.Authentication == null &&
                                        e.Id == target.SessionId),
                    It.IsAny<CancellationToken>()),
                    Times.Once());
        }

        #endregion

        #region AuthenticateSessionAsync

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_NegotiatingStateValidOptions_CallsTransportAndReadsFromBuffer()
        {
            var session = Dummy.CreateSession(SessionState.Authenticating);
                        
            var target = GetTarget(SessionState.Negotiating, sessionId: session.Id);

            var schemeOptions = Dummy.CreateSchemeOptions();
           
            var cancellationToken = Dummy.CreateCancellationToken();

            _transport
                .Setup(t => t.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<Envelope>(session))
                .Verifiable();

            var actual = await target.AuthenticateSessionAsync(schemeOptions, cancellationToken);

            _transport.Verify();

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Authenticating &&
                                        e.CompressionOptions == null &&
                                        e.EncryptionOptions == null &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To == null &&
                                        e.SchemeOptions == schemeOptions &&
                                        e.Compression == null &&
                                        e.Encryption == null &&
                                        e.Authentication == null &&
                                        e.Id == target.SessionId),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            Assert.Equal(SessionState.Authenticating, target.State);
            Assert.Equal(session, actual);
        }

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_InvalidStateValidOptions_ThrowsInvalidOperationException()
        {
            var target = GetTarget(SessionState.Established);
            var schemeOptions = Dummy.CreateSchemeOptions();            
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.AuthenticateSessionAsync(schemeOptions, cancellationToken)
                        .ShouldThrowAsync<InvalidOperationException>();
        }

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_NullOptions_ThrowsArgumentNullException()
        {
            var target = GetTarget(SessionState.Negotiating);
            AuthenticationScheme[] schemeOptions = null;
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.AuthenticateSessionAsync(schemeOptions, cancellationToken)
                        .ShouldThrowAsync<ArgumentNullException>();
        }

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_EmptyOptions_ThrowsArgumentException()
        {
            var target = GetTarget(SessionState.Negotiating);
            AuthenticationScheme[] schemeOptions = new AuthenticationScheme[0];
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.AuthenticateSessionAsync(schemeOptions, cancellationToken)
                        .ShouldThrowAsync<ArgumentException>();
        }

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_AuthenticatingStateValidRoundtrip_CallsTransportAndReadsFromBuffer()
        {
            var session = Dummy.CreateSession(SessionState.Authenticating);

            var target = GetTarget(SessionState.Authenticating, sessionId: session.Id);

            var authenticationRoundtrip = Dummy.CreatePlainAuthentication();
            
            var cancellationToken = Dummy.CreateCancellationToken();

            _transport
                .Setup(t => t.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<Envelope>(session))
                .Verifiable();

            var actual = await target.AuthenticateSessionAsync(authenticationRoundtrip, cancellationToken);

            _transport.Verify();

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Authenticating &&
                                        e.CompressionOptions == null &&
                                        e.EncryptionOptions == null &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To == null &&
                                        e.SchemeOptions == null &&
                                        e.Compression == null &&
                                        e.Encryption == null &&
                                        e.Authentication == authenticationRoundtrip &&
                                        e.Id == target.SessionId),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            Assert.Equal(SessionState.Authenticating, target.State);
            Assert.Equal(session, actual);
        }

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_AuthenticatingStateNullRoundtrip_ThrowsArgumentNullException()
        {
            var target = GetTarget(SessionState.Authenticating);
            Authentication authenticationRoundtrip = null;
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.AuthenticateSessionAsync(authenticationRoundtrip, cancellationToken)
                        .ShouldThrowAsync<ArgumentNullException>();
        }

        [Fact]
        [Trait("Category", "AuthenticateSessionAsync")]
        public async Task AuthenticateSessionAsync_InvalidStateValidRoundtrip_ThrowsInvalidOperationException()
        {
            var target = GetTarget(SessionState.New);
            var authenticationRoundtrip = Dummy.CreatePlainAuthentication();
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual =
                await
                    target.AuthenticateSessionAsync(authenticationRoundtrip, cancellationToken)
                        .ShouldThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region SendEstablishedSessionAsync

        [Fact]
        [Trait("Category", "SendEstablishedSessionAsync")]
        public async Task SendEstablishedSessionAsync_ValidArgumentsAuthenticatingState_CallsTransport()
        {
            var target = GetTarget(SessionState.Authenticating);

            var node = Dummy.CreateNode();

            await target.SendEstablishedSessionAsync(node);

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Established &&
                                        e.Authentication == null &&
                                        e.SchemeOptions == null &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To.Equals(node) &&
                                        e.CompressionOptions == null &&
                                        e.Compression == null &&
                                        e.EncryptionOptions == null &&
                                        e.Encryption == null &&
                                        e.Id == target.SessionId),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            Assert.Equal(target.State, SessionState.Established);
            Assert.Equal(target.RemoteNode, node);
        }

        [Fact]
        [Trait("Category", "SendEstablishedSessionAsync")]
        public async Task SendEstablishedSessionAsync_ValidArgumentsNewState_CallsTransport()
        {
            var target = GetTarget();

            var node = Dummy.CreateNode();
            await target.SendEstablishedSessionAsync(node);

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Established &&
                                        e.Authentication == null &&
                                        e.SchemeOptions == null &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To.Equals(node) &&
                                        e.CompressionOptions == null &&
                                        e.Compression == null &&
                                        e.EncryptionOptions == null &&
                                        e.Encryption == null &&
                                        e.Id == target.SessionId),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            Assert.Equal(target.State, SessionState.Established);
            Assert.Equal(target.RemoteNode, node);
        }        

        [Fact]
        [Trait("Category", "SendEstablishedSessionAsync")]
        public void SendEstablishedSessionAsync_NullNodeAuthenticatingState_ThrowsArgumentNullException()
        {
            var target = GetTarget(SessionState.Authenticating);
            Node node = null;

            Should.Throw<ArgumentNullException>(() =>
                target.SendEstablishedSessionAsync(node));
        }

        #endregion

        #region ReceiveFinishingSessionAsync

        [Fact]
        [Trait("Category", "ReceiveFinishingSessionAsync")]
        public async Task ReceiveFinishingSessionAsync_EstablishedState_ReadsTransport()
        {            
            var session = Dummy.CreateSession(SessionState.Finishing);
            
            var cancellationToken = Dummy.CreateCancellationToken();

            var tcs = new TaskCompletionSource<Envelope>();
            _transport
                .SetupSequence(t => t.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<Envelope>(session))
                .Returns(tcs.Task);


            var target = GetTarget(SessionState.Established);
            session.Id = target.SessionId;

            var actual = await target.ReceiveFinishingSessionAsync(cancellationToken);

            Assert.Equal(session, actual);
            _transport.Verify();
        }

        [Fact]
        [Trait("Category", "ReceiveFinishingSessionAsync")]
        public async Task ReceiveFinishingSessionAsync_InvalidState_ThrowsInvalidOperationException()
        {
            var target = GetTarget(SessionState.Authenticating);
            var cancellationToken = Dummy.CreateCancellationToken();

            var actual = await target.ReceiveFinishingSessionAsync(cancellationToken).ShouldThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region SendFinishedSessionAsync

        [Fact]
        [Trait("Category", "SendFinishedSessionAsync")]
        public async Task SendFinishedSessionAsync_EstablishedState_CallsAndClosesTransport()
        {
            var remoteNode = Dummy.CreateNode();

            var target = GetTarget(SessionState.Established, remoteNode);

            await target.SendFinishedSessionAsync();

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Finished &&
                                        e.Id == target.SessionId &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To.Equals(target.RemoteNode) &&
                                        e.Authentication == null &&
                                        e.SchemeOptions == null &&
                                        e.CompressionOptions == null &&
                                        e.Compression == null &&
                                        e.EncryptionOptions == null &&
                                        e.Encryption == null),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            _transport.Verify(
                t => t.CloseAsync(
                    It.IsAny<CancellationToken>()));

            Assert.Equal(target.State, SessionState.Finished);
        }

        [Fact]
        [Trait("Category", "SendFinishedSessionAsync")]
        public async Task SendFinishedSessionAsync_NewState_ClosesTransport()
        {
            var target = GetTarget();
            await target.SendFinishedSessionAsync();

            _transport.Verify(
                t => t.CloseAsync(
                    It.IsAny<CancellationToken>()));

            Assert.Equal(target.State, SessionState.Finished);

        }

        #endregion

        #region SendFailedSessionAsync

        [Fact]
        [Trait("Category", "SendFailedSessionAsync")]
        public async Task SendFailedSessionAsync_EstablishedState_CallsAndClosesTransport()
        {
            var remoteNode = Dummy.CreateNode();

            var target = GetTarget(SessionState.Established, remoteNode);

            var reason = Dummy.CreateReason();

            await target.SendFailedSessionAsync(reason);

            _transport.Verify(
                t => t.SendAsync(
                    It.Is<Session>(e => e.State == SessionState.Failed &&
                                        e.Id == target.SessionId &&
                                        e.From.Equals(target.LocalNode) &&
                                        e.To.Equals(target.RemoteNode) &&
                                        e.Authentication == null &&
                                        e.SchemeOptions == null &&
                                        e.CompressionOptions == null &&
                                        e.Compression == null &&
                                        e.EncryptionOptions == null &&
                                        e.Encryption == null),
                    It.IsAny<CancellationToken>()),
                    Times.Once());

            _transport.Verify(
                t => t.CloseAsync(
                    It.IsAny<CancellationToken>()));

            Assert.Equal(target.State, SessionState.Failed);
        }

        [Fact]
        [Trait("Category", "SendFailedSessionAsync")]
        public async Task SendFailedSessionAsync_NullReason_ThrowsArgumentNullException()
        {
            var target = GetTarget();
            Reason reason = null;

            await target.SendFailedSessionAsync(reason).ShouldThrowAsync<ArgumentNullException>();
        }

        #endregion

        #region OnRemoteIdleAsync
        [Fact]
        [Trait("Category", "OnRemoteIdleAsync")]
        public async Task OnRemoteIdleAsync_EstablishedState_CallsSendSessionFinishedAndClosesTransport()
        {
            // Arrange            
            var tcs1 = new TaskCompletionSource<Envelope>();
            _transport
                .Setup(t => t.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns(tcs1.Task);
            _transport
                .Setup(t => t.SendAsync(It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
                .Returns(TaskUtil.CompletedTask);
            var target = GetTarget(                
                SessionState.Established,                
                remotePingInterval: TimeSpan.FromMilliseconds(500),
                remoteIdleTimeout: TimeSpan.FromMilliseconds(150));

            // Act
            await Task.Delay(1000);

            // Assert
            _transport
                .Verify(t => t.SendAsync(
                    It.IsAny<Envelope>(), 
                    It.IsAny<CancellationToken>()), 
                    Times.Once());

            _transport
                .Verify(t => t.SendAsync(
                    It.Is<Envelope>(e => e is Session && ((Session)e).State == SessionState.Finished && ((Session)e).Id == target.SessionId), It.IsAny<CancellationToken>()), Times.Once());

            _transport
                .Verify(t =>
                    t.CloseAsync(It.IsAny<CancellationToken>()),
                    Times.Once());
        }

        #endregion

        private class TestServerChannel : ServerChannel
        {
            public TestServerChannel(SessionState state, Guid sessionId, Node serverNode, ITransport transport, TimeSpan sendTimeout, Node remoteNode, TimeSpan? remotePingInterval = null, TimeSpan? remoteIdleTimeout = null)
                : base(sessionId, serverNode, transport, sendTimeout, remotePingInterval: remotePingInterval, remoteIdleTimeout: remoteIdleTimeout)
            {                
                base.State = state;
                base.RemoteNode = remoteNode;
            }
        }

    }
}