using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages;

public record SignalRKeepAlive(Guid CorrelationId):CorrelatedBy<Guid>;