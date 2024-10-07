using Microsoft.AspNetCore.Builder;

namespace Microsoft.Greenlight.Shared.Interfaces;

public interface IEndpointDefinition
{
    void DefineEndpoints(WebApplication app);
}
