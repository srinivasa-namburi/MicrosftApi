using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages;

/// <summary>
/// Generic message to be used to send messages from the backend processing service to the frontend.
/// It is up to the consumer to decide how to handle the message.
/// The Correlation ID is used as a SignalR group ID.
/// </summary>
/// <param name="CorrelationId"></param>
/// <param name="Message"></param>
public record BackendProcessingMessageGenerated (Guid CorrelationId, string Message) : CorrelatedBy<Guid>
{
    
}