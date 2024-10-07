using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages;

public record SignalRKeepAlive(Guid CorrelationId):CorrelatedBy<Guid>;
