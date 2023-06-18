using System.Net;
using System.Net.Sockets;
using LoongEgg.LoongLogger;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TCP_UDP通信
{
    public class Client : IDisposable
    {
        private Socket socket;
        private EndPoint remoteEP;
        private string host;
        private int port;
        private Thread? receiveThread;
        private bool disposedValue;

        private bool retry;
        private int retry_number;
        private int retry_TEMP_number = 0;
        private int retry_delay;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public Client(string host, int port, bool retry = false, int number = 5, int delay = 1000)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
            this.host = host;
            this.port = port;
            this.retry = retry;
            this.retry_number = number;
            this.retry_delay = delay;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPHostEntry hostEntry = Dns.GetHostEntry(host);
            remoteEP = new IPEndPoint(hostEntry.AddressList[0], port);
        }

        public void ConnectTCP()
        { 
        retry_Connect:
            try
            {
                socket.Connect(remoteEP);

                Logger.WriteInfor($"TCP已连接! Host:{host}:{port}");

                receiveThread = new Thread(ReceiveLoop);
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Logger.WriteError("连接到服务器失败: " + ex.Message);
                if (retry & retry_TEMP_number < retry_number)
                {
                    retry_TEMP_number++;
                    Thread.Sleep(retry_delay);
                    Logger.WriteInfor($"连接重试第{retry_TEMP_number}次");
                    goto retry_Connect;
                }
                else if (retry)
                {
                    Logger.WriteError("多次尝试连接依旧失败!: " + ex.Message);
                }
                throw new Exception("连接到服务器失败: " + ex.Message);
            }
        }

        public void ConnectUDP()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            remoteEP = new IPEndPoint(IPAddress.Parse(host), port);

            try
            {
                socket.Connect(remoteEP);

                Logger.WriteInfor($"UDP已连接! Host:{host}:{port}");

                receiveThread = new Thread(ReceiveLoop);
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Logger.WriteError("连接到服务器失败: " + ex.Message);
                throw;
            }
        }

        public event Action<string> DataReceived;
        public event Action<string> DataSent;

        private void ReceiveLoop()
        {
            while (true)
            {
                try
                {
                    byte[] data = new byte[1024];
                    int bytesReceived = socket.Receive(data);

                    if (bytesReceived > 0)
                    {
                        string message = System.Text.Encoding.UTF8.GetString(data, 0, bytesReceived);
                        Logger.WriteInfor("收到数据: " + message);
                        DataReceived?.Invoke(message);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteError("接收数据失败: " + ex.Message);
                    throw;
                }
            }
        }

        public void Send(string message)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
            socket.BeginSend(data, 0, data.Length, SocketFlags.None, ar =>
            {
                try
                {
                    int bytesSent = socket.EndSend(ar);
                    Logger.WriteInfor("发送数据: " + message);
                    DataSent?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Logger.WriteError("接收数据失败: " + ex.Message);
                }
            }, null);
        }

        public void SendFile(string fileName)
        {
            byte[] fileData = File.ReadAllBytes(fileName);
            byte[] fileNameData = System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(fileName));
            byte[] data = new byte[fileNameData.Length + fileData.Length + 1];
            fileNameData.CopyTo(data, 0);
            data[fileNameData.Length] = 0;
            fileData.CopyTo(data, fileNameData.Length + 1);
            socket.BeginSend(data, 0, data.Length, SocketFlags.None, ar =>
            {
                try
                {
                    int bytesSent = socket.EndSend(ar);
                    Logger.WriteInfor("发送文件: " + fileName);
                    DataSent?.Invoke(fileName);
                }
                catch (Exception ex)
                {
                    Logger.WriteError("发送文件失败: " + fileName + ", " + ex.Message);
                }
            }, null);
        }

        public void Close()
        {
            try
            {
                if (socket.Connected) socket.Shutdown(SocketShutdown.Both);
                socket.Close();

                Logger.WriteInfor("连接关闭");
            }
            catch (Exception ex)
            {
                Logger.WriteError("关闭连接失败: " + ex.Message);
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                    socket.Dispose();
                }

                disposedValue = true;
            }
        }

        ~Client()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
