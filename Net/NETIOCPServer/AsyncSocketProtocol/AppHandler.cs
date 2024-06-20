
namespace Net
{
    /// <summary>
    /// 此类将网络通信内部需要用户处理的逻辑引出到框架外，降低程序耦合度，如有类似情况，请参照此类编写
    /// </summary>
    public class AppHandler
    {
        public delegate void HandlerReceivedMsg(string msg,ServerFullHandlerProtocol fullHandlerSocketProtocol);
        public event HandlerReceivedMsg OnReceivedMsg;
        public void HandlerMsg(string msg, ServerFullHandlerProtocol fullHandlerSocketProtocol)
        {
            if (OnReceivedMsg != null)
            {
                OnReceivedMsg(msg, fullHandlerSocketProtocol);
            }
        }
    }
}
