using System.Threading;

namespace System.Net.Sockets
{
    // Socket using 'SocketAsyncEventArgs' to communication from client to server.
    public sealed class AsynchronousSocket
    {
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(true);

        private Socket _socket;
        private IPEndPoint _endPoint;

        public AsynchronousSocket(IPAddress ipAddress, int port,
            SocketType socketType, ProtocolType protocolType)
        {
            // Gaurd clause.
            if (ipAddress == null)
                throw new ArgumentNullException(nameof(ipAddress));

            _endPoint = new IPEndPoint(ipAddress, port);
            _socket = new Socket(_endPoint.AddressFamily, socketType, protocolType);
        }

        public event EventHandler<SocketAsyncEventArgs> Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SocketAsyncEventArgs> Received;
        public event EventHandler<SocketAsyncEventArgs> Sent;

        public bool IsConnected => _socket.Connected;

        public void Connect()
        {
            var connectArgs = CreateSocketAsyncEventArgs(OnConnect);

            // Start connect operation.
            _socket.ConnectAsync(connectArgs);

            // Wait for connect operation to complete.
            WaitResetEvent();

            if (connectArgs.SocketError != SocketError.Success)
            {
                ProcessError(connectArgs);
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e)
                {
                    // Usually is thrown when already closed.
                    throw e;
                }
                finally
                {
                    // Signal disconnect event for processing.
                    Disconnected?.Invoke(this, new EventArgs());
                }
            }
        }

        public void Send(byte[] bufferSend)
        {
            // Guard clause.
            if (bufferSend == null)
                throw new ArgumentNullException(nameof(bufferSend));

            // Gaurd clause.
            if (!IsConnected && _socket.ProtocolType != ProtocolType.Udp)
                throw new SocketException((int)SocketError.NotConnected);

            var sendArgs = CreateSocketAsyncEventArgs(OnSend);
            sendArgs.SetBuffer(bufferSend, 0, bufferSend.Length);

            // Start send operation.
            if (_socket.ProtocolType == ProtocolType.Udp)
                _socket.SendToAsync(sendArgs);
            else
                _socket.SendAsync(sendArgs);

            // Wait for send operation to complete.
            WaitResetEvent();
        }

        private SocketAsyncEventArgs CreateSocketAsyncEventArgs(EventHandler<SocketAsyncEventArgs> eventHandler)
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs()
            {
                UserToken = _socket,
                RemoteEndPoint = _endPoint
            };
            args.Completed += new EventHandler<SocketAsyncEventArgs>(eventHandler);

            return args;
        }

        private void OnConnect(object sender, SocketAsyncEventArgs e)
        {
            SignalResetEvent();

            if (!IsConnected || e.SocketError != SocketError.Success)
                ProcessError(e);

            // Signal Connected event for processing.
            Connected?.Invoke(this, e);

            byte[] receiveBuffer = new byte[1024];
            var receiveArgs = CreateSocketAsyncEventArgs(OnReceive);

            // Start receive operation.
            _socket.ReceiveAsync(receiveArgs);

            // Wait for receive operation to complete.
            WaitResetEvent();
        }

        private void OnReceive(object sender, SocketAsyncEventArgs e)
        {
            SignalResetEvent();

            // Signal received event for processing.
            Received?.Invoke(this, e);

            if (e.SocketError != SocketError.Success)
                ProcessError(e);
        }
        private void OnSend(object sender, SocketAsyncEventArgs e)
        {
            SignalResetEvent();

            // Signal sent event for processing.
            Sent?.Invoke(this, e);

            if (e.SocketError != SocketError.Success)
                ProcessError(e);

            byte[] receiveBuffer = new byte[1024];
            var receiveArgs = CreateSocketAsyncEventArgs(OnReceive);

            // Start receive operation.
            _socket.ReceiveAsync(receiveArgs);

            // Wait for receive operation to complete.
            WaitResetEvent();
        }

        private void ProcessError(SocketAsyncEventArgs e)
        {
            Disconnect();

            throw new SocketException((int)e.SocketError);
        }

        private void SignalResetEvent()
        {
            _resetEvent.Set();
        }

        private void WaitResetEvent()
        {
            _resetEvent.Reset();
            _resetEvent.WaitOne();
        }
    }
}
