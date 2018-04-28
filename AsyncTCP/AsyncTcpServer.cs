#region 文件说明
/*------------------------------------------------------------------------------
// Copyright © 2018 Granda. All Rights Reserved.
// 苏州广林达电子科技有限公司 版权所有
//------------------------------------------------------------------------------
// File Name: AsyncTcpServer
// Author: Ivan JL Zhang    Date: 2018/4/9 10:32:05    Version: 1.0.0
// Description: 
//   
// 
// Revision History: 
// <Author>  		<Date>     	 	<Revision>  		<Modification>
// 	
//----------------------------------------------------------------------------*/
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.IO;

namespace AsyncTCP
{
    public class AsyncTcpServer : IDisposable
    {
        private TcpListener _listener;
        private ConcurrentDictionary<string, TcpClientState> _clients;
        private bool _disposed = false;

        #region 构造方法
        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="listenPort">监听的端口</param>
        public AsyncTcpServer(int listenPort) :
            this(IPAddress.Any, listenPort)
        {

        }
        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localEP">监听的终结点</param>
        public AsyncTcpServer(IPEndPoint localEP) :
            this(localEP.Address, localEP.Port)
        { }
        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localIpAddress">监听的IP地址</param>
        /// <param name="listenPort">监听的端口</param>
        public AsyncTcpServer(IPAddress localIpAddress, int listenPort)
        {
            Address = localIpAddress;
            Port = listenPort;
            this.Encoding = Encoding.ASCII;

            _clients = new ConcurrentDictionary<string, TcpClientState>();

            _listener = new TcpListener(Address, Port);
            _listener.AllowNatTraversal(true);
        }
        #endregion

        #region properties
        /// <summary>
        /// 服务器是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// 监听的IP地址
        /// </summary>
        public IPAddress Address { get; private set; }
        /// <summary>
        /// 监听的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public Encoding Encoding { get; private set; }
        #endregion

        #region Server
        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns></returns>
        public AsyncTcpServer Start()
        {
            return Start(10);
        }
        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">服务器所允许的挂起连接序列的最大长度</param>
        /// <returns></returns>
        public AsyncTcpServer Start(int backlog)
        {
            if (!IsRunning)
            {
                IsRunning = true;
                _listener.Start(backlog);
                ContinueAcceptTcpClient(_listener);
            }
            return this;
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns></returns>
        public AsyncTcpServer Stop()
        {
            if (!IsRunning) return this;


            try
            {
                _listener.Stop();
                lock (this._clients)
                {
                    foreach (var item in this._clients.Values)
                    {
                        item.TcpClient.Client.Disconnect(false);
                    }
                    this._clients.Clear();
                }
                IsRunning = false;
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Error("", ex);
            }
            catch (SocketException ex)
            {
                Logger.Error("", ex);
            }

            return this;
        }

        private void ContinueAcceptTcpClient(TcpListener listener)
        {
            try
            {
                listener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), listener);
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Error("", ex);
            }
            catch (SocketException ex)
            {
                Logger.Error("", ex);
            }
        }
        #endregion

        #region Receive
        private void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (IsRunning)
            {
                TcpListener tcpListener = (TcpListener)ar.AsyncState;
                TcpClient tcpClient = tcpListener.EndAcceptTcpClient(ar);
                if (!tcpClient.Connected) return;

                byte[] buffer = new byte[tcpClient.ReceiveBufferSize];

                // add client connection to cache
                TcpClientState tcpClientState = new TcpClientState(tcpClient, buffer);
                lock (this._clients)
                {
                    string clientKey = tcpClientState.TcpClient.Client.RemoteEndPoint.ToString();
                    this._clients.AddOrUpdate(clientKey, tcpClientState, (c, t) => { return tcpClientState; });
                    RaiseClientConnected(tcpClientState);
                }
                // begin to read data
                NetworkStream networkStream = tcpClientState.NetworkStream;
                ContinueReadBuffer(tcpClientState, networkStream);

                // keep listening to accept next connection
                ContinueAcceptTcpClient(tcpListener);
            }
        }

        private void HandleDatagramReceived(IAsyncResult ar)
        {
            if (!IsRunning) return;
            try
            {
                TcpClientState tcpClientState = (TcpClientState)ar.AsyncState;
                if (!tcpClientState.TcpClient.Connected) return;

                NetworkStream networkStream = tcpClientState.NetworkStream;
                int numberOfReadBytes = 0;
                try
                {
                    // if the remote host has shutdown its connection, 
                    // read will immediately return with zero bytes.
                    numberOfReadBytes = networkStream.EndRead(ar);
                }
                catch (Exception ex)
                {
                    Logger.Error("", ex);
                    numberOfReadBytes = 0;
                }

                if (numberOfReadBytes == 0)
                {
                    // connection has been closed
                    string tcpClientKey = tcpClientState.TcpClient.Client.RemoteEndPoint.ToString();
                    _clients.TryRemove(tcpClientKey, out TcpClientState internalClientToBeThrowAway);
                    RaiseClientDisconnected(tcpClientState.TcpClient);
                    return;
                }

                // received byte and trigger event notification
                byte[] receivedBytes = new byte[numberOfReadBytes];
                Buffer.BlockCopy(
                    tcpClientState.Buffer,
                    0,
                    receivedBytes,
                    0,
                    numberOfReadBytes);
                RaiseDatagramReceived(tcpClientState.TcpClient, receivedBytes);
                RaisePlaintextReceived(tcpClientState.TcpClient, receivedBytes);

                // continue listening for tcp datagram packets
                ContinueReadBuffer(tcpClientState, networkStream);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error("", ex);
            }
        }

        private void ContinueReadBuffer(TcpClientState tcpClientState, NetworkStream networkStream)
        {
            try
            {
                networkStream.BeginRead(
                    tcpClientState.Buffer,
                    0,
                    tcpClientState.Buffer.Length,
                    HandleDatagramReceived,
                    tcpClientState);
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Error("", ex);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 接收到数据报文事件
        /// </summary>
        public event EventHandler<TcpDatagramReceivedEventArgs<byte[]>> DatagramReceived;
        /// <summary>
        /// 接收到数据报文明文事件
        /// </summary>
        public event EventHandler<TcpDatagramReceivedEventArgs<string>> PlaintextReceived;

        private void RaisePlaintextReceived(TcpClient tcpClient, byte[] receivedBytes)
        {
            var str = this.Encoding.GetString(receivedBytes, 0, receivedBytes.Length);
            PlaintextReceived?.Invoke(this, new TcpDatagramReceivedEventArgs<string>(tcpClient,
                this.Encoding.GetString(receivedBytes, 0, receivedBytes.Length)));
        }

        private void RaiseDatagramReceived(TcpClient tcpClient, byte[] receivedBytes)
        {
            DatagramReceived?.Invoke(this, new TcpDatagramReceivedEventArgs<byte[]>(tcpClient, receivedBytes));
        }

        /// <summary>
        /// 与客户端的连接建立事件
        /// </summary>
        public event EventHandler<TcpClientConnectedEventArgs> ClientConnected;

        public event EventHandler<TcpClientDisConnectedEventArgs> ClientDisconnected;
        private void RaiseClientDisconnected(TcpClient tcpClient)
        {
            ClientDisconnected?.Invoke(this, new TcpClientDisConnectedEventArgs(tcpClient));
        }

        private void RaiseClientConnected(TcpClientState tcpClientState)
        {
            ClientConnected?.Invoke(this, new TcpClientConnectedEventArgs(tcpClientState.TcpClient));
        }
        #endregion

        #region Send
        private void GuardRunning()
        {
            if (!IsRunning)
                throw new InvalidProgramException("This TCP server has not been started yet.");
        }
        /// <summary>
        /// 发送报文至客户端
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="datagram"></param>
        public void Send(TcpClient tcpClient, byte[] datagram)
        {
            GuardRunning();
            if (tcpClient == null)
            {
                throw new ArgumentNullException("tcpClient");
            }
            if (datagram == null)
                throw new ArgumentNullException("datagram");

            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                if (networkStream.CanWrite)
                {
                    networkStream.BeginWrite(
                            datagram,
                            0,
                            datagram.Length,
                            HandleDatagramWritten,
                            tcpClient);
                }
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Error("", ex);
            }
        }
        /// <summary>
        /// 发送报文至客户端
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="datagram"></param>
        public void Send(TcpClient tcpClient, string datagram)
        {
            Send(tcpClient, this.Encoding.GetBytes(datagram));
        }
        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram"></param>
        public void SendToAll(byte[] datagram)
        {
            GuardRunning();
            foreach (var item in this._clients.Values)
            {
                Send(item.TcpClient, datagram);
            }
        }
        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram"></param>
        public void SendToAll(string datagram)
        {
            SendToAll(this.Encoding.GetBytes(datagram));
        }

        private void HandleDatagramWritten(IAsyncResult ar)
        {
            try
            {
                TcpClient tcpClient = (TcpClient)ar.AsyncState;
                tcpClient.GetStream().EndWrite(ar);
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Error("", ex);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error("", ex);
            }
            catch (IOException ex)
            {
                Logger.Error("", ex);
            }
        }

        /// <summary>
        /// 发送报文至指定的客户端
        /// </summary>
        /// <param name="tcpClient">客户端</param>
        /// <param name="datagram">报文</param>
        public void SyncSend(TcpClient tcpClient, byte[] datagram)
        {
            GuardRunning();

            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");

            if (datagram == null)
                throw new ArgumentNullException("datagram");

            try
            {
                NetworkStream stream = tcpClient.GetStream();
                if (stream.CanWrite)
                {
                    stream.Write(datagram, 0, datagram.Length);
                }
            }
            catch (ObjectDisposedException ex)
            {
                Logger.Error("", ex);
            }
        }

        /// <summary>
        /// 发送报文至指定的客户端
        /// </summary>
        /// <param name="tcpClient">客户端</param>
        /// <param name="datagram">报文</param>
        public void SyncSend(TcpClient tcpClient, string datagram)
        {
            SyncSend(tcpClient, this.Encoding.GetBytes(datagram));
        }

        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SyncSendToAll(byte[] datagram)
        {
            GuardRunning();

            foreach (var client in _clients.Values)
            {
                SyncSend(client.TcpClient, datagram);
            }
        }
        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="arraySegments"></param>
        public void SyncSendToAll(List<ArraySegment<byte>> arraySegments)
        {
            var buffer = getBuffer(arraySegments);
            SyncSendToAll(buffer);
        }

        byte[] getBuffer(List<ArraySegment<byte>> bufferList)
        {
            List<byte> arraySegment = new List<byte>(bufferList[0].Array);
            for (int index = 1; index < bufferList.Count; index++)
            {
                arraySegment.AddRange(bufferList[index].Array);
            }
            return arraySegment.ToArray();
        }
        /// <summary>
        /// 发送报文至所有客户端
        /// </summary>
        /// <param name="datagram">报文</param>
        public void SyncSendToAll(string datagram)
        {
            GuardRunning();

            SyncSendToAll(this.Encoding.GetBytes(datagram));
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();
                        if (_listener != null)
                            _listener = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("", ex);
                    }
                }
                this._disposed = true;
            }
        }
    }
}
