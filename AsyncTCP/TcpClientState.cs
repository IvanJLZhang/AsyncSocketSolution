#region 文件说明
/*------------------------------------------------------------------------------
// Copyright © 2018 Granda. All Rights Reserved.
// 苏州广林达电子科技有限公司 版权所有
//------------------------------------------------------------------------------
// File Name: TcpClientState
// Author: Ivan JL Zhang    Date: 2018/4/9 10:35:29    Version: 1.0.0
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
using System.Net.Sockets;
using System.Text;

namespace AsyncTCP
{
    class TcpClientState
    {
        public byte[] Buffer { get; set; }
        public TcpClient TcpClient { get; set; }
        public NetworkStream NetworkStream { get; set; }
        public TcpClientState(TcpClient tcpClient, byte[] buffer)
        {
            this.TcpClient = tcpClient;
            this.Buffer = buffer;
            this.NetworkStream = tcpClient.GetStream();
        }
    }
}
