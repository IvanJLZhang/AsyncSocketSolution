using System;
using System.Net.Sockets;

namespace AsyncTCP
{
    public class TcpClientDisConnectedEventArgs : EventArgs
    {
        public TcpClient tcpClient { get; private set; }

        public TcpClientDisConnectedEventArgs(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
        }
    }
}