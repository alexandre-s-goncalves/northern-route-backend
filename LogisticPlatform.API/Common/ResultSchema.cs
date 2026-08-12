using System.Text.Json.Serialization;

namespace LogisticPlatform.API.Common;

public sealed class ResultSchema<T>
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Data { get; init; }

    [JsonConstructor]
    private ResultSchema()
    {
    }

    private ResultSchema(bool isSuccess, string? errorMessage, T? data)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Data = data;
    }

    public static ResultSchema<T> Success(T data) => new(true, null, data);
    public static ResultSchema<T> Failure(string errorMessage) => new(false, errorMessage, default);
}
