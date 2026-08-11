using System.Diagnostics.CodeAnalysis;

namespace LogisticPlatform.API.Common;

internal sealed class ResultSchema<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? ErrorMessage { get; }

    private ResultSchema(bool isSuccess, T? data, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
    }

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Standard Result Pattern factory methods.")]
    public static ResultSchema<T> Success(T data) => new(true, data, null);

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Standard Result Pattern factory methods.")]
    public static ResultSchema<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
