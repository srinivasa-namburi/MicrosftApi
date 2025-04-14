using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Grains.Shared.Contracts.Models
{
    public class GenericResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }

        [JsonConstructor]
        private GenericResult(bool isSuccess, string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static GenericResult Success() => new(true);
        public static GenericResult Failure(string errorMessage) => new(false, errorMessage);
    }

    public class GenericResult<T>
    {
        public bool IsSuccess { get; }
        public T? Data { get; }
        public string? ErrorMessage { get; }

        [JsonConstructor]
        private GenericResult(bool isSuccess, T? data = default, string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorMessage = errorMessage;
        }

        public static GenericResult<T> Success(T data) => new(true, data);
        public static GenericResult<T> Failure(string errorMessage) => new(false, default, errorMessage);
    }

}