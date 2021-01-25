﻿using System;
using System.IO.Pipes;
using System.Threading;
using WatchdogClient.IO;

namespace WatchdogClient
{
    public delegate void PipeExceptionEventHandler(Exception exception);

    /// <summary>
    ///     Wraps a <see cref="NamedPipeClientStream" />.
    /// </summary>
    /// <typeparam name="TReadWrite">Reference type to read from and write to the named pipe</typeparam>
    public class NamedPipeClient<TReadWrite> : NamedPipeClient<TReadWrite, TReadWrite> where TReadWrite : class
    {
        /// <summary>
        ///     Constructs a new <c>NamedPipeClient</c> to connect to the NamedPipeNamedPipeServer/> specified by
        ///     <paramref name="pipeName" />.
        /// </summary>
        /// <param name="pipeName">Name of the server's pipe</param>
        public NamedPipeClient(string pipeName) : base(pipeName)
        {
        }
    }

    /// <summary>
    ///     Wraps a <see cref="NamedPipeClientStream" />.
    /// </summary>
    /// <typeparam name="TRead">Reference type to read from the named pipe</typeparam>
    /// <typeparam name="TWrite">Reference type to write to the named pipe</typeparam>
    public class NamedPipeClient<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        private readonly string _pipeName;

        private readonly AutoResetEvent _connected = new AutoResetEvent(false);
        private readonly AutoResetEvent _disconnected = new AutoResetEvent(false);
        private NamedPipeConnection<TRead, TWrite> _connection;

        private volatile bool _closedExplicitly;
        private bool _wasConnected;


        /// <summary>
        ///     Constructs a new <c>NamedPipeClient</c> to connect to the NamedPipeServer /> specified by
        ///     <paramref name="pipeName" />.
        /// </summary>
        /// <param name="pipeName">Name of the server's pipe</param>
        public NamedPipeClient(string pipeName)
        {
            _pipeName = pipeName;
            AutoReconnect = true;
        }

        /// <summary>
        ///     Gets or sets whether the client should attempt to reconnect when the pipe breaks
        ///     due to an error or the other end terminating the connection.
        ///     Default value is <c>true</c>.
        /// </summary>
        public bool AutoReconnect { get; set; }


        /// <summary>
        ///     Gets a value indicating whether the pipe is connected or not.
        /// </summary>
        public bool IsConnected => _connection.IsConnected;

        /// <summary>
        ///     Connects to the named pipe server asynchronously.
        ///     This method returns immediately, possibly before the connection has been established.
        /// </summary>
        public void Start()
        {
            _closedExplicitly = false;
            _wasConnected = false;
            var worker = new Worker();
            worker.Error += OnError;
            worker.DoWork(ListenSync);
        }

        /// <summary>
        ///     Sends a message to the server over a named pipe.
        /// </summary>
        /// <param name="message">Message to send to the server.</param>
        public void PushMessage(TWrite message)
        {
            if (_connection != null)
            {
                _connection.PushMessage(message);
            }
        }

        /// <summary>
        ///     Closes the named pipe.
        /// </summary>
        public void Stop()
        {
            _closedExplicitly = true;
            if (_connection != null)
            {
                _connection.Close();
            }
        }

        /// <summary>
        ///     Invoked whenever a message is received from the server.
        /// </summary>
        public event ConnectionMessageEventHandler<TRead, TWrite> ServerMessage;

        /// <summary>
        ///     Invoked when the client disconnects from the server (e.g., the pipe is closed or broken).
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> Disconnected;


        /// <summary>
        ///     Invoked when the client connects to the server
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> Connected;
        /// <summary>
        ///     Invoked whenever an exception is thrown during a read or write operation on the named pipe.
        /// </summary>
        public event PipeExceptionEventHandler Error;

        #region Wait for connection/disconnection

        /// <summary>
        ///     Waits for connection
        /// </summary>
        public void WaitForConnection()
        {
            _connected.WaitOne();
        }

        /// <summary>
        ///     Waits for connection with a time-out
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        public void WaitForConnection(int millisecondsTimeout)
        {
            _connected.WaitOne(millisecondsTimeout);
        }

        /// <summary>
        ///     Waits for connection with a time-out
        /// </summary>
        /// <param name="timeout"></param>
        public void WaitForConnection(TimeSpan timeout)
        {
            _connected.WaitOne(timeout);
        }

        /// <summary>
        ///     Wait for disconnection
        /// </summary>
        public void WaitForDisconnection()
        {
            _disconnected.WaitOne();
        }

        /// <summary>
        ///     Wait for disconnection with time-out
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        public void WaitForDisconnection(int millisecondsTimeout)
        {
            _disconnected.WaitOne(millisecondsTimeout);
        }

        /// <summary>
        ///     Wait for disconnection with time-out
        /// </summary>
        /// <param name="timeout"></param>
        public void WaitForDisconnection(TimeSpan timeout)
        {
            _disconnected.WaitOne(timeout);
        }

        #endregion

        #region Private methods

        private void ListenSync()
        {
            // Get the name of the data pipe that should be used from now on by this NamedPipeClient
            var handshake = PipeClientFactory.Connect<string, string>(_pipeName);
            var dataPipeName = handshake.ReadObject();
            handshake.Close();

            // Connect to the actual data pipe
            var dataPipe = PipeClientFactory.CreateAndConnectPipe(dataPipeName);

            // Create a Connection object for the data pipe
            _connection = ConnectionFactory.CreateConnection<TRead, TWrite>(dataPipe);
            _connection.Disconnected += OnDisconnected;
            _connection.ReceiveMessage += OnReceiveMessage;
            _connection.Error += ConnectionOnError;
            _connection.Open();

            _connected.Set();
        }

        private void OnDisconnected(NamedPipeConnection<TRead, TWrite> connection)
        {
            if (Disconnected != null)
            {
                Disconnected(connection);
            }

            _wasConnected = false;

            _disconnected.Set();

            // Reconnect
            if (AutoReconnect && !_closedExplicitly)
            {
                Start();
            }
        }

        private void OnReceiveMessage(NamedPipeConnection<TRead, TWrite> connection, TRead message)
        {
            if (!_wasConnected && Connected != null)
            {
                Connected(connection);
                _wasConnected = true;
            }

            if (ServerMessage != null)
            {
                ServerMessage(connection, message);
            }
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        private void ConnectionOnError(NamedPipeConnection<TRead, TWrite> connection, Exception exception)
        {
            OnError(exception);
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        /// <param name="exception"></param>
        private void OnError(Exception exception)
        {
            if (Error != null)
            {
                Error(exception);
            }
        }

        #endregion
    }

    internal static class PipeClientFactory
    {
        public static PipeStreamWrapper<TRead, TWrite> Connect<TRead, TWrite>(string pipeName)
            where TRead : class
            where TWrite : class
        {
            return new PipeStreamWrapper<TRead, TWrite>(CreateAndConnectPipe(pipeName));
        }

        public static NamedPipeClientStream CreateAndConnectPipe(string pipeName)
        {
            var pipe = CreatePipe(pipeName);
            pipe.Connect();
            return pipe;
        }

        private static NamedPipeClientStream CreatePipe(string pipeName)
        {
            return new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }
    }
}