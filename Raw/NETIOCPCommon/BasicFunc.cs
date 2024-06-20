using System.IO;
using System.Text;

namespace Net
{
    public class BasicFunc
    {
        public static bool IsFileInUse(string fileName)
        {
            bool inUse = true;
            FileStream fs = null;
            try
            {
                try
                {
                    fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);//使用共享只读方式打开，可以支持多个客户端同时访问一个文件。
                    inUse = false;
                }
                catch
                {
                    inUse = true;
                }
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
            return inUse;
        }

        public static string MD5String(string value)
        {
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] data = Encoding.Default.GetBytes(value);
            byte[] md5Data = md5.ComputeHash(data);
            md5.Clear();
            string result = "";
            for (int i = 0; i < md5Data.Length; i++)
            {
                result += md5Data[i].ToString("x").PadLeft(2, '0');
            }
            return result;
        }
        /// <summary>
        /// 判断Socket是否连接，Socket的属性中没有一个可靠的判断方式，使用该方法比较准确
        /// </summary>
        /// <param name="socket">socket</param>
        /// <returns>是否连接</returns>
        public static bool SocketConnected(System.Net.Sockets.Socket socket)
        {
            bool blockingState = socket.Blocking;
            try
            {
                byte[] tmp = new byte[1];
                socket.Blocking = false;
                socket.Send(tmp, 0, 0);
                //return true;
                return socket.Connected;//Connected为false时，竟然发送数据无异常？目前仍然没有好办法来判断socket是否已经断开
            }
            catch (System.Net.Sockets.SocketException e)
            {
                //10035==WSAEWOULDBLOCK
                if (e.NativeErrorCode.Equals(10035))
                    return true;
                else
                    return false;
            }
            finally
            {
                socket.Blocking = blockingState;
            }
                
        }
    }
}
