using System;
using System.Net;

namespace AsyncTCP
{
    public class TcpServerDisconnectedEventArgs : EventArgs
    {
        private IPAddress[] addresses;
        private int port;

        public TcpServerDisconnectedEventArgs(IPAddress[] addresses, int port)
        {
            this.addresses = addresses;
            this.port = port;
        }

        public IPAddress[] Addresses { get => addresses; set => addresses = value; }
        public int Port { get => port; set => port = value; }
    }
}