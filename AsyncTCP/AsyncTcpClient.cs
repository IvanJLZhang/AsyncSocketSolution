#region 文件说明
/*------------------------------------------------------------------------------
// Copyright © 2018 Granda. All Rights Reserved.
// 苏州广林达电子科技有限公司 版权所有
//------------------------------------------------------------------------------
// File Name: AsyncTcpClient
// Author: Ivan JL Zhang    Date: 2018/4/9 11:35:06    Version: 1.0.0
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
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AsyncTCP
{//: IDisposable
    public class AsyncTcpClient 
    {
        private TcpClient _tcpClient;
        private bool _disposed = false;
        private int _retries = 0;

        #region 构造方法
        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteEP">远端服务器终结点</param>
        public AsyncTcpClient(IPEndPoint remoteEP) : this(new[] { remoteEP.Address }, remoteEP.Port)
        {

        }
        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAdress">远端服务器IP地址</param>
        /// <param name="remotePort">远端服务器端口</param>
        public AsyncTcpClient(IPAddress remoteIPAdress, int remotePort)
            : this(new[] { remoteIPAdress }, remotePort)
        { }
        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAdress">远端服务器IP地址</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(IPAddress remoteIPAdress, int remotePort, IPEndPoint localEP)
            : this(new[] { remoteIPAdress }, remotePort, localEP) { }
        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteHostName">远端服务器主机名</param>
        /// <param name="remotePort">远端服务器端口</param>
        public AsyncTcpClient(string remoteHostName, int remotePort)
            : this(Dns.GetHostAddresses(remoteHostName), remotePort) { }
        /// <summary>
        /// 步TCP客户端
        /// </summary>
        /// <param name="remoteHostName">远端服务器主机名</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(string remoteHostName, int remotePort, IPEndPoint localEP)
            : this(Dns.GetHostAddresses(remoteHostName), remotePort, localEP) { }
        /// <summary>
        /// 步TCP客户端
        /// </summary>
        /// <param name="remoteIPAdresses">远端服务器IP地址列表</param>
        /// <param name="remotePort">远端服务器端口</param>
        public AsyncTcpClient(IPAddress[] remoteIPAdresses, int remotePort)
            : this(remoteIPAdresses, remotePort, null)
        {

        }
        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIpAddresses">远端服务器IP地址列表</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(IPAddress[] remoteIpAddresses, int remotePort, IPEndPoint localEP)
        {
            this.Addresses = remoteIpAddresses;
            this.Port = remotePort;
            this.LocalIPEndPoint = localEP;
            this.Encoding = Encoding.ASCII;

            if (this.LocalIPEndPoint != null)
            {
                this._tcpClient = new TcpClient(this.LocalIPEndPoint);
            }
            else
            {
                this._tcpClient = new TcpClient();
            }
            Retries = 3;
            RetryInterval = 5;

        }
        #endregion

        #region properties
        /// <summary>
        /// 远端服务器的IP地址列表
        /// </summary>
        public IPAddress[] Addresses { get; private set; }
        /// <summary>
        /// 远端服务器的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public IPEndPoint LocalIPEndPoint { get; private set; }
        /// <summary>
        /// 连接重试次数
        /// </summary>
        public int Retries { get; private set; }
        /// <summary>
        /// 连接重试间隔
        /// </summary>
        public int RetryInterval { get; set; }
        /// <summary>
        /// 通信所使用的编码
        /// </summary>
        public Encoding Encoding { get; private set; }
        /// <summary>
        /// 是否已经与服务器建立连接
        /// </summary>
        public bool Connected { get; private set; }
        #endregion

        #region Connect
        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <returns></returns>
        public AsyncTcpClient Connect()
        {
            if (!Connected)
            {
                // start the async connect operation
                _tcpClient.BeginConnect(Addresses, Port, HandleTcpServerConnected, _tcpClient);
            }
            return this;
        }
        /// <summary>
        /// 关闭与服务器的连接
        /// </summary>
        /// <returns></returns>
        public AsyncTcpClient Close()
        {
            if (Connected)
            {
                _retries = 0;
                _tcpClient.Close();
                RaiseServerDisconnected(Addresses, Port);
            }
            return this;
        }
        #endregion

        #region Receive
        private void HandleTcpServerConnected(IAsyncResult ar)
        {
            try
            {
                _tcpClient.EndConnect(ar);
                Connected = true;
                RaiseServerConnected(Addresses, Port);
                Logger.Info("Connected.");
                _retries = 0;
            }
            catch (Exception ex)
            {
                Logger.Error("", ex);
                if (_retries > 0)
                {
                    Logger.Info(string.Format(CultureInfo.InvariantCulture, "Connect to server with retry {0} failed", _retries));
                }
                _retries++;
                if (_retries > Retries)
                {
                    // we have failed to connect to all the IP addresses, connection has failed overall.
                    RaiseServerExceptionOccured(Addresses, Port, ex);
                    return;
                }
                else
                {
                    Logger.Info(string.Format(CultureInfo.InvariantCulture, "Waiting {0} seconds before retrying to connect to server.", RetryInterval));
                    Thread.Sleep(TimeSpan.FromSeconds(RetryInterval));
                    Connect();
                    return;
                }
            }

            // we are connected successfully and start async read operation
            byte[] buffer = new byte[_tcpClient.ReceiveBufferSize];
            _tcpClient.GetStream().BeginRead(buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
        }

        private void HandleDatagramReceived(IAsyncResult ar)
        {
            int numberOfReadBytes = 0;
            try
            {
                NetworkStream stream = _tcpClient.GetStream();
                numberOfReadBytes = stream.EndRead(ar);
            }
            catch (Exception)
            {
                numberOfReadBytes = 0;
            }
            if (numberOfReadBytes == 0)
            {
                // connection has been closed
                Close();
                return;
            }

            // received byte and trigger event notification
            byte[] buffer = (byte[])ar.AsyncState;
            byte[] receivedBytes = new byte[numberOfReadBytes];
            Array.Copy(buffer, 0, receivedBytes, 0, numberOfReadBytes);

            RaiseDatagramReceived(_tcpClient, receivedBytes);
            RaisePlaintextReceived(_tcpClient, receivedBytes);

            // then start reading from the network again
            _tcpClient.GetStream().BeginRead(buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
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
            PlaintextReceived?.Invoke(this, new TcpDatagramReceivedEventArgs<string>(tcpClient, this.Encoding.GetString(receivedBytes)));
        }

        private void RaiseDatagramReceived(TcpClient tcpClient, byte[] receivedBytes)
        {
            DatagramReceived?.Invoke(this, new TcpDatagramReceivedEventArgs<byte[]>(tcpClient, receivedBytes));
        }
        /// <summary>
        /// 与服务器的连接已建立事件
        /// </summary>
        public event EventHandler<TcpServerConnectedEventArgs> ServerConnected;
        /// <summary>
        /// 与服务器的连接已断开事件
        /// </summary>
        public event EventHandler<TcpServerDisconnectedEventArgs> ServerDisconnected;
        /// <summary>
        /// 与服务器的连接发生异常事件
        /// </summary>
        public event EventHandler<TcpServerExceptionOccurredEventArgs> ServerExceptionOccurred;

        private void RaiseServerConnected(IPAddress[] addresses, int port)
        {
            ServerConnected?.Invoke(this, new TcpServerConnectedEventArgs(addresses, port));
        }

        private void RaiseServerDisconnected(IPAddress[] addresses, int port)
        {
            ServerDisconnected?.Invoke(this, new TcpServerDisconnectedEventArgs(addresses, port));
        }

        /// <summary>
        /// 与服务器的连接发生异常事件
        /// </summary>
        private void RaiseServerExceptionOccured(IPAddress[] addresses, int port, Exception ex)
        {
            ServerExceptionOccurred(this, new TcpServerExceptionOccurredEventArgs(addresses, port, ex));
        }
        #endregion

        #region Send
        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(byte[] datagram)
        {
            if (datagram == null)
                throw new ArgumentNullException("datagram");

            if (!Connected)
            {
                RaiseServerDisconnected(Addresses, Port);
                throw new InvalidProgramException("This client has not connected to server.");
            }

            _tcpClient.GetStream().BeginWrite(datagram, 0, datagram.Length, HandleDatagramWritten, _tcpClient);
        }

        public void Send(List<ArraySegment<byte>> arraySegments)
        {
            var buffer = getBuffer(arraySegments);
            Send(buffer);
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
        private void HandleDatagramWritten(IAsyncResult ar)
        {
            ((TcpClient)ar.AsyncState).GetStream().EndWrite(ar);
        }

        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(string datagram)
        {
            Send(this.Encoding.GetBytes(datagram));
        }
        #endregion

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Close();

                        if (_tcpClient != null)
                        {
                            _tcpClient = null;
                        }
                    }
                    catch (SocketException ex)
                    {
                        Logger.Error("", ex);
                    }
                }

                _disposed = true;
            }
        }
    }
}
