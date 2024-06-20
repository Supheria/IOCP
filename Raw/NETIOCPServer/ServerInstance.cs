using log4net;
using System;
using System.IO;
using System.Net;
namespace Net
{
    public static class ServerInstance
    {
        public static ILog Logger;
        public static AsyncSocketServer AsyncSocketSvr;
        public static string FileDirectory;
        public static AppHandler appHandler;
        /// <summary>
        /// 服务端实例初始化
        /// </summary>
        /// <param name="fileDirectory">服务端用于接收文件的目录</param>
        /// <param name="Port">侦听端口</param>
        /// <param name="MaxConnection">最大并发连接数</param>
        /// <param name="SocketTimeOutMS">超时（毫秒）</param>
        /// <param name="localIP">服务端用于侦听的IP，0.0.0.0表示服务端的所有IP</param>
        public static void Init(string fileDirectory, int Port, int MaxConnection, int SocketTimeOutMS,string localIP)
        {
            DateTime currentTime = DateTime.Now;
            log4net.GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
            log4net.GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
            Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            FileDirectory = fileDirectory;
            if (FileDirectory == "")
                FileDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Files");
            if (!Directory.Exists(FileDirectory))
                Directory.CreateDirectory(FileDirectory);

            int port = Port;
            int parallelNum = MaxConnection;
            int socketTimeOutMS = SocketTimeOutMS;//1 * 60 * 1000
            AsyncSocketSvr = new AsyncSocketServer(parallelNum);            
            AsyncSocketSvr.SocketTimeOutMS = socketTimeOutMS;
            AsyncSocketSvr.Init();
            IPEndPoint listenPoint = new IPEndPoint(IPAddress.Parse(localIP), port);
            AsyncSocketSvr.Start(listenPoint);
            //AsyncSocketSvr.SetNoDelay(true);//增强实时性时设为无延迟
        }
        public static void Close()
        {
            if (AsyncSocketSvr != null)
                AsyncSocketSvr.Close();
        }
    }    
}
