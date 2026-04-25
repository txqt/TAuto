namespace TAuto.Shared.Models;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string? error)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException("Success result cannot have an error message.");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("Failure result must have an error message.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException("Failure result has no value.");

    protected Result(bool isSuccess, T? value, string? error) : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static new Result<T> Failure(string error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
