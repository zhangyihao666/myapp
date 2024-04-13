using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Flir.Atlas.Live;
using Flir.Atlas.Live.Device;
using Flir.Atlas.Image.Palettes;
using Flir.Atlas.Live.Discovery;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace A35Demo
{
    public partial class FrmMain : Form
    {
        private PID pidController;
        private static FrmMain currentInstance;
        private Socket server;
        private string host = "192.168.3.230";
        private int port = 10001;
        // private WebBrowserWindow webBrowserWindow;
        private double temperature; // 新增一个字段来存储温度值
        private bool continueheating = true; // 控制是否加热
        private string presentPower = "10";
        private bool FirstConnect = true; //第一次连接
        private bool firstheat = true; //第一次加热
        private object lockObject = new object();

        public FrmMain()
        {
            InitializeComponent();
            currentInstance = this;
            timer1.Interval = 30;
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            pidController = new PID(P: 0.2, I: 0.0, D: 0.0, sampleTime: 1.0); // 根据需要调整参数
            pidController.SetPoint = 0; // 设置设定点（期望值）
        }
        //添加一个公共方法来获取server实例
        public Socket GetServerInstance()
        {
            return server;
        }

        //连接主机
        private async Task ConnectToServerAsync()
        {
            try
            {
                await server.ConnectAsync(host, port);
                Console.WriteLine("成功连接到服务器");

            }
            catch (Exception ex)
            {
                MessageBox.Show("无法连接到服务器: " + ex.Message);
            }
        }
        //发送HTTP请求并等待响应
        private async Task SendHttpRequestAsync()
        {
            string httpRequest = "GET / HTTP/1.1\r\n" +
                         "Host: " + host + "\r\n" +
                         "\r\n";

            byte[] requestBytes = Encoding.ASCII.GetBytes(httpRequest);
            // 使用 SocketAsyncEventArgs 来异步发送数据
            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(requestBytes, 0, requestBytes.Length);

            var sendTaskCompletionSource = new TaskCompletionSource<bool>();
            sendEventArgs.Completed += (sender, e) =>
            {
                if (e.SocketError == SocketError.Success)
                {
                    sendTaskCompletionSource.SetResult(true);
                    Console.WriteLine("成功发送服务器请求");
                }
                else
                {
                    sendTaskCompletionSource.SetException(new SocketException((int)e.SocketError));
                }
            };

            server.SendAsync(sendEventArgs);
            await sendTaskCompletionSource.Task;
        }

        //接收服务器发送的 HTTP 响应内容，并将其作为字符串返回
        private async Task<string> ReceiveHttpResponseAsync()
        {
            byte[] buffer = new byte[1024];
            StringBuilder responseBuilder = new StringBuilder();

            while (true)
            {
                var receiveEventArgs = new SocketAsyncEventArgs();
                receiveEventArgs.SetBuffer(buffer, 0, buffer.Length);

                var receiveTaskCompletionSource = new TaskCompletionSource<int>();
                receiveEventArgs.Completed += (sender, e) =>
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        receiveTaskCompletionSource.SetResult(e.BytesTransferred);
                    }
                    else
                    {
                        receiveTaskCompletionSource.SetException(new SocketException((int)e.SocketError));
                    }
                };

                server.ReceiveAsync(receiveEventArgs); // 这里只需要传递SocketAsyncEventArgs对象
                int bytesRead = await receiveTaskCompletionSource.Task;
                if (bytesRead > 0)
                {
                    responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                    Console.WriteLine("成功返回HTTP响应内容");
                }
                else
                {
                    break;
                }
            }

            return responseBuilder.ToString();
        }


        private async Task CloseConnectionAsync()
        {
            await Task.Delay(1000); // 等待一段时间，确保响应内容接收完整
            server.Close();
            Console.WriteLine("成功关闭socket连接");
        }

        //private class WebBrowserWindow : Form
        //{
        //    private WebBrowser webBrowser;

        //    public WebBrowserWindow()
        //    {
        //        // 初始化WebBrowser控件
        //        webBrowser = new WebBrowser();
        //        webBrowser.Dock = DockStyle.Fill;
        //        this.Controls.Add(webBrowser);

        //        // 设置 WebBrowser 控件的 Dock 属性，使其充满整个窗口
        //        webBrowser.Dock = DockStyle.Fill;

        //        // 等待WebBrowser加载完成
        //        webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;

        //        // 在窗口加载时导航到网页
        //        string url = "http://192.168.3.230"; 
        //        webBrowser.Navigate(url);
        //    }
        //    private void FrmMain_Load(object sender, EventArgs e)
        //    {
        //        if (camera == null)
        //        {
        //            camera = new FlirCamera();
        //            camera.CameraStatusChanged += CameraStatusChangedHandel;
        //        }
        //    }

        //    async private void WebBrowser_DocumentCompleted(object sender, EventArgs e)
        //    {
           
        //        // 等待一段时间，确保页面加载完成
        //        await Task.Delay(5000);

        //    }
        //}

    
        public static FlirCamera camera = null;

        public bool IsConnected
        {
            get
            {
                return camera.ConnectStatus == CameraStatus.Connected?true:false;
            }
        
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            if (camera == null)
            {
                camera = new FlirCamera();
                camera.CameraStatusChanged += CameraStatusChangedHandel;          
            }
           
        }

        // 连接状态通知
       public static void CameraStatusChangedHandel(object sender, CameraStatusArgs e)
        {
            if (currentInstance != null && !currentInstance.IsDisposed && currentInstance.IsHandleCreated)
            {
                currentInstance.BeginInvoke(new Action(() =>
                {
                    currentInstance.lblStatus.Text = "相机状态" + e.Msg;
                }));
            }
        }

        // 控制激光加热
        public static void OpenBeam()
        {
            FrmMain frmMainInstance = currentInstance;
            if (frmMainInstance != null)
            {
                Socket server = frmMainInstance.GetServerInstance();
                if (server != null)
                {
                    byte[] send_data = Encoding.Default.GetBytes("emon\n");
                    int data = server.Send(send_data);
                    
                    // Show status
                    send_data = Encoding.Default.GetBytes("sta\n");
                    server.Send(send_data);

                    byte[] recv_data = new byte[1024];
                    int bytesReceived = server.Receive(recv_data);
                    string response = Encoding.ASCII.GetString(recv_data, 0, bytesReceived);
                    Console.WriteLine("Openbeam Response: " + response);
                }
                else
                {
                    Console.WriteLine("Server instance is null.");
                }
            }
            else
            {
                Console.WriteLine("FrmMain instance is null.");
            }
        }

        // 停止加热
        public static void CloseBeam()
            {
                FrmMain frmMainInstance = currentInstance;
                if (frmMainInstance != null)
                {
                    Socket server = frmMainInstance.GetServerInstance();
                    if (server != null)
                    {
                        byte[] send_data = Encoding.ASCII.GetBytes("emoff\r");
                        server.Send(send_data);

                        // Show status
                        send_data = Encoding.ASCII.GetBytes("sta\r");
                        server.Send(send_data);

                        byte[] recv_data = new byte[1024];
                        int bytesReceived = server.Receive(recv_data);
                        string response = Encoding.ASCII.GetString(recv_data, 0, bytesReceived);
                        Console.WriteLine("Closebeam Response: " + response);
                    }
                    else
                    {
                        Console.WriteLine("Server instance is null.");
                    }
                }
                else
                {
                    Console.WriteLine("FrmMain instance is null.");
                }
            }
        // 控制功率
        public static void SetPower(string power)
            {
                FrmMain frmMainInstance = currentInstance;
                Socket server = frmMainInstance.GetServerInstance();
                // 将power转换为ASCII编码的字节数组
                byte[] powerBytes = Encoding.ASCII.GetBytes(power);

                // 构造发送的数据 SDC+power+/r
                byte[] send_data = Encoding.ASCII.GetBytes("sdc");
                byte[] cr = Encoding.ASCII.GetBytes("\r");
                byte[] sendDataWithCR = new byte[send_data.Length + powerBytes.Length + cr.Length];
                Buffer.BlockCopy(send_data, 0, sendDataWithCR, 0, send_data.Length);
                Buffer.BlockCopy(powerBytes, 0, sendDataWithCR, send_data.Length, powerBytes.Length);
                Buffer.BlockCopy(cr, 0, sendDataWithCR, send_data.Length + powerBytes.Length, cr.Length);

                // 发送数据
                server.Send(sendDataWithCR);

                // 发送STA命令
                byte[] send_sta = Encoding.ASCII.GetBytes("STA\r");
                server.Send(send_sta);

                // 接收响应数据
                byte[] recv_data = new byte[1024];
                int bytesReceived = server.Receive(recv_data);
            }
        // 调整功率
        public async void UpAndKeepTemperature()
        {
            double startTime = DateTime.Now.ToOADate();
            double offset = 5;
            double presentTime;
            double flagTime = startTime;
            double presentTemperature = 0;
            double setTemperature = 0;

            // 初始化
            if (firstheat)
            {
                SetPower("10");
                firstheat = false;
            }

            while (continueheating)
            {
                lock (lockObject)
                {
                    // 获取目标温度，使用lock避免UI线程修改温度导致资源冲突
                    setTemperature = pidController.SetPoint;
                    // 获取当前温度
                    presentTemperature = temperature;
                }
                presentTime = DateTime.Now.ToOADate();
                if (presentTime - flagTime > 0.00005)
                {
                    // 当温度超过设定温度+offset度时，减小功率
                    if (presentTemperature > setTemperature + offset)
                    {
                        double power = double.Parse(presentPower) - 0.1;
                        presentPower = Convert.ToString(power);
                        SetPower(presentPower);
                        await Task.Delay(1000);
                    }
                    // 当温度小于设定温度-offset度时，增加功率
                    else if (presentTemperature < setTemperature - offset && presentTemperature > setTemperature - 3*offset)
                    {
                        double power = double.Parse(presentPower) + 0.1;
                        presentPower = Convert.ToString(power);
                        SetPower(presentPower);
                        await Task.Delay(1000);
                    }
                    // 当温度在设定温度+-offset度时，维持该功率
                    else if (presentTemperature >= setTemperature - offset && presentTemperature <= setTemperature + offset)
                    {
                        SetPower(presentPower);
                        await Task.Delay(10);
                    }
                    else
                    {
                        // 每秒增加1%的功率
                        double power = double.Parse(presentPower) + 1;
                        presentPower = Convert.ToString(power);
                        SetPower(power.ToString("0.0"));
                        flagTime = presentTime;
                    }
                }
            }
        }


        private void btnConnect_Click(object sender, EventArgs e)
        {
        if (this.btnConnect.Text =="连接")
        {
            camera.Ip = this.txtIp.Text;
            bool isOk = camera.Connect();
            if (isOk)
            {
                this.btnConnect.Text = "断开";

                UpdateCamInfo();
                    

                //可以获取热图，并且显示,抓图，聚焦
                timer1.Start();

                camera.EnumTemperatureRanges(comboBox1);
                camera.EnumFrame(comboBox2);
                textBox1.Text = camera.EnumEmissivity().ToString();

            }
            else
                MessageBox.Show("连接失败！");
                
        }
        else
        {
            timer1.Stop();
            camera.DisConnect();
            this.pictureBox1.Image = null;
            this.btnConnect.Text = "连接";
        }
    }

        void UpdateCamInfo()
        {
            this.txtCamInfo.Text = "";
            CameraDeviceInfo info =  camera.GetCameraInfo();
            this.txtCamInfo.Text += "SwCombinationVersion:"  + info.SwCombinationVersion;
            this.txtCamInfo.Text += "OsImagekitName: " + info.OsImagekitName;
            this.txtCamInfo.Text += "ConfigurationKitName: " + info.ConfigurationKitName;
            this.txtCamInfo.Text += "DeviceIdentifier: " + info.DeviceIdentifier;
        }
            
     

        /// <summary>
        /// 获取热图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSnapShot_Click(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                try
                {
                    string name = camera.SnapShot();
                    if (name != null)
                    {
                        MessageBox.Show("保存热图成功: " + name);
                    }
                    else
                    {
                        MessageBox.Show("保存热图失败");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
               
            }
           
        }
        double recordTem = -1;
        private bool isswitch = false;
        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (!IsConnected) 
                return;

            try
            {
                if (camera.Image != null)
                {
                    camera.Image.EnterLock();
                    this.pictureBox1.Image = camera.Image.Image;
                    double tem = camera.GetTemperature();
                    temperature = tem;
                    textBox2.Text = tem.ToString();

                    if (!isswitch)
                    {
                        isswitch = true;
                        try
                        {
                            SaveContex();
                        }
                        catch (Exception ez)
                        {

                        }
                        if (tem <= 120)
                        {
                            comboBox1.SelectedIndex = 0;
                        }
                        else if (tem <= 600)
                        {
                            comboBox1.SelectedIndex = 1;
                        }
                        else if (tem <= 2000)
                        {
                            comboBox1.SelectedIndex = 2;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        isswitch = false;
                    }
                    
                    

                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
            }
            finally
            {
                if (camera.Image !=null)
                {
                    camera.Image.ExitLock();  
                }
            }
        }
        public void SaveContex()
        {
           
            string listBoxPath =Application.StartupPath+ "\\温度.txt";

            try
            {
                using (FileStream fs = File.Open(listBoxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    StreamWriter sw = new StreamWriter(fs);

                    sw.WriteLine(textBox2.Text);

                    sw.Flush();

                    sw.Close();
                }
               
            }
            catch (Exception ex)
            {
                
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            camera.SetTemperatureRanges(comboBox1.SelectedIndex);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            camera.SetFrame(comboBox2.SelectedIndex);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            camera.SetImageEmissivity(Convert.ToDouble(textBox1.Text));
        }

        //private async void button2_Click(object sender, EventArgs e)
        //{
        //    WebBrowserWindow webBrowserWindow = new WebBrowserWindow();
        //    webBrowserWindow.Show();

        //    await ConnectToServerAsync();
        //    await SendHttpRequestAsync();
        //    await CloseConnectionAsync();
        //}

        private async void button3_Click(object sender, EventArgs e)
        {
            // 获取用户输入的温度值
            string temperatureText = textBox3.Text;

            // 尝试将输入转换为温度值
            if (double.TryParse(temperatureText, out double input))
            {
                lock (lockObject)
                {
                    pidController.SetPoint = input;
                }
            }
            else
            {
                MessageBox.Show("请输入有效的温度值。");
            }

            // 第一次连接
            if (FirstConnect)
            {
                await ConnectToServerAsync();
                await SendHttpRequestAsync();
                OpenBeam();
                FirstConnect = false;
            }

            // 获取反馈值即当前温度
            double feedbackValue = temperature;
            //更新PID控制器与当前反馈值
            pidController.Update(feedbackValue);
            //从PID控制器获取输出值，并按需要使用它
            double pidOutput = pidController.output;
            //根据输出值控制激光加热
            Task.Run(()=>UpAndKeepTemperature());
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            continueheating = false;
            SetPower("0");
            CloseBeam();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
           
        }

        private void txtIp_TextChanged(object sender, EventArgs e)
        {

        }
    }

    internal class InputDialog : Form
    {
        private TextBox textBoxTemperature;
        private Button buttonOK;
        private Button buttonCancel;

        public InputDialog()
        {
            // 初始化对话框控件
            this.Text = "设置温度";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(200, 120); // 设置对话框的大小

            // 添加文本框用于输入温度
            textBoxTemperature = new TextBox();
            textBoxTemperature.Location = new Point(20, 20);
            textBoxTemperature.Size = new Size(150, 25);
            this.Controls.Add(textBoxTemperature);

            // 添加确认按钮
            buttonOK = new Button();
            buttonOK.Text = "确认";
            buttonOK.DialogResult = DialogResult.OK;
            buttonOK.Location = new Point(20, 60);
            this.Controls.Add(buttonOK);

            // 添加取消按钮
            buttonCancel = new Button();
            buttonCancel.Text = "取消";
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(100, 60);
            this.Controls.Add(buttonCancel);
        }

        // 提供一个属性来获取用户输入的温度值
        public string TemperatureInput
        {
            get { return textBoxTemperature.Text; }
        }
    }

    public class PID
    {
        private double Kp;
        private double Ki;
        private double Kd;

        public double SetPoint;
        private double PTerm;
        private double ITerm;
        private double DTerm;
        private double lastError;
        private double intError;
        private double IMaxModify;
        public double output;
        private DateTime currentTime;
        private DateTime lastTime;
        private double sampleTime;

        public PID(double P = 0.1, double I = 0.0, double D = 0.0, double sampleTime = 0.00)
        {
            Kp = P;
            Ki = I;
            Kd = D;
            this.sampleTime = sampleTime;
            currentTime = DateTime.Now;
            lastTime = currentTime;
            Clear();
        }

        public void Clear()
        {
            SetPoint = 0.0;
            PTerm = 0.0;
            ITerm = 0.0;
            DTerm = 0.0;
            lastError = 0.0;
            intError = 0.0;
            IMaxModify = 20.0;
            output = 0.0;
        }

        public void Update(double feedbackValue)
        {
            // feedbackValue:实际测量值
            // SetPoint:设定温度
            // error:当前误差
            double error = SetPoint - feedbackValue;
            currentTime = DateTime.Now;
            TimeSpan deltaTime = currentTime - lastTime;

            if (deltaTime.TotalSeconds >= sampleTime)
            {
                ITerm += error * deltaTime.TotalSeconds;

                if (ITerm < -IMaxModify)
                {
                    ITerm = -IMaxModify;
                }
                else if (ITerm > IMaxModify)
                {
                    ITerm = IMaxModify;
                }

                DTerm = 0.0;

                if (deltaTime.TotalSeconds > 0)
                {
                    DTerm = (error - lastError) / deltaTime.TotalSeconds;
                }

                lastTime = currentTime;
                lastError = error;

                // 计算输出
                output = Kp * error + Ki * ITerm + Kd * DTerm;

            }
        }
    }
}
