namespace Warehouse.Application.Common;

public enum ErrorType
{
    NotFound,
    Validation,
    Conflict
}

public record Error(ErrorType Type, string Message);

public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(ErrorType type, string message) => new(false, new Error(type, message));

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(ErrorType type, string message) => Result<T>.Failure(type, message);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, Error? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static new Result<T> Failure(ErrorType type, string message) => new(false, default, new Error(type, message));
}
