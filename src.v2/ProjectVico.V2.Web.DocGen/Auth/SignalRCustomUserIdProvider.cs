using Microsoft.AspNetCore.SignalR;

namespace ProjectVico.V2.Web.DocGen.Auth;

public class SignalRCustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        //Use the claim type that contains the user's OID. Adjust the claim type as necessary.
        var oid = connection.User?.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
        return oid;
    }
}