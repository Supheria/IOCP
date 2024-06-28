namespace Net;

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
    NotLogined,
}
