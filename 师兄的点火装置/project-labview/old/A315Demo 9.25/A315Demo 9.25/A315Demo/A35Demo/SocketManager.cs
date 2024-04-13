using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace A35Demo
{
    public class SocketManager
    {
        private Socket server;
        private string host;
        private int port;

        public SocketManager(string host, int port)
        {
            this.host = host;
            this.port = port;
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public int Send(byte[] data)
        {
                return server.Send(data);
        }

        public int Receive(byte[] buffer)
        {
            try
            {
                int bytesRead = server.Receive(buffer); // 接收数据并返回接收的字节数
                return bytesRead;
            }
            catch (SocketException ex)
            {
                // 处理接收数据时发生的异常
                return -1; // 返回一个错误代码或其他指示
            }
        }

        //连接主机
        public async Task ConnectToServerAsync()
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

        // 发送HTTP请求并等待响应
        public async Task SendHttpRequestAsync()
        {
            // 构造HTTP请求的文本
            string httpRequest = "GET / HTTP/1.1\r\n" +
                                 "Host: " + host + "\r\n" +
                                 "\r\n";

            // 将HTTP请求文本转换为字节数组
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

            // 发送HTTP请求
            server.SendAsync(sendEventArgs);
            await sendTaskCompletionSource.Task;
        }

        // 接收服务器发送的 HTTP 响应内容，并将其作为字符串返回
        public async Task<string> ReceiveHttpResponseAsync()
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

                // 异步接收服务器响应数据
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

        // 关闭socket连接
        public async Task CloseConnectionAsync()
        {
            await Task.Delay(1000); // 等待一段时间，确保响应内容接收完整
            server.Close();
            Console.WriteLine("成功关闭socket连接");
        }
    }
}
