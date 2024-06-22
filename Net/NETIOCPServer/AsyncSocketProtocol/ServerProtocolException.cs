using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net;

internal class ServerProtocolException(string message) : IocpException(message)
{

}
