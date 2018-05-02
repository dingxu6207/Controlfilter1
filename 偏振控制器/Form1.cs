using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Net.Sockets;
using System.Net;  // IP，IPAddress, IPEndPoint，端口等；
using System.Threading;
using System.IO;
using System.Timers;


namespace 偏振控制器
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.lbOnline.Items.Clear();

            TextBox.CheckForIllegalCrossThreadCalls = false; 
            this.txtIp.Text = GetIpAddress();
            this.txtPort.Text = "8080";
        }

        private string GetIpAddress()
        {       
            string AddressIP = string.Empty;
            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    AddressIP = _IPAddress.ToString();
                }
            }

            return AddressIP;


        }

        Thread threadWatch = null; // 负责监听客户端连接请求的 线程；
        Socket socketWatch = null;

        Dictionary<string, Socket> dict = new Dictionary<string, Socket>();
        Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();

        private void btnStartListen_Click(object sender, EventArgs e)
        {
            // 创建负责监听的套接字，注意其中的参数；
            socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // 获得文本框中的IP对象；
            IPAddress address = IPAddress.Parse(txtIp.Text.Trim());
            // 创建包含ip和端口号的网络节点对象；
            IPEndPoint endPoint = new IPEndPoint(address, int.Parse(txtPort.Text.Trim()));

            try
            {
                // 将负责监听的套接字绑定到唯一的ip和端口上；
                socketWatch.Bind(endPoint);
            }
            catch (SocketException se)
            {
                MessageBox.Show("异常：" + se.Message);
                return;
            }

            // 设置监听队列的长度；
            socketWatch.Listen(10);
            // 创建负责监听的线程；
            threadWatch = new Thread(WatchConnecting);
            threadWatch.IsBackground = true;
            threadWatch.Start();
            ShowMsg("服务器启动监听成功！");

        }


        /// <summary>
        /// 监听客户端请求的方法；
        /// </summary>
        void WatchConnecting()
        {
            while (true)  // 持续不断的监听客户端的连接请求；
            {
                // 开始监听客户端连接请求，Accept方法会阻断当前的线程；
                Socket sokConnection = socketWatch.Accept(); // 一旦监听到一个客户端的请求，就返回一个与该客户端通信的 套接字；
                // 想列表控件中添加客户端的IP信息；
                lbOnline.Items.Add(sokConnection.RemoteEndPoint.ToString());
                // 将与客户端连接的 套接字 对象添加到集合中；
                dict.Add(sokConnection.RemoteEndPoint.ToString(), sokConnection);
                ShowMsg("客户端连接成功！");
                Thread thr = new Thread(RecMsg);
                thr.IsBackground = true;
                thr.Start(sokConnection);
                dictThread.Add(sokConnection.RemoteEndPoint.ToString(), thr);  //  将新建的线程 添加 到线程的集合中去。
            }
        }

        void RecMsg(object sokConnectionparn)
        {
            Socket sokClient = sokConnectionparn as Socket;
            while (true)
            {
                // 定义一个2M的缓存区；
                byte[] arrMsgRec = new byte[1024 * 1024 * 2];
                // 将接受到的数据存入到输入  arrMsgRec中；
                int length = -1;
                try
                {
                    length = sokClient.Receive(arrMsgRec); // 接收数据，并返回数据的长度；
                }
                catch (SocketException se)
                {
                    ShowMsg("异常：" + se.Message);
                    // 从 通信套接字 集合中删除被中断连接的通信套接字；
                    dict.Remove(sokClient.RemoteEndPoint.ToString());
                    // 从通信线程集合中删除被中断连接的通信线程对象；
                    dictThread.Remove(sokClient.RemoteEndPoint.ToString());
                    // 从列表中移除被中断的连接IP
                    lbOnline.Items.Remove(sokClient.RemoteEndPoint.ToString());
                    break;
                }
                catch (Exception e)
                {
                    ShowMsg("异常：" + e.Message);
                    // 从 通信套接字 集合中删除被中断连接的通信套接字；
                    dict.Remove(sokClient.RemoteEndPoint.ToString());
                    // 从通信线程集合中删除被中断连接的通信线程对象；
                    dictThread.Remove(sokClient.RemoteEndPoint.ToString());
                    // 从列表中移除被中断的连接IP
                    lbOnline.Items.Remove(sokClient.RemoteEndPoint.ToString());
                    break;
                }
             
               // if (arrMsgRec[0] == ':')  // 表示接收到的是数据；
               // {
                    string strMsg = System.Text.Encoding.UTF8.GetString(arrMsgRec, 0, length);// 将接受到的字节数据转化成字符串；
                    ShowMsg(strMsg);
                //}

                if (arrMsgRec[2] == 'S')
                    this.textBoxCount.Text = strMsg.Substring(3, length - 4);
                  

                if (arrMsgRec[2] == 'V')
                    this.textBoxSpeed.Text = strMsg.Substring(3, length - 4);


                this.label3.Text = this.textBoxCount.Text + "/720";

            }            
        }
        void SendComd(string CmdMsg)
        {

            byte[] arrMsg = System.Text.Encoding.UTF8.GetBytes(CmdMsg); // 将要发送的字符串转换成Utf-8字节数组；
            byte[] arrSendMsg = new byte[arrMsg.Length];
            
            Buffer.BlockCopy(arrMsg, 0, arrSendMsg, 0, arrMsg.Length);
            string strKey = "";
            strKey = lbOnline.Text.Trim();
            if (string.IsNullOrEmpty(strKey))   // 判断是不是选择了发送的对象；
            {
                MessageBox.Show("请选择你要发送的好友！！！");
            }
            else
            {
                dict[strKey].Send(arrSendMsg);// 解决了 sokConnection是局部变量，不能再本函数中引用的问题；
                ShowMsg(CmdMsg);
            }
        
        }
        
        private void right_Click(object sender, EventArgs e)
        {
            
            string strMsg = ":F+" + textBoxStepR.Text.ToString() + "#";
            SendComd(strMsg);
        }

        private void left_Click(object sender, EventArgs e)
        {
            
            string strMsg = ":F-" + textBoxStepL.Text.ToString() + "#";
            SendComd(strMsg);

        }

        private void textBoxIP_TextChanged(object sender, EventArgs e)
        {

        }

        void ShowMsg(string str)
        {
            txtMsg.AppendText(str + "\r\n");
        }

        private void textBox1Speed_TextChanged(object sender, EventArgs e)
        {
                        
            string strGetSpeed = this.textBox1Speed.Text.ToString();           
            string strMsg = ":FV" + strGetSpeed + "#";
            SendComd(strMsg);
            
        }

        private void rtbAccept_TextChanged(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.label7.Text = DateTime.Now.ToLongTimeString().ToString(); 
        }

    }
}
