// Create a generic result class in a shared location
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Grains.Validation.Contracts.Models;

/// <summary>
/// Contains both data and step results for a validation step to facilitate orchestration.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ValidationStepResult<T>
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public T? Data { get; }

    [JsonConstructor]
    private ValidationStepResult(bool isSuccess, T? data, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
    }
    
    public static ValidationStepResult<T> Success(T data) => 
        new(true, data, null);
        
    public static ValidationStepResult<T> Failure(string errorMessage) => 
        new(false, default, errorMessage);
}

/// <summary>
/// Contains the result of a validation step that doesn't need to return data.
/// </summary>
public class ValidationStepResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    [JsonConstructor]
    private ValidationStepResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }
        
    public static ValidationStepResult Success() => 
        new(true, null);
            
    public static ValidationStepResult Failure(string errorMessage) => 
        new(false, errorMessage);
}