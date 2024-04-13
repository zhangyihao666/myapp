using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Flir.Atlas.Live;
using Flir.Atlas.Live.Device;
using Flir.Atlas.Image.Palettes;
using System.Threading;
using Flir.Atlas.Live.Remote;
using System.IO;
using Flir.Atlas.Image;
using Flir.Atlas.Live.Discovery;
using System.Drawing;
using System.Windows.Forms;

namespace A35Demo
{
    public enum CameraStatus
    {
        Connected,
        Connecting,
        DisConnected,
        ReTryConnect
    }

    public class CameraStatusArgs : EventArgs
    {
        public CameraStatus Status { get; set; }
        public string Msg { get; set; }

    }
    public class FlirCamera
    {
        private readonly ThermalGigabitCamera camera = new ThermalGigabitCamera();


        /// <summary>
        /// 启用自动重连
        /// </summary>
        public bool IsUseAutoConnect { get; set; }

        private bool IsUserDisConnect { get; set; }

        public EventHandler<CameraStatusArgs> CameraStatusChanged;
        public FlirCamera()
        {
            IsUseAutoConnect = true;
            camera.AutoReconnect = IsUseAutoConnect;

            IsUserDisConnect = false;

            camera.DeviceError += camera_DeviceError;
            camera.ConnectionStatusChanged += camera_ConnectionStatusChanged;
        }

        /// <summary>
        /// 相机连接状态通知
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void camera_ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            string msg = "";

            switch (e.Status)
            {
                case ConnectionStatus.Connected:
                    msg = "相机已经连接";
                    ConnectStatus = CameraStatus.Connected;
                    break;

                //相机 自动重连时间为 10 秒钟心跳包， 其中， 需要加 try catch 判断。 事件发生后，才能返回相机状态
                case ConnectionStatus.Connecting:
                    if (IsUserDisConnect)
                    {
                        ConnectStatus = CameraStatus.ReTryConnect;
                        msg = "相机正在重连中";
                    }
                    else
                    {
                        ConnectStatus = CameraStatus.Connecting;
                        msg = "相机正在连接中";
                    }
                    msg = "相机正在连接中";
                    break;
                case ConnectionStatus.Disconnected:
                    msg = "相机已经断开";
                    break;
                case ConnectionStatus.Disconnecting:
                    msg = "相机断开中";
                    break;
                default:
                    break;
            }

            // 处理相机状态
            if (CameraStatusChanged != null)
            {
                CameraStatusChanged(null, new CameraStatusArgs() { Status = ConnectStatus, Msg = msg });
            }


        }

        /// <summary>
        /// 相机连接错误
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void camera_DeviceError(object sender, DeviceErrorEventArgs e)
        {
            string msg = e.ErrorMessage;

            //通知调用程序
        }

        public string Ip { get; set; }

        public CameraStatus ConnectStatus { get; private set; }


        public ThermalImage Image
        {
            get
            {
                if (ConnectStatus == CameraStatus.Connected)
                {
                    return camera.GetImage() as ThermalImage;
                }
                else
                    return null;
            }

        }

        public bool Connect()
        {
            // 连接等待测试
            int times = 0;
            var device = CameraDeviceInfo.Create(Ip, Interface.Gigabit);
            if (device == null)
            {
                return false;
            }
            ConnectStatus = CameraStatus.DisConnected;

            //用户没有断开
            IsUserDisConnect = false;

            camera.Connect(device);

            while (times < 40)
            {
                if (camera.ConnectionStatus != ConnectionStatus.Connected)
                {
                    Thread.Sleep(100);
                    times++;
                }
                else
                {
                    ConnectStatus = CameraStatus.Connected;
                    break;
                }
            }

            //相机状态
            if (ConnectStatus == CameraStatus.Connected)
            {
                //可以获取热图和进行操作
                Image.Palette = PaletteManager.Iron;
                Image.Scale.IsAutoAdjustEnabled = true;

                //属性设备信息
                GetCameraInfo();

            }
            else
            {
                return false;
            }



            return true;
        }


        /// <summary>
        /// 人工关闭连接，不再自动重连尝试
        /// </summary>
        /// <returns></returns>
        public bool DisConnect()
        {
            if (ConnectStatus == CameraStatus.DisConnected)
            {
                return true;
            }

            // 用户手动关闭，不再自动重连
            IsUserDisConnect = true;

            camera.Disconnect();
            ConnectStatus = CameraStatus.DisConnected;

            return true;
        }


        public string SnapShot()
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return null;
            }
            string name = "";
            string path = @"d:\IrImages\";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            name = path + System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss fff") + ".jpg";

            try
            {
                camera.GetImage().EnterLock();
                camera.GetImage().SaveSnapshot(name);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
            finally
            {
                camera.GetImage().ExitLock();
            }
            return name;

        }








        /// <summary>
        /// 设置热图发射率
        /// </summary>
        /// <param name="emissivity"></param>
        /// <returns></returns>
        public bool SetImageEmissivity(double emissivity)
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return false;
            }
            if (emissivity < 0.1 || emissivity > 1)
            {
                return false;
            }
            try
            {
                if (Image != null)
                {
                    Image.ThermalParameters.Emissivity = emissivity;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;

        }



        /// <summary>
        /// 执行一次NUC 校准
        /// A non-uniformity correction can take up to 10 seconds. 
        /// 最多10秒 一般3秒时间， 内部为 异步函数调用
        /// </summary>
        /// <returns></returns>
        public bool Nuc()
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return false;
            }

            try
            {
                camera.RemoteControl.CameraAction.Nuc();
            }
            catch (Exception)
            {
                return false;
            }
            return true;

        }





        public CameraDeviceInfo GetCameraInfo()
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return null;
            }

            return camera.CameraDeviceInfo;

        }
        public bool IsReadingTemperature { get; set; } = true;

        public double  GetTemperature()
        {
        
            if (ConnectStatus != CameraStatus.Connected)
            {
                return 0;
            }
            Rectangle rectangle = new Rectangle(0, 0, 640, 480);
            double[] vs1 = camera.ThermalImage.GetValues(rectangle);
            Array.Sort(vs1);
            double max = vs1[vs1.Length - 1];
            return max;
          
            ////辐射率相机对象直接设置参数辐射率为0.9
            //camera.ThermalImage.ThermalParameters.Emissivity = 0.9;
            ////读取相机所有的量程
            //camera.RemoteControl.CameraSettings.EnumerateTemperatureRanges();
            ////设置量程序号0 1 2...
            //camera.RemoteControl.CameraSettings.SetTemperatureRangeIndex(0);


            ////读取相机的所有的帧频
            //var listFrames = camera.EnumerateFrameRates();
            ////读取当前的帧频序号
            //var index = camera.FrameRateIndex;
            ////设置相机的帧频序号0 12...(注意100 200 Hz图像的高度会改变,一定要注意，不建议使用100 20OHz的帧频)
            //camera.FrameRateIndex = 0;

        }
        public double EnumEmissivity()
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return 0;
            }
            return camera.ThermalImage.ThermalParameters.Emissivity;
        }
        public void  SetEmissivity( double value)
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return ;
            }
             camera.ThermalImage.ThermalParameters.Emissivity= value;
        }
        public void EnumTemperatureRanges(ComboBox comboBoxTempRanges)
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return;
            }
            var ranges = camera.RemoteControl.CameraSettings.EnumerateTemperatureRanges();
            if (ranges.Any())
            {
                foreach (var enumerateTemperatureRange in ranges)
                {
                    string value = string.Format("{0:F};{1:F}", enumerateTemperatureRange.Minimum, enumerateTemperatureRange.Maximum);


                    comboBoxTempRanges.Items.Add(value);
                }
                int index = camera.RemoteControl.CameraSettings.GetTemperatureRangeIndex();

                if (index >= 0)
                {
                    comboBoxTempRanges.SelectedIndex = index;
                    comboBoxTempRanges.Enabled = true;
                }
            }
        }
        public void SetTemperatureRanges(int index)
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return;
            }
            int indexNow = camera.RemoteControl.CameraSettings.GetTemperatureRangeIndex();
            if (indexNow!= index)
            {
                camera.RemoteControl.CameraSettings.SetTemperatureRangeIndex(index);
            }
          

        }

        public void EnumFrame(ComboBox comboBoxFrame)
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return;
            }
            var listFrames = camera.EnumerateFrameRates();

            foreach (var frame in listFrames)
            {



                comboBoxFrame.Items.Add(frame);
            }
            int index = camera.FrameRateIndex;

            if (index >= 0)
            {
                comboBoxFrame.SelectedIndex = index;
                comboBoxFrame.Enabled = true;
            }

        }
        public void SetFrame(int index)
        {
            if (ConnectStatus != CameraStatus.Connected)
            {
                return;
            }
            camera.FrameRateIndex = index;

        }

    }
}
