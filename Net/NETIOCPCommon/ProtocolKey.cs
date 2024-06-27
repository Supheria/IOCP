
namespace Net;

public class ProtocolKey
{
    public const string Request = "Request";
    public const string Response = "Response";
    public const string LeftBrackets = "[";
    public const string RightBrackets = "]";
    public const string ReturnWrap = "\r\n";
    public const char EqualSign = '=';
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
    public const string FileLength = "FileSize";
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

    public const string SourcePath = "SourcePath";
    public const string TargetPath = "TargetPath";
    public const string WriteFile = "WriteFile";
    public const string Stamp = "Stamp";

    public const string ParameterError = "ParameterError";
}

public enum ProtocolCode
{
    Success,
    NotExistCommand,
    PacketLengthError,
    PacketFormatError,
    UnknowError,
    CommandNoCompleted,
    ParameterError,
    ParameterInvalid,
    UserOrPasswordError,
    UserHasLogined,
    FileNotExist,
    NotOpenFile,
    FileIsInUse,
    FileIsExpired,
    NotSameVersion,
    Disconnection,

    DirNotExist,
    CreateDirError,
    DeleteDirError,
    DeleteFileFailed,
    FileSizeError,
}
