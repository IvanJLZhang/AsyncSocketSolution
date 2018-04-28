using System;
using System.Net.Sockets;

namespace AsyncTCP
{
    public class TcpDatagramReceivedEventArgs<T> : EventArgs
    {
        public TcpClient tcpClient { get; private set; }
        public byte[] ReceivedBytes { get => receivedBytes; private set => receivedBytes = value; }
        public string ReceivedPlaintext { get => receivedPlaintext; private set => receivedPlaintext = value; }
        public int ReceivedCount { get => this.receivedBytes.Length; }
        private byte[] receivedBytes;
        private string receivedPlaintext;

        public TcpDatagramReceivedEventArgs(TcpClient tcpClient, byte[] receivedBytes)
        {
            this.tcpClient = tcpClient;
            this.receivedBytes = receivedBytes;
        }

        public TcpDatagramReceivedEventArgs(TcpClient tcpClient, string v)
        {
            this.tcpClient = tcpClient;
            this.receivedPlaintext = v;
        }
    }
}