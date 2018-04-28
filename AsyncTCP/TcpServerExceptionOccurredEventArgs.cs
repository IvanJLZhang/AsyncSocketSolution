using System;
using System.Net;

namespace AsyncTCP
{
    public class TcpServerExceptionOccurredEventArgs : EventArgs
    {
        private IPAddress[] addresses;
        private int port;
        private Exception ex;

        public TcpServerExceptionOccurredEventArgs(IPAddress[] addresses, int port, Exception ex)
        {
            this.addresses = addresses;
            this.port = port;
            this.ex = ex;
        }

        public IPAddress[] Addresses { get => addresses; set => addresses = value; }
        public int Port { get => port; set => port = value; }
        public Exception Ex { get => ex; set => ex = value; }
    }
}