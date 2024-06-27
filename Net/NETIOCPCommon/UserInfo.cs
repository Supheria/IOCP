using LocalUtilities.TypeToolKit.Text;

namespace Net;

public class UserInfo(string name, string password)
{
    public string Id { get; } = name.ToLower().ToMd5HashString();

    public string Password { get; set; } = password.ToMd5HashString();

    public string Name { get; set; } = name.ToLower();

    public UserInfo() : this("", "")
    {

    }
}
