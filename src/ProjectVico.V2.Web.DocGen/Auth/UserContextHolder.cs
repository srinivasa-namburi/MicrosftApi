using ProjectVico.V2.Web.Shared.Auth;

namespace ProjectVico.V2.Web.DocGen.Auth;

public class UserContextHolder : IUserContextHolder
{
    public string Token { get; set; }
}