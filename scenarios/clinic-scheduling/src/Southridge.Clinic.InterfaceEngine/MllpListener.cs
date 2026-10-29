using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;
using Southridge.Clinic.Hl7;

namespace Southridge.Clinic.InterfaceEngine
{
    /// <summary>Accepts MLLP connections on one port; each connection gets a pool thread and may carry many messages.</summary>
    public class MllpListener
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MllpListener));

        private readonly ListenerElement _config;
        private readonly InboundProcessor _processor;
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _stopping;
        private int _connections;

        public MllpListener(ListenerElement config, InboundProcessor processor)
        {
            _config = config;
            _processor = processor;
        }

        public string Partner
        {
            get { return _config.Partner; }
        }

        public int ActiveConnections
        {
            get { return _connections; }
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Parse(_config.Address), _config.Port);
            _listener.Start();
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "mllp-" + _config.Partner };
            _acceptThread.Start();
            Log.InfoFormat("Listening for {0} on {1}:{2} ({3})", _config.Partner, _config.Address, _config.Port, _config.Accept);
        }

        public void Stop()
        {
            _stopping = true;
            if (_listener != null)
            {
                _listener.Stop();
            }

            if (_acceptThread != null && !_acceptThread.Join(TimeSpan.FromSeconds(5)))
            {
                Log.Warn("Accept thread for " + _config.Partner + " did not stop in time.");
            }
        }

        private void AcceptLoop()
        {
            while (!_stopping)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    if (_stopping)
                    {
                        return;
                    }

                    throw;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(Serve, client);
            }
        }

        private void Serve(object state)
        {
            var client = (TcpClient)state;
            string peer = client.Client.RemoteEndPoint.ToString();
            Interlocked.Increment(ref _connections);
            Log.DebugFormat("{0} connected from {1}", _config.Partner, peer);
            try
            {
                client.ReceiveTimeout = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    string text;
                    while (!_stopping && (text = Mllp.Read(stream)) != null)
                    {
                        string ack = _processor.Process(_config.Partner, _config.Accept, text);
                        Mllp.Write(stream, ack);
                    }
                }
            }
            catch (IOException ex)
            {
                Log.Debug(_config.Partner + " connection from " + peer + " closed: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(_config.Partner + " connection from " + peer + " failed.", ex);
            }
            finally
            {
                Interlocked.Decrement(ref _connections);
            }
        }
    }
}
