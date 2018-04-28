using System;
using System.Net.Sockets;

namespace AsyncTCP
{
    public class TcpClientConnectedEventArgs : EventArgs
    {
        public TcpClient tcpClient { get; private set; }

        public TcpClientConnectedEventArgs(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
        }
    }
}