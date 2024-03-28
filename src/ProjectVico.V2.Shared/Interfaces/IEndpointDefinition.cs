using Microsoft.AspNetCore.Builder;

namespace ProjectVico.V2.Shared.Interfaces;

public interface IEndpointDefinition
{
    void DefineEndpoints(WebApplication app);
}