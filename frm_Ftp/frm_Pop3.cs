using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Windows.Forms;

namespace SendMailSimple
{
    public partial class frm_Pop3 : Form
    {
        public frm_Pop3()
        {
            InitializeComponent();
        }

        private TcpClient Server;
        private NetworkStream StrmWtr;
        private StreamReader streamReader;
        private String cmdData;
        private byte[] szData;
        private const String CRLF = "\r\n";


        private void frm_Pop3_Load(object sender, EventArgs e)
        {
            textBox1.Text = "pop.tom.com";
            textBox2.Text = "110";


            button1.Enabled = false;
            button2.Enabled = false;
            listBox2.SelectedIndexChanged += ListBox2_SelectedIndexChanged;
        }

        private void ListBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int curIndex = listBox2.SelectedIndex;
            curIndex++;

 
            showStatusMessage($"read No.{curIndex} letter");
            string msg = SendCommandToServer($"RETR {curIndex}" + CRLF);

            textBox7.Text = "";

            while (msg != ".")
            {
                textBox7.AppendText(msg);
                msg = streamReader.ReadLine();
            }

        }

        string showStatusMessage()
        {
            string ret = streamReader.ReadLine();
            listBox1.Items.Add(ret);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
            return ret;
        }

        string showStatusMessage(string ret)
        {
            listBox1.Items.Add(ret);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
            return ret;
        }

        string SendCommandToServer(string command)
        {
            byte[] byteCommand = System.Text.Encoding.ASCII.GetBytes(command.ToCharArray());
            StrmWtr.Write(byteCommand, 0, byteCommand.Length);
            StrmWtr.Flush();
            return showStatusMessage();
        }

        void Connect()
        {
            string popHostIP = textBox1.Text;

            string receiverName = textBox3.Text;

            string receiverrPwd = textBox4.Text;



            if (string.IsNullOrEmpty(popHostIP) ||
                string.IsNullOrEmpty(receiverName) ||
                string.IsNullOrEmpty(receiverrPwd))
            {
                MessageBox.Show("请填写完整的信息");
                return;
            }

            Cursor cr = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;


            Server = new TcpClient(popHostIP, 110);
            listBox1.Items.Clear();

            showStatusMessage("start connecting");

            try
            {
                StrmWtr = Server.GetStream();
                streamReader = new StreamReader(Server.GetStream());
                showStatusMessage();

                //Login
                SendCommandToServer("USER " + receiverName + CRLF);

                SendCommandToServer("PASS " + receiverrPwd + CRLF);

                SendCommandToServer("STAT " + CRLF);


                showStatusMessage("connecting success");
            }
            catch (InvalidOperationException err)
            {
                listBox1.Items.Add("ERROR: " + err.ToString());
            }
            finally
            {
                Cursor.Current = cr;
            }

        }

        void DisConnect()
        {
            Cursor cr = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;


            //Logout
            SendCommandToServer("QUIT" + CRLF);


            StrmWtr.Close();
            streamReader.Close();

            showStatusMessage("connect closed");
            Cursor.Current = cr;
        }


        void ReceiveMail()
        {

            try
            {
                showStatusMessage("start receiveing");

                listBox2.Items.Clear();

                string msg = SendCommandToServer("LIST" + CRLF);

                string[] splitString = msg.Split(' ');

                //从字符串中取子串获取邮件总数
                int count = int.Parse(splitString[1]);

                //判断邮箱中是否有邮件            
                if (count > 0)
                {
                    

                    //向邮件列表框中添加邮件
                    for (int i = 0; i < count; i++)
                    {
                        if ((msg = showStatusMessage()) == null)
                            return;

                        splitString = msg.Split(' ');
                        listBox2.Items.Add($"第 {splitString[0]} 封邮件 [{splitString[1]}b] ");

                    }


                    //读出结束符
                    if ((msg = showStatusMessage()) == null)
                        return;
                }


                showStatusMessage("receive complete");
            }
            catch (InvalidOperationException err)
            {
                listBox1.Items.Add("ERROR: " + err.ToString());
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Server == null) {
                MessageBox.Show("请连接服务器");    
                return; 
            }
            ReceiveMail();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (Server == null)
            {
                MessageBox.Show("请连接服务器");
                return;
            }

            int curIndex = listBox2.SelectedIndex;
            curIndex++;

            SendCommandToServer($"DELE {curIndex}" + CRLF);

            textBox7.Text = "";

            ReceiveMail();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (button3.Text == "连接")
            {
                Connect();

                button3.Text = "断开";
                button1.Enabled = true;
                button2.Enabled = true;
            }
            else
            {
                DisConnect();
                button3.Text = "连接";
                button1.Enabled = false;
                button2.Enabled = false;
            }
        }
    }
}
