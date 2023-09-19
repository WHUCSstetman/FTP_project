using System;
using System.Net.Sockets;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Collections;
using System.Data.SqlClient;
using System.Net;
using System.Collections.Generic;
using System.ComponentModel;


namespace SendMailSimple
{
    public partial class frm_Ftp : Form
    {
        public frm_Ftp()
        {
            InitializeComponent();
        }

        private TcpClient cmdServer;
        private TcpClient dataServer;
        private NetworkStream cmdStrmWtr;
        private StreamReader cmdStrmRdr;
        private NetworkStream dataStrmWtr;
        private StreamReader dataStrmRdr;
        private String cmdData;
        private byte[] szData;
        private const String CRLF = "\r\n";
        private enum DownloadOrUpload { Download=0, Upload=1}


        // 连接到 SQL Server 数据库
        private SqlConnection CreateSqlConnection()
        {
            string connectionString = "Data Source=DESKTOP-0DRDAUB;Initial Catalog=ftpconnect;Integrated Security=SSPI;";
            return new SqlConnection(connectionString);
        }

        // 创建保存下载进度的数据库表
        private void CreateProgressTable()
        {
            using (SqlConnection connection = CreateSqlConnection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DownloadOrUploadProgress'";
                    object result = command.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        command.CommandText = "CREATE TABLE DownloadOrUploadProgress (FileName NVARCHAR(255) PRIMARY KEY, DownloadPosition BIGINT, UploadPosition BIGINT)";
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        // 保存下载进度到数据库
        private void SaveDownloadOrUploadPositionToDB(string fileName, long position, DownloadOrUpload downloadOrUpload)
        {
            using (SqlConnection connection = CreateSqlConnection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = "MERGE INTO DownloadOrUploadProgress AS target " +
                        "USING(VALUES(@FileName, @DownloadPosition, @UploadPosition)) AS source(FileName, DownloadPosition, UploadPosition) " +
                        "ON target.FileName = source.FileName " +
                        "WHEN MATCHED THEN UPDATE SET target.DownloadPosition = source.DownloadPosition, target.UploadPosition = source.UploadPosition " +
                        "WHEN NOT MATCHED THEN INSERT(FileName, DownloadPosition, UploadPosition) VALUES(source.FileName, source.DownloadPosition, source.UploadPosition);"; 

                    // 添加命名参数并为其提供值
                    command.Parameters.AddWithValue("@FileName", fileName);
                    if (downloadOrUpload == DownloadOrUpload.Download)
                    {
                        command.Parameters.AddWithValue("@DownloadPosition", position);
                        command.Parameters.AddWithValue("@UploadPosition", 0); // 假设上传位置为0
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@DownloadPosition", 0); // 假设下载位置为0
                        command.Parameters.AddWithValue("@UploadPosition", position);
                    }

                    command.ExecuteNonQuery();
                }
            }
        }



        // 从数据库中读取下载进度
        private long ReadDownloadOrUploadPositionFromDB(string fileName, DownloadOrUpload downloadOrUpload)
        {
            using (SqlConnection connection = CreateSqlConnection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    object result;
                    if (downloadOrUpload == DownloadOrUpload.Download)
                    {
                        command.CommandText = "SELECT DownloadPosition FROM DownloadOrUploadProgress WHERE FileName = @FileName";
                        command.Parameters.AddWithValue("@FileName", fileName);
                        result = command.ExecuteScalar();
                    }
                    else
                    {
                        command.CommandText = "SELECT UploadPosition FROM DownloadOrUploadProgress WHERE FileName = @FileName";
                        command.Parameters.AddWithValue("@FileName", fileName);
                        result = command.ExecuteScalar();
                    }
                    if (result != null && long.TryParse(result.ToString(), out long position))
                    {
                        return position;
                    }
                }
            }
            return 0;
        }

    private void frm_Ftp_Load(object sender, EventArgs e)
        {

            textBox5.Text = "C:\\";
            freshFileBox_Left();
        }

        /// <summary>
        /// 进入被动模式，并初始化数据端口的输入输出流
        /// </summary>
        private void openDataPort()
        {
            string retstr;
            string[] retArray;
            int dataPort;



            // Start Passive Mode 

        
            string command = "PASV" + CRLF;
            byte[] byteCommand = System.Text.Encoding.ASCII.GetBytes(command.ToCharArray());
            cmdStrmWtr.Write(byteCommand, 0, byteCommand.Length);
            cmdStrmWtr.Flush();
            do
            {
                retstr = showStatusMessage();
             
            } while (retstr.IndexOf("Entering Passive Mode")<0);

            // Calculate data's port
            retArray = Regex.Split(retstr, ",");

            showStatusMessage(retstr);
            if (retArray[5][2] != ')') retstr = retArray[5].Substring(0, 3);
            else retstr = retArray[5].Substring(0, 2);

            dataPort = Convert.ToInt32(retArray[4]) * 256 + Convert.ToInt32(retstr);
            showStatusMessage("Get dataPort=" + dataPort);

            //Connect to the dataPort
            dataServer = new TcpClient(textBox1.Text, dataPort);
            dataStrmRdr = new StreamReader(dataServer.GetStream());
            dataStrmWtr = dataServer.GetStream();
        }

        /// <summary>
        /// 断开数据端口的连接
        /// </summary>
        private void closeDataPort()
        {
            dataStrmRdr.Close();
            dataStrmWtr.Close();
            showStatusMessage();

            SendCommandToServer("ABOR" + CRLF);             
        }

        /// <summary>
        /// 获得/刷新 右侧的服务器文件列表
        /// </summary>
        private void freshFileBox_Right()
        {

            openDataPort();

            string absFilePath;

            //List
            SendCommandToServer("LIST" + CRLF);
            
            listBox3.Items.Clear();

            while ((absFilePath = dataStrmRdr.ReadLine()) != null)
            {
                string[] temp = Regex.Split(absFilePath, " ");
                listBox3.Items.Add($"{temp[9]}{temp[temp.Length - 1]}" );
            }

            closeDataPort();
        }

        /// <summary>
        /// 获得/刷新 左侧的本地文件列表
        /// </summary>
        private void freshFileBox_Left()
        {
            listBox2.Items.Clear();
            if (textBox5.Text == "") return;

            var files = Directory.GetFiles(textBox5.Text, "*.*");
            foreach (var file in files)
            {
                Console.WriteLine(file);
                string[] temp = Regex.Split(file, @"\\");
                listBox2.Items.Add(temp[temp.Length - 1]);
            }
        }


        string showStatusMessage()
        {
            string ret = cmdStrmRdr.ReadLine();
           
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
            cmdStrmWtr.Write(byteCommand, 0, byteCommand.Length);
            cmdStrmWtr.Flush();
            return showStatusMessage();
        }

        void Connect()
        {
            string serverAddress = textBox1.Text;

            string userName = textBox3.Text;

            string userPwd = textBox4.Text;

            cmdServer = new TcpClient(serverAddress, 21);
            listBox1.Items.Clear();

            try
            {
                cmdStrmRdr = new StreamReader(cmdServer.GetStream());
                cmdStrmWtr = cmdServer.GetStream();

                showStatusMessage("start connecting");

                string retstr;

                //Login
                SendCommandToServer("USER " + userName + CRLF);

                retstr = SendCommandToServer("PASS " + userPwd + CRLF);

                retstr = retstr.Substring(0, 3);

                if (Convert.ToInt32(retstr) == 530) throw new InvalidOperationException("帐号密码错误");

            }
            catch (InvalidOperationException err)
            {
                listBox1.Items.Add("ERROR: " + err.Message.ToString());
            }

        }

        void DisConnect()
        {
            
            SendCommandToServer("QUIT" + CRLF);

            cmdStrmWtr.Close();
            cmdStrmRdr.Close();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                path = fbd.SelectedPath;
                listBox2.Items.Add("选中本地路径:" + path);
            }

            textBox5.Text = path;
            freshFileBox_Left();

        }
        // 记录当前上传或下载的起始位置
        private long transferStartPosition;

        /// <summary>
        /// 上传
        /// </summary>
        private void button3_Click(object sender, EventArgs e)
        {
            if (textBox5.Text == "" || listBox2.SelectedIndex < 0)
            {
                MessageBox.Show("请选择上传的文件", "ERROR");
                return;
            }

            string fileName = listBox2.Items[listBox2.SelectedIndex].ToString();
            string filePath = Path.Combine(textBox5.Text, fileName);

            this.openDataPort();

            using (SqlConnection connection = CreateSqlConnection())
            {
                connection.Open();
                CreateProgressTable();
                transferStartPosition = ReadDownloadOrUploadPositionFromDB(fileName, DownloadOrUpload.Upload);
                // 使用REST命令设置上传起始位置（在续传时，可以是上次传输的已上传大小）
                SendCommandToServer("REST " + transferStartPosition + CRLF);
                SendCommandToServer("STOR " + fileName + CRLF);

                using (FileStream fstrm = new FileStream(filePath, FileMode.Open))
                {
                    byte[] fbytes = new byte[1030];
                    int cnt = 0;
                    while ((cnt = fstrm.Read(fbytes, 0, 1024)) > 0)
                    {
                        dataStrmWtr.Write(fbytes, 0, cnt);
                        transferStartPosition += cnt; // 更新上传起始位置
                        SaveDownloadOrUploadPositionToDB(fileName, transferStartPosition, DownloadOrUpload.Upload);
                    }
                    SaveDownloadOrUploadPositionToDB(fileName, 0, DownloadOrUpload.Upload);
                }
            }

            this.closeDataPort();

            this.freshFileBox_Right();
        }


        /// <summary>
        /// 下载
        /// </summary>
        private void button4_Click(object sender, EventArgs e)
        {
            if (textBox5.Text != "" && listBox2.SelectedIndex >= 0)
            {
                string fileName = listBox3.Items[listBox3.SelectedIndex].ToString();
                string filePath = Path.Combine(textBox5.Text, fileName);

                this.openDataPort();

                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();
                    CreateProgressTable();
                    transferStartPosition = ReadDownloadOrUploadPositionFromDB(fileName, DownloadOrUpload.Download);
                    // 使用REST命令设置下载起始位置（在续传时，可以是上次传输的已下载大小）
                    SendCommandToServer("REST " + transferStartPosition + CRLF);
                    SendCommandToServer("RETR " + fileName + CRLF);

                    using (FileStream fstrm = new FileStream(filePath, FileMode.OpenOrCreate))
                    {
                        byte[] fbytes = new byte[1030];
                        int cnt = 0;
                        while ((cnt = dataStrmWtr.Read(fbytes, 0, 1024)) > 0)
                        {
                            fstrm.Write(fbytes, 0, cnt);
                            transferStartPosition += cnt; // 更新下载起始位置
                            SaveDownloadOrUploadPositionToDB(fileName, transferStartPosition, DownloadOrUpload.Download);
                        }
                        SaveDownloadOrUploadPositionToDB(fileName, 0, DownloadOrUpload.Download);
                    }
                }

                this.closeDataPort();

                this.freshFileBox_Left();
            }
            else
            {
                MessageBox.Show("请选择目标文件和下载路径", "ERROR");
                return;
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {
            if (button2.Text == "连接")
            {
                Connect();
                freshFileBox_Right();
                button2.Text = "断开";
     
            }
            else
            {
                DisConnect();
                button2.Text = "连接";
           
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (listBox3.SelectedIndex < 0)
            {
                MessageBox.Show("请选择要删除的文件", "ERROR");
                return;
            }

            string fileName = listBox3.Items[listBox3.SelectedIndex].ToString();

            // 启动 BackgroundWorker 来执行删除操作
            var deleteWorker = new BackgroundWorker();
            deleteWorker.DoWork += DeleteFileAsync;
            deleteWorker.RunWorkerCompleted += DeleteWorker_RunWorkerCompleted;

            // 显示删除操作进行中的提示
            MessageBox.Show("正在删除文件，请稍候...", "提示");

            // 开始异步执行删除操作，并传递要删除的文件名作为参数
            deleteWorker.RunWorkerAsync(fileName);
        }

        private void DeleteFileAsync(object sender, DoWorkEventArgs e)
        {
            string fileName = e.Argument.ToString();

            // 使用 Control.Invoke 在主线程上执行操作
            this.Invoke((MethodInvoker)delegate
            {
                // 向服务器发送DELE命令以删除文件
                SendCommandToServer("DELE " + fileName + CRLF);
            });

            e.Result = true; // 删除成功
        }


        private void DeleteWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // 异步操作完成后的处理
            if ((bool)e.Result)
            {
                // 删除操作成功
                MessageBox.Show("文件删除成功！", "Success");

                // 从右侧的文件列表中移除已删除的文件项
                if (listBox3.SelectedIndex >= 0)
                {
                    listBox3.Items.RemoveAt(listBox3.SelectedIndex);
                }

                // 将服务器的响应添加到 listBox1 中
                if (e.Result != null && e.Result is string response)
                {
                    listBox1.Items.Add(response);
                }
            }
            else
            {
                // 删除操作失败
                MessageBox.Show("删除文件失败！", "Error");
            }
        }
    }
}
