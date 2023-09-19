using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SendMailSimple
{
    public partial class frm_smtp : Form
    {
        public frm_smtp()
        {
            InitializeComponent();
        }

        private TcpClient Server;
        private NetworkStream StrmWtr;
        private StreamReader streamReader;
        private String cmdData;
        private byte[] szData;
        private const String CRLF = "\r\n";


        /// <summary>
        /// 显示状态消息
        /// </summary>
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

        /// <summary>
        /// 发送指令
        /// </summary>
        void SendCommandToServer(string command)
        {
            byte[] byteCommand = System.Text.Encoding.ASCII.GetBytes(command.ToCharArray());
            StrmWtr.Write(byteCommand, 0, byteCommand.Length);
            StrmWtr.Flush();
            showStatusMessage();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        void Connect(string smtpHostIP, string senderName, string senderPwd)
        {

            Cursor cr = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;


            Server = new TcpClient(smtpHostIP, 25);
            listBox1.Items.Clear();

            showStatusMessage("start connecting");

            try
            {
                StrmWtr = Server.GetStream();
                streamReader = new StreamReader(Server.GetStream());
                showStatusMessage();

                //Login
                SendCommandToServer("HELO " + smtpHostIP + CRLF);

                SendCommandToServer("AUTH LOGIN" + CRLF);

                SendCommandToServer(Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(senderName)) + CRLF);

                SendCommandToServer(Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(senderPwd)) + CRLF);


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

        void SendMail(string senderName,string receiverName,string mailSubject,string mailContent)
        {

            try
            {
                showStatusMessage("start sending");

                SendCommandToServer("MAIL FROM: <" + senderName + ">" + CRLF);

                SendCommandToServer("RCPT TO: <" + receiverName + ">" + CRLF);

                SendCommandToServer("DATA" + CRLF);

                SendCommandToServer(@"from: " + senderName + CRLF
                            + "to: " + receiverName + CRLF
                            + "subject: " + mailSubject + CRLF + CRLF
                            + mailContent + CRLF + "." + CRLF);

                showStatusMessage("send complete");
            }
            catch (InvalidOperationException err)
            {
                listBox1.Items.Add("ERROR: " + err.ToString());
            }


        }

        private void button1_Click(object sender, EventArgs e)
        {

            string smtpHostIP = textBox1.Text;

            //发件人帐号
            string senderName = textBox3.Text;
            //发件人密码
            string senderPwd = textBox4.Text;

            string receiverName = textBox5.Text;

            //邮件标题
            string mailSubject = textBox6.Text;

            //邮件内容
            string mailContent = textBox7.Text;

            if(string.IsNullOrEmpty(smtpHostIP) ||
                string.IsNullOrEmpty(senderName) ||
                string.IsNullOrEmpty(senderPwd) ||
                string.IsNullOrEmpty(receiverName) ||
                string.IsNullOrEmpty(mailSubject) ||
                string.IsNullOrEmpty(mailContent))
            {
                MessageBox.Show("请填写完整的信息");
                return;
            }

            Connect(smtpHostIP, senderName, senderPwd);

            SendMail(senderName, receiverName, mailSubject, mailContent);

            DisConnect();

            MessageBox.Show("发送邮件成功");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = "smtp.tom.com";
            textBox2.Text = "25";

            textBox3.Text = "jackson_cq@tom.com";
            textBox4.Text = "Jia@1234";

            textBox5.Text = "371647186@qq.com";

        }
    }
}
