﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lime.Protocol;
using Lime.Protocol.Network;
using Lime.Protocol.Serialization;
using Lime.Protocol.UnitTests;
using Lime.Transport.Tcp;
using Xunit;
using Moq;
using Shouldly;

namespace Lime.Transport.Tcp.UnitTests
{    
    public class TcpTransportTests
    {
        #region Private Fields

        private Mock<ITcpClient> _tcpClient;
        private Mock<IEnvelopeSerializer> _envelopeSerializer;
        private Mock<ITraceWriter> _traceWriter;
        private Mock<Stream> _stream;

        #endregion

        #region Scenario

        public TcpTransportTests()
        {
            _stream = new Mock<Stream>();
            _tcpClient = new Mock<ITcpClient>();
            _tcpClient
                .Setup(c => c.GetStream())
                .Returns(() => _stream.Object);

            _envelopeSerializer = new Mock<IEnvelopeSerializer>();
            _traceWriter = new Mock<ITraceWriter>();
        }

        #endregion

        #region Private methods

        private TcpTransport GetTarget(X509Certificate2 certificate = null, int bufferSize = TcpTransport.DEFAULT_BUFFER_SIZE)
        {
            return new TcpTransport(
                _tcpClient.Object,
                _envelopeSerializer.Object,
                certificate,
                bufferSize,
                _traceWriter.Object);
        }
        private async Task<TcpTransport> GetAndOpenTargetAsync(int bufferSize = TcpTransport.DEFAULT_BUFFER_SIZE, Stream stream = null)
        {
            if (stream == null)
            {
                stream = _stream.Object;
            }

            var uri = Dummy.CreateUri(Uri.UriSchemeNetTcp);
            var cancellationToken = CancellationToken.None;

            var readTcs = new TaskCompletionSource<int>();

            _tcpClient
                .Setup(c => c.Connected)
                .Returns(true)
                .Verifiable();

            _tcpClient
                .Setup(s => s.GetStream())
                .Returns(stream);

            var target = GetTarget(bufferSize: bufferSize);

            await target.OpenAsync(uri, cancellationToken);
            return target;
        }  

        #endregion

        #region OpenAsync

        [Fact]
        [Trait("Category", "OpenAsync")]
        public async Task OpenAsync_NotConnectedValidUri_ConnectsClientAndCallsGetStream()
        {
            var uri = Dummy.CreateUri(Uri.UriSchemeNetTcp);
            var cancellationToken = CancellationToken.None;
            int bufferSize = Dummy.CreateRandomInt(10000);

            var readTcs = new TaskCompletionSource<int>();

            _tcpClient
                .Setup(c => c.Connected)
                .Returns(false)
                .Verifiable();

            var target = GetTarget(bufferSize: bufferSize);

            await target.OpenAsync(uri, cancellationToken);

            _tcpClient.Verify();
            _stream.Verify();

            _tcpClient.Verify(
                c => c.ConnectAsync(
                    uri.Host,
                    uri.Port),
                Times.Once());

            _tcpClient.Verify(
                c => c.GetStream(),
                Times.Once());
        }

        [Fact]
        [Trait("Category", "OpenAsync")]        
        public async Task OpenAsync_NotConnectedInvalidUriScheme_ThrowsArgumentException()
        {
            var uri = Dummy.CreateUri(Uri.UriSchemeHttp);
            var cancellationToken = CancellationToken.None;

            _tcpClient
                .Setup(c => c.Connected)
                .Returns(false)
                .Verifiable();

            var target = GetTarget();

            Should.Throw<ArgumentException>(() => target.OpenAsync(uri, cancellationToken));
        }

        [Fact]
        [Trait("Category", "OpenAsync")]
        public async Task OpenAsync_AlreadyConnectedValidUri_CallsGetStream()
        {
            var uri = Dummy.CreateUri(Uri.UriSchemeNetTcp);
            var cancellationToken = CancellationToken.None;
            int bufferSize = Dummy.CreateRandomInt(10000);

            var readTcs = new TaskCompletionSource<int>();

            _tcpClient
                .Setup(c => c.Connected)
                .Returns(true)
                .Verifiable();

            var target = GetTarget(bufferSize: bufferSize);

            await target.OpenAsync(uri, cancellationToken);

            _tcpClient.Verify();
            _stream.Verify();

            _tcpClient.Verify(
                c => c.ConnectAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>()),
                Times.Never());

            _tcpClient.Verify(
                c => c.GetStream(),
                Times.Once());
        }
    
        #endregion

        #region SendAsync

        [Fact]
        [Trait("Category", "SendAsync")]
        public async Task SendAsync_ValidArgumentsAndOpenStreamAndTraceEnabled_CallsWriteAsyncAndTraces()
        {
            var target = await this.GetAndOpenTargetAsync();

            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            var serializedMessage = Dummy.CreateRandomString(200);
            var serializedMessageBytes = Encoding.UTF8.GetBytes(serializedMessage);

            var cancellationToken = CancellationToken.None;

            _stream
                .Setup(s => s.CanWrite)
                .Returns(true)
                .Verifiable();

            _envelopeSerializer
                .Setup(e => e.Serialize(message))
                .Returns(serializedMessage);

            _stream
                .Setup(s =>
                    s.WriteAsync(
                        It.IsAny<byte[]>(),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<object>(null))
                .Verifiable();

            _traceWriter
                .Setup(t => t.IsEnabled)
                .Returns(true);

            await target.SendAsync(message, cancellationToken);

            _stream.Verify();

            _stream
                .Verify(s =>
                    s.WriteAsync(
                        It.Is<byte[]>(b => b.SequenceEqual(serializedMessageBytes)),
                        It.Is<int>(o => o == 0),
                        It.Is<int>(l => l == serializedMessageBytes.Length),
                        cancellationToken),
                    Times.Once());

            _traceWriter
                .Verify(t =>
                    t.TraceAsync(serializedMessage, DataOperation.Send),
                Times.Once());
        }

        [Fact]
        [Trait("Category", "SendAsync")]
        public async Task SendAsync_NullEnvelope_ThrowsArgumentNullException()
        {
            var target = this.GetTarget();
            
            Envelope message = null;

            var cancellationToken = CancellationToken.None;

            Should.Throw<ArgumentNullException>(() => target.SendAsync(message, cancellationToken));
        }

        [Fact]
        [Trait("Category", "SendAsync")]
        public async Task SendAsync_ClosedTransport_ThrowsInvalidOperationException()
        {
            var target = this.GetTarget();

            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);

            var cancellationToken = CancellationToken.None;

            Should.Throw<InvalidOperationException>(() => target.SendAsync(message, cancellationToken));
        }

        [Fact]
        [Trait("Category", "SendAsync")]
        public async Task SendAsync_IOException_ThrowsIOExceptionAndCallsCloseAsync()
        {
            var target = await this.GetAndOpenTargetAsync();

            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            var serializedMessage = Dummy.CreateRandomString(200);
            var serializedMessageBytes = Encoding.UTF8.GetBytes(serializedMessage);
            var cancellationToken = CancellationToken.None;

            _tcpClient
                .Setup(s => s.Close())
                .Verifiable();

            _stream
                .Setup(s => s.Close())
                .Verifiable();

            _stream
                .Setup(s => s.CanWrite)
                .Returns(true);
                
            _stream
                .Setup(s =>
                    s.WriteAsync(
                        It.IsAny<byte[]>(),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                .Throws(new IOException())
                .Verifiable();

            _envelopeSerializer
                .Setup(e => e.Serialize(message))
                .Returns(serializedMessage);

            Should.Throw<IOException>(() => target.SendAsync(message, cancellationToken));

            _tcpClient.Verify();
            _stream.Verify();
        }

        #endregion

        #region ReceiveAsync

        [Fact]
        [Trait("Category", "ReceiveAsync")]
        public async Task ReceiveAsync_OneRead_ReadEnvelopeJsonFromStream()
        {
            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            var messageJson = Dummy.CreateMessageJson();
            var cancelationToken = Dummy.CreateCancellationToken();

            byte[] messageBuffer = Encoding.UTF8.GetBytes(
                messageJson);

            int bufferSize = messageBuffer.Length + Dummy.CreateRandomInt(1000);
            var stream = new TestStream(messageBuffer);
            var target = await GetAndOpenTargetAsync(bufferSize, stream);

            _envelopeSerializer
                .Setup(e => e.Deserialize(messageJson))
                .Returns(message)
                .Verifiable();

            var actual = await target.ReceiveAsync(cancelationToken);

            _stream.Verify();
            _envelopeSerializer.Verify();

            Assert.Equal(message, actual);

            Assert.Equal(1, stream.ReadCount);
        }

        [Fact]
        [Trait("Category", "ReceiveAsync")]
        
        public async Task ReceiveAsync_NotStarted_ThrowsInvalidOperationException()
        {
            var cancelationToken = Dummy.CreateCancellationToken();
            var target = GetTarget();

            Should.Throw<InvalidOperationException>(() => target.ReceiveAsync(cancelationToken));
        }

        [Fact]
        [Trait("Category", "ReceiveAsync")]
        public async Task ReceiveAsync_MultipleReads_ReadEnvelopeJsonFromStream()
        {
            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            var messageJson = Dummy.CreateMessageJson();
            var cancelationToken = Dummy.CreateCancellationToken();

            var bufferParts = Dummy.CreateRandomInt(10) + 1;

            byte[] messageBuffer = Encoding.UTF8.GetBytes(
                messageJson);

            var messageBufferParts = SplitBuffer(messageBuffer);
            int bufferSize = messageBuffer.Length + Dummy.CreateRandomInt(1000);
            var stream = new TestStream(messageBufferParts);
            var target = await GetAndOpenTargetAsync(bufferSize, stream);

            _envelopeSerializer
                .Setup(e => e.Deserialize(messageJson))
                .Returns(message)
                .Verifiable();

            var actual = await target.ReceiveAsync(cancelationToken);            

            _stream.Verify();
            _envelopeSerializer.Verify();

            Assert.Equal(message, actual);
            Assert.Equal(messageBufferParts.Length, stream.ReadCount);
        }

        [Fact]
        [Trait("Category", "ReceiveAsync")]
        public async Task ReceiveAsync_MultipleReadsMultipleEnvelopes_ReadEnvelopesJsonFromStream()
        {
            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            int messagesCount = Dummy.CreateRandomInt(100) + 1;
            var messageJsonQueue = new Queue<string>();

            for (int i = 0; i < messagesCount; i++)
            {
                string messageJson;

                do
                {
                    messageJson = Dummy.CreateMessageJson();
                } while (messageJsonQueue.Contains(messageJson));
              
                messageJsonQueue.Enqueue(messageJson);
            }

            var messagesJsons = string.Join("", messageJsonQueue);                                    
            var cancelationToken = Dummy.CreateCancellationToken();
            byte[] messageBuffer = Encoding.UTF8.GetBytes(messagesJsons);
            var messageBufferParts = SplitBuffer(messageBuffer);         
            int bufferSize = messageBuffer.Length + Dummy.CreateRandomInt(1000);
            var stream = new TestStream(messageBufferParts);
            var target = await GetAndOpenTargetAsync(bufferSize, stream);

            _envelopeSerializer
                .Setup(e => e.Deserialize(It.Is<string>(s => messageJsonQueue.Contains(s))))
                .Returns(message)
                .Callback(() => messageJsonQueue.Dequeue())
                .Verifiable();

            try
            {
                for (int i = 0; i < messagesCount; i++)
                {
                    var actual = await target.ReceiveAsync(cancelationToken);
                    Assert.Equal(message, actual);
                }

                _stream.Verify();
                _envelopeSerializer.Verify();

                Assert.Equal(messageBufferParts.Length, stream.ReadCount);
                Assert.Equal(0, messageJsonQueue.Count);
            }
            catch (Exception)
            {
                Debug.WriteLine("Buffer parts: {0}", messageBuffer.Length);
                Debug.WriteLine("Buffer: ");
                Debug.Write(messagesJsons);
                Debug.WriteLine("");
                Debug.Flush();
                                
                throw;
            }
        }

        [Fact]
        [Trait("Category", "ReceiveAsync")]
        public async Task ReceiveAsync_MultipleReadsMultipleEnvelopesWithInvalidCharsBetween_ReadEnvelopeJsonFromStream()
        {
            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            int messagesCount = Dummy.CreateRandomInt(100) + 1;
            var messageJsonQueue = new Queue<string>();

            for (int i = 0; i < messagesCount; i++)
            {
                string messageJson;

                do
                {
                    messageJson = Dummy.CreateMessageJson();
                } while (messageJsonQueue.Contains(messageJson));

                messageJsonQueue.Enqueue(messageJson);
            }

            var messagesJsons = "  \t\t " + string.Join("\r\n   ", messageJsonQueue);
            var cancelationToken = Dummy.CreateCancellationToken();            
            byte[] messageBuffer = Encoding.UTF8.GetBytes(messagesJsons);
            var messageBufferParts = SplitBuffer(messageBuffer);
            int bufferSize = messageBuffer.Length + Dummy.CreateRandomInt(1000);
            var stream = new TestStream(messageBufferParts);
            var target = await GetAndOpenTargetAsync(bufferSize, stream);

            _envelopeSerializer
                .Setup(e => e.Deserialize(It.Is<string>(s => messageJsonQueue.Contains(s))))
                .Returns(message)
                .Callback(() => messageJsonQueue.Dequeue())
                .Verifiable();

            try
            {
                for (int i = 0; i < messagesCount; i++)
                {
                    var actual = await target.ReceiveAsync(cancelationToken);
                    Assert.Equal(message, actual);
                }

                _stream.Verify();
                _envelopeSerializer.Verify();

                Assert.Equal(messageBufferParts.Length, stream.ReadCount);
                Assert.Equal(0, messageJsonQueue.Count);
            }
            catch (Exception)
            {
                Debug.WriteLine("Buffer parts: {0}", messageBuffer.Length);
                Debug.WriteLine("Buffer: ");
                Debug.Write(messagesJsons);
                Debug.WriteLine("");
                Debug.Flush();

                throw;
            }
        }


        [Fact]
        [Trait("Category", "ReceiveAsync")]
        public async Task ReceiveAsync_SingleReadBiggerThenBuffer_ClosesStreamAndThrowsInvalidOperationException()
        {
            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            var messageJson = Dummy.CreateMessageJson();
            var cancelationToken = Dummy.CreateCancellationToken();
            byte[] messageBuffer = Encoding.UTF8.GetBytes(messageJson);
            int bufferSize = messageBuffer.Length - 1;
            var stream = new TestStream(messageBuffer);
            var target = await GetAndOpenTargetAsync(bufferSize, stream);

            _envelopeSerializer
                .Setup(e => e.Deserialize(messageJson))
                .Returns(message)
                .Verifiable();

            _envelopeSerializer
                .Setup(e => e.Deserialize(messageJson))
                .Returns(message)
                .Verifiable();

            try
            {
                var actual = await target.ReceiveAsync(cancelationToken);
                throw new Exception("The expected exception didn't occurred");
            }
            catch (Exception ex)
            {
                ex.ShouldBeOfType<BufferOverflowException>();
                stream.CloseInvoked.ShouldBe(true);
            }

        }

        [Fact]
        [Trait("Category", "ReceiveAsync")]
        public async Task ReceiveAsync_MultipleReadsBiggerThenBuffer_ClosesTheTransportAndThrowsInvalidOperationException()
        {
            var content = Dummy.CreateTextContent();
            var message = Dummy.CreateMessage(content);
            var messageJson = Dummy.CreateMessageJson();
            var cancelationToken = Dummy.CreateCancellationToken();
            byte[] messageBuffer = Encoding.UTF8.GetBytes(messageJson);
            var messageBufferParts = SplitBuffer(messageBuffer);
            int bufferSize = messageBuffer.Length - 1;
            var stream = new TestStream(messageBufferParts);
            var target = await GetAndOpenTargetAsync(bufferSize, stream);

            _envelopeSerializer
                .Setup(e => e.Deserialize(messageJson))
                .Returns(message)
                .Verifiable();

            try
            {
                var actual = await target.ReceiveAsync(cancelationToken);
                throw new Exception("The expected exception didn't occurred");
            }
            catch (Exception ex)
            {
                Assert.True(ex is BufferOverflowException);
                Assert.True(stream.CloseInvoked);
            }
            
        }


        private static byte[][] SplitBuffer(byte[] messageBuffer)
        {
            var bufferParts = Dummy.CreateRandomInt(100) + 1;
            if (bufferParts >= messageBuffer.Length) bufferParts = messageBuffer.Length - 1;
            var bufferPartSize = messageBuffer.Length / bufferParts;

            byte[][] messageBufferParts = new byte[bufferParts][];

            for (int i = 0; i < bufferParts; i++)
            {
                if (i + 1 == bufferParts)
                {
                    messageBufferParts[i] = messageBuffer
                        .Skip(i * bufferPartSize)
                        .ToArray();
                }
                else
                {
                    messageBufferParts[i] = messageBuffer
                        .Skip(i * bufferPartSize)
                        .Take(bufferPartSize)
                        .ToArray();
                }
            }
            return messageBufferParts;
        }        
        #endregion

        #region PerformCloseAsync

        [Fact]
        [Trait("Category", "PerformCloseAsync")]
        public async Task PerformCloseAsync_StreamOpened_ClosesStreamAndClient()
        {
            var cancellationToken = CancellationToken.None;

            _stream
                .Setup(s => s.Close())
                .Verifiable();

            _tcpClient
                .Setup(s => s.Close())
                .Verifiable();

            var target = await GetAndOpenTargetAsync();

            await target.CloseAsync(cancellationToken);

            _stream.Verify();
            _tcpClient.Verify();

        }

        [Fact]
        [Trait("Category", "PerformCloseAsync")]
        public async Task PerformCloseAsync_NoStream_ClosesClient()
        {
            var cancellationToken = CancellationToken.None;

            _tcpClient
                .Setup(s => s.Close())
                .Verifiable();

            var target = GetTarget();

            await target.CloseAsync(cancellationToken);

            _tcpClient.Verify();
        }

        [Fact]
        [Trait("Category", "PerformCloseAsync")]
        public async Task PerformCloseAsync_AlreadyClosed_ClosesClient()
        {
            var cancellationToken = CancellationToken.None;

            _tcpClient
                .Setup(s => s.Close())
                .Verifiable();

            var target = GetTarget();

            await target.CloseAsync(cancellationToken);

            await target.CloseAsync(cancellationToken);

            _tcpClient.Verify();
        }

        #endregion

        #region GetSupportedEncryption

        [Fact]
        [Trait("Category", "GetSupportedEncryption")]
        public void GetSupportedEncryption_Default_ReturnsNoneAndTLS()
        {
            var target = GetTarget();

            var actual = target.GetSupportedEncryption();

            Assert.Equal(2, actual.Length);
            Assert.True(actual.Contains(SessionEncryption.None));
            Assert.True(actual.Contains(SessionEncryption.TLS));

        }

        #endregion

        private class TestStream : Stream
        {

            private byte[][] _buffers;

            public TestStream(params byte[][] buffers)
            {
                _buffers = buffers;
                this.ReadCount = 0;
            }

            public override bool CanRead 
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanWrite
            {
                get { throw new NotImplementedException(); }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Length
            {
                get { throw new NotImplementedException(); }
            }

            public override long Position { get; set; }

            public int ReadCount { get; private set; }


            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {                
                if (this.ReadCount >= _buffers.Length)
                {
                    throw new InvalidOperationException("Buffer end reached");
                }

                var currentBuffer = _buffers[this.ReadCount];
                this.ReadCount++;

                Array.Copy(currentBuffer, 0, buffer, offset, currentBuffer.Length > count ? count : currentBuffer.Length);

                this.Position += currentBuffer.Length;

                return Task.FromResult(currentBuffer.Length);  
            }

            public bool CloseInvoked { get; set; }

            public override void Close()
            {
                CloseInvoked = true;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }
    }
}
