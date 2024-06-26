
namespace Net;

public class ProtocolKey
{
    public const string Request = "Request";
    public const string Response = "Response";
    public const string LeftBrackets = "[";
    public const string RightBrackets = "]";
    public const string ReturnWrap = "\r\n";
    public const string EqualSign = "=";
    public const string Command = "Command";
    public const string Code = "Code";
    public const string Message = "Message";
    public const string UserName = "UserName";
    public const string Password = "Password";
    public const string Organization = "Organization";
    public const string Autograph = "Autograph";
    public const string FileName = "FileName";
    public const string Item = "Item";
    public const string ParentDir = "ParentDir";
    public const string DirName = "DirName";
    public const char TextSeperator = (char)1;
    public const string FileSize = "FileSize";
    public const string PacketSize = "PacketSize";

    public const string FileExists = "FileExists";
    public const string OpenFile = "OpenFile";
    public const string SetSize = "SetSize";
    public const string GetSize = "GetSize";
    public const string SetPosition = "SetPosition";
    public const string GetPosition = "GetPosition";
    public const string Read = "Read";
    public const string Write = "Write";
    public const string Seek = "Seek";
    public const string CloseFile = "CloseFile";
    public const string Mode = "Mode";
    public const string Size = "Size";
    public const string Position = "Position";
    public const string Count = "Count";
    public const string Offset = "Offset";
    public const string SeekOrigin = "SeekOrigin";
    public const string Login = "Login";
    public const string Active = "Active";
    public const string GetClients = "GetClients";
    public const string Dir = "Dir";
    public const string CreateDir = "CreateDir";
    public const string DeleteDir = "DeleteDir";
    public const string FileList = "FileList";
    public const string DeleteFile = "DeleteFile";
    public const string Upload = "Upload";
    public const string Data = "Data";
    public const string Eof = "Eof";
    public const string Download = "Download";
    public const string SendFile = "SendFile";
    public const string CyclePacket = "CyclePacket";

    public const string UserID = "UserID";
    public const string UserPermissions = "UserPermissions";
}

public class ProtocolCode
{
    public const int Success = 0x00000000;
    public const int NotExistCommand = Success + 0x01;
    public const int PacketLengthError = Success + 0x02;
    public const int PacketFormatError = Success + 0x03;
    public const int UnknowError = Success + 0x04;
    public const int CommandNoCompleted = Success + 0x05;
    public const int ParameterError = Success + 0x06;
    public const int UserOrPasswordError = Success + 0x07;
    public const int UserHasLogined = Success + 0x08;
    public const int FileNotExist = Success + 0x09;
    public const int NotOpenFile = Success + 0x0A;
    public const int FileIsInUse = Success + 0x0B;

    public const int DirNotExist = 0x02000001;
    public const int CreateDirError = 0x02000002;
    public const int DeleteDirError = 0x02000003;
    public const int DeleteFileFailed = 0x02000007;
    public const int FileSizeError = 0x02000008;

    public static string GetErrorCodeString(int errorCode)
    {
        string errorString = null;
        if (errorCode == NotExistCommand)
            errorString = "Not Exist Command";
        return errorString;
    }
}
