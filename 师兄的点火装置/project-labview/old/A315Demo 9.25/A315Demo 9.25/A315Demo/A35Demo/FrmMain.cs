using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

using Flir.Atlas.Live;
using Flir.Atlas.Live.Device;
using Flir.Atlas.Image.Palettes;
using Flir.Atlas.Live.Discovery;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace A35Demo
{
    public partial class FrmMain : Form
    {
        private PID pidController;
        public static FrmMain currentInstance;
        public SocketManager server;
        private string host = "192.168.3.230";
        private int port = 10001;
        // private WebBrowserWindow webBrowserWindow;
        private double temperature; // 新增一个字段来存储温度值
        private bool continueheating = true; // 控制是否加热
        private string presentPower = "10"; // 初始功率
        private bool FirstConnect = true; // 第一次连接
        private bool firstheat = true; // 第一次加热
        private bool firstbegin = true; // 第一次开始循环加热
        private object lockObject = new object();
        private bool targetTemperatureReached = false; // 目标温度未达到
        private int step = 1; // 当前执行到了第几步
        private bool isTemperatureStable = false; // 当前温度稳定标志
        private Stopwatch stableTimer = new Stopwatch();
        private System.Windows.Forms.Timer maintainTimer;// 用于显示保温时间的计时器
        private int maintainSeconds = 0; // 保温时间计时变量，以秒为单位
        private double currentTime = 0.0; // 记录当前时间
        private double timeInterval = 0.02; // 记录时间间隔，1s记录50个，每次间隔0.02s
        private double maxTemperaturePower; // 存储最高温度和最低温度时的功率
        private double minTemperaturePower;
        private int second = 0; // 记录第一次升到最高温度和第一次降至最低温度
        private CameraManager cameraManager;
        private FlirCamera camera;

        public FrmMain()
        {
            InitializeComponent();
            currentInstance = this;
            timer1.Interval = 20;
            timer2.Interval = 20; // 50hz
            server = new SocketManager(host,port);
            pidController = new PID(P: 0.2, I: 0.0, D: 0.0, sampleTime: 1.0); // 根据需要调整参数
            pidController.SetPoint = 0; // 设置设定点（期望值）
            maintainTimer = new System.Windows.Forms.Timer();
            maintainTimer.Interval = 1000; // 计时器间隔设置为1000毫秒（1秒）
            maintainTimer.Tick += new EventHandler(maintainTimer_Tick);
            CameraManager cameraManager = CameraManager.GetInstance();
            FlirCamera camera = cameraManager.GetCamera();
            trackBar1.ValueChanged += TrackBar1_ValueChanged; // 添加滑块值更改事件处理程序
        }

        // 处理滑块值更改事件
        private void TrackBar1_ValueChanged(object sender, EventArgs e)
        {
            // 获取 TrackBar 的值
            int trackBarValue = trackBar1.Value;
            // 将 TrackBar 的值映射到目标范围（0 到 100）
            double mappedValue = (double)trackBarValue / 10.0; // 除以10以获得小数精度
            // 更新显示值
            textBox9.Text = mappedValue.ToString("0.0"); // 显示一位小数
            SetPower(textBox9.Text);
        }

        // 公共静态方法，用于获取单例实例
        public static FrmMain GetInstance()
        {
            if (currentInstance == null)
            {
                currentInstance = new FrmMain();
            }
            return currentInstance;
        }

        public bool IsConnected
        {
            get
            {
                return camera.ConnectStatus == CameraStatus.Connected ? true : false;
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
            // 获取当前的 FrmMain 实例
            FrmMain frmMainInstance = currentInstance;

            if (frmMainInstance != null)
            {
                if (frmMainInstance.server != null)
                {
                    // 发送启动激光加热的命令
                    byte[] send_data = Encoding.Default.GetBytes("emon\n");
                    int data = frmMainInstance.server.Send(send_data);

                    // 显示状态
                    send_data = Encoding.Default.GetBytes("sta\n");
                    frmMainInstance.server.Send(send_data);

                    // 接收服务器响应
                    byte[] recv_data = new byte[1024];
                    int bytesReceived = frmMainInstance.server.Receive(recv_data);
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
            // 获取当前的 FrmMain 实例
            FrmMain frmMainInstance = currentInstance;

            if (frmMainInstance != null)
            {

                if (frmMainInstance.server != null)
                {
                    // 发送停止激光加热的命令
                    byte[] send_data = Encoding.ASCII.GetBytes("emoff\r");
                    frmMainInstance.server.Send(send_data);

                    // 显示状态
                    send_data = Encoding.ASCII.GetBytes("sta\r");
                    frmMainInstance.server.Send(send_data);

                    // 接收服务器响应
                    byte[] recv_data = new byte[1024];
                    int bytesReceived = frmMainInstance.server.Receive(recv_data);
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
            frmMainInstance.server.Send(sendDataWithCR);

            // 发送STA命令
            byte[] send_sta = Encoding.ASCII.GetBytes("STA\r");
            frmMainInstance.server.Send(send_sta);

            // 接收响应数据
            byte[] recv_data = new byte[1024];
            int bytesReceived = frmMainInstance.server.Receive(recv_data);
        }

        // 获取激光功率
        public static string GetPower()
        {
            FrmMain frmMainInstance = FrmMain.GetInstance();
            SocketManager server = frmMainInstance.server;

            // 发送查询激光功率的命令
            byte[] send_data = Encoding.Default.GetBytes("rop\n");
            int data = server.Send(send_data);

            // 接收服务器响应
            byte[] recv_data = new byte[1024];
            int bytesReceived = server.Receive(recv_data);
            string response = Encoding.ASCII.GetString(recv_data, 0, bytesReceived);

            // 定义正则表达式模式，匹配数字部分
            string pattern = @"ROP:\d+(\.\d+)?";

            // 创建正则表达式匹配器
            Match match = Regex.Match(response, pattern);

            // 检查是否有匹配项
            if (match.Success)
            {
                // 提取数字部分并转换为 double 类型
                string numberPart = match.Groups[1].Value;
                double result = double.Parse(numberPart);
                frmMainInstance.trackBar1.Value = (int)(result * 10); // 将 result 映射到 TrackBar 的值
                frmMainInstance.Invoke((MethodInvoker)(() =>
                {
                    frmMainInstance.textBox9.Text = result.ToString();
                }));
            }
            return response;
        }

        // 调整功率
        public async void UpAndKeepTemperature()
        {
            double startTime = DateTime.Now.ToOADate();
            double offset = 1;
            double presentTime;
            double flagTime = startTime;
            double presentTemperature = 0;
            double setTemperature = 0;

            // 初始化
            if (firstheat)
            {
                OpenBeam();
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
                    // 当温度超过设定温度+2度时，减小功率
                    if (presentTemperature > setTemperature + offset)
                    {
                        double power = double.Parse(presentPower) - 0.1;
                        presentPower = Convert.ToString(power);
                        SetPower(presentPower);
                        await Task.Delay(1000);
                    }
                    // 当温度小于设定温度-2度时，增加功率升温
                    else if (presentTemperature < setTemperature - offset && presentTemperature > setTemperature - 3 * offset)
                    {
                        double power = double.Parse(presentPower) + 0.1;
                        presentPower = Convert.ToString(power);
                        SetPower(presentPower);
                        await Task.Delay(1000);
                    }
                    // 当温度在设定温度+-2度时，维持该功率
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
                        double runTime = presentTime - startTime;
                    }
                }
            }
        }


        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (this.btnConnect.Text == "连接")
            {
                camera.Ip = this.txtIp.Text;
                bool isOk = camera.Connect();
                if (isOk)
                {
                    this.btnConnect.Text = "断开";


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
                    // 拍摄热图并获取保存的文件名
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

        // 记录温度，切换模式
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
                            // 处理异常
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
                if (camera.Image != null)
                {
                    camera.Image.ExitLock();
                }
            }
        }

        /// <summary>
        /// 保存温度到文件
        /// </summary>
        public void SaveContex()
        {
            // 定义保存温度文件的路径
            string listBoxPath = Application.StartupPath + "\\温度.txt";

            try
            {
                using (FileStream fs = File.Open(listBoxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    StreamWriter sw = new StreamWriter(fs);

                    // 将当前温度写入文件
                    sw.WriteLine(textBox2.Text);

                    sw.Flush();
                    sw.Close();
                }
            }
            catch (Exception ex)
            {
                // 处理异常
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

        private async void button3_Click(object sender, EventArgs e)
        {
            await SetTemperature();
        }

        // 升温控制逻辑
        async private Task SetTemperature()
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

            // 获取反馈值即当前温度
            double feedbackValue = temperature;
            // 更新PID控制器与当前反馈值
            pidController.Update(feedbackValue);
            // 从PID控制器获取输出值，并按需要使用它
            double pidOutput = pidController.output;

            // 获取用户输入的时间
            string keeptime = textBox6.Text;
            double desiredKeepTime = double.Parse(keeptime); // 将字符串转换为double

            while (true)
            {
                if (firstbegin)
                {
                    // 根据输出值控制激光加热
                    await Task.Run(() => UpAndKeepTemperature());
                    firstbegin = false;
                }
                // 监测温度是否稳定
                await Task.Run(() => MonitorTemperature(input));
                if (targetTemperatureReached)
                {
                    maintainTimer.Start(); // 启动保温计时器
                    await Task.Delay(TimeSpan.FromSeconds(desiredKeepTime));
                    maintainTimer.Stop(); // 停止保温计时器
                    maintainSeconds = 0;
                    step = 2;
                    targetTemperatureReached = false;
                    break;
                }
            }
        }

        // 降温控制逻辑
        private async Task CoolDownTemperature()
        {
            // 获取用户输入的温度值
            string temperatureText = textBox4.Text;

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

            // 获取反馈值即当前温度
            double feedbackValue = temperature;
            // 更新PID控制器与当前反馈值
            pidController.Update(feedbackValue);
            // 从PID控制器获取输出值，并按需要使用它
            double pidOutput = pidController.output;

            // 获取用户输入的时间
            string keeptime = textBox7.Text;
            double desiredKeepTime = double.Parse(keeptime); // 将字符串转换为double

            while (true)
            {
                // 监测温度是否稳定
                await Task.Run(() => MonitorTemperature(input));
                if (targetTemperatureReached)
                {
                    maintainTimer.Start(); // 启动保温计时器
                    await Task.Delay(TimeSpan.FromSeconds(desiredKeepTime));
                     maintainTimer.Stop(); // 停止保温计时器
                    maintainSeconds = 0;
                    step = 3;
                    targetTemperatureReached = false;
                    break;
                }
            }

        }

        // 冷却至室温
        private async Task CoolDownToRoomTemperature()
        {
            continueheating = false;
            SetPower("0");
            CloseBeam();
            // 程序等待30s，冷却结束
            await Task.Delay(30000);
            targetTemperatureReached = false;
            step = 1;
        }
        private void maintainTimer_Tick(object sender, EventArgs e)
        {
            maintainSeconds++;
            label13.Invoke((MethodInvoker)(() => 
            {
                label13.Text = $"正在保温 {maintainSeconds} 秒";
            }));
        }

        // 监测目标温度
        private async Task MonitorTemperature(double targetTemperature)
        {
            while (true)
            {
                double currentTemperature = temperature; // 读取当前温度

                if (Math.Abs(currentTemperature - targetTemperature) <= 2)
                {
                    if (!isTemperatureStable)
                    {
                        stableTimer.Start();
                        isTemperatureStable = true;
                    }
                    else if (stableTimer.Elapsed.TotalSeconds >= 10)
                    {
                        // 温度稳定超过10秒，targetTemperatureReached = true
                        targetTemperatureReached = true;
                        stableTimer.Reset(); // 重置计时器
                        isTemperatureStable = false;

                        second++;
                        if(second == 1)
                        {
                            maxTemperaturePower = double.Parse(presentPower);
                        }
                        if(second == 2)
                        {
                            minTemperaturePower = double.Parse(presentPower);
                        }
                        return;
                    }
                }
                else
                {
                    stableTimer.Reset(); // 温度不在范围内，重置计时器
                    isTemperatureStable = false;
                }
                await Task.Delay(1000); // 每秒检查一次温度
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SetPower("0");
            continueheating = false;
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

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void txtCamInfo_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private async void button2_Click(object sender, EventArgs e)
        {
            await CoolDownTemperature();
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {

        }

        async private void button5_Click(object sender, EventArgs e)
        {
            // 获取用户输入的循环次数
            if (!int.TryParse(textBox5.Text, out int loopCount) || loopCount <= 0)
            {
                MessageBox.Show("请输入有效的循环次数。");
                return;
            }
            // 第一次连接
            if (FirstConnect)
            {
                await server.ConnectToServerAsync();
                await server.SendHttpRequestAsync();
                FirstConnect = false;
            }

            // 循环开始
            for (int i = loopCount; i > 0; i--)
            {
                textBox8.Text = i.ToString();
                continueheating = true;
                firstheat = true;
                firstbegin = true;

                // 加热到目标温度
                if (step == 1)
                {
                    label13.Invoke((MethodInvoker)(() =>
                    {
                        label13.Text = "正在加热";
                    }));
                    // 设置激光功率为最高温度时的功率
                    SetPower(maxTemperaturePower.ToString("0.0"));
                    await SetTemperature();
                }

                // 降温
                if (step == 2)
                {
                    label13.Invoke((MethodInvoker)(() =>
                    {
                        label13.Text = "正在降温";
                    }));
                    // 设置激光功率为最低温度时的功率
                    SetPower(minTemperaturePower.ToString("0.0"));
                    await CoolDownTemperature();
                }

                // 冷却至室温
                if (step == 3)
                {
                    label13.Invoke((MethodInvoker)(() =>
                    {
                        label13.Text = "正在冷却至室温";
                    }));
                    await CoolDownToRoomTemperature();
                }
            }
        }

        private void textBox8_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox9_TextChanged(object sender, EventArgs e)
        {
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            timer2.Start();
        }
        private void button9_Click(object sender, EventArgs e)
        {
            timer2.Stop();
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            int maxDataPoints = 500;
            // 模拟获取温度数据，您需要替换为实际的数据来源
            double currentTemperature = camera.GetTemperature();

            // 确定当前相对时间
            currentTime += timeInterval;

            // 获取图表中 "Temperature" 系列的引用
            var temperatureSeries = chart1.Series["Temperature"];

            // 将相对时间和温度数据添加到图表中
            temperatureSeries.Points.AddXY(currentTime, currentTemperature);

            //// 如果数据点数量超过一定数量，您可以删除旧的数据点以保持图表的性能
            //while (temperatureSeries.Points.Count > maxDataPoints)
            //{
            //    temperatureSeries.Points.RemoveAt(0);
            //}

            // 将数据保存到文件
            string dataFilePath = "../../data.txt";
            string dataLine = $"{currentTime.ToString(CultureInfo.InvariantCulture)},{currentTemperature.ToString(CultureInfo.InvariantCulture)}";
            File.AppendAllText(dataFilePath, dataLine + Environment.NewLine);
        }

        internal class InputDialog : Form
        {
            private System.Windows.Forms.TextBox textBoxTemperature;
            private System.Windows.Forms.Button buttonOK;
            private System.Windows.Forms.Button buttonCancel;

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
                textBoxTemperature = new System.Windows.Forms.TextBox();
                textBoxTemperature.Location = new Point(20, 20);
                textBoxTemperature.Size = new Size(150, 25);
                this.Controls.Add(textBoxTemperature);

                // 添加确认按钮
                buttonOK = new System.Windows.Forms.Button();
                buttonOK.Text = "确认";
                buttonOK.DialogResult = DialogResult.OK;
                buttonOK.Location = new Point(20, 60);
                this.Controls.Add(buttonOK);

                // 添加取消按钮
                buttonCancel = new System.Windows.Forms.Button();
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

            public PID(double P = 0.2, double I = 0.0, double D = 0.0, double sampleTime = 0.00)
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

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }
    }
}

