namespace Limita.Application.Common;

public class Result
{
    public bool Succeeded { get; protected set; }
    public bool IsSuccess => Succeeded;
    public string? Message { get; protected set; }
    public List<string> Errors { get; protected set; } = new();

    public static Result Success(string? message = null)
    {
        return new Result
        {
            Succeeded = true,
            Message = message
        };
    }

    public static Result Failure(string error)
    {
        return new Result
        {
            Succeeded = false,
            Errors = new List<string> { error }
        };
    }

    public static Result Failure(IEnumerable<string> errors, string? message = null)
    {
        return new Result
        {
            Succeeded = false,
            Message = message,
            Errors = errors.ToList()
        };
    }

    public static Result<T> Success<T>(T data, string? message = null) => Result<T>.Success(data, message);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
    public static Result<T> Failure<T>(IEnumerable<string> errors, string? message = null) => Result<T>.Failure(errors, message);
}

public class Result<T> : Result
{
    public T? Data { get; private set; }
    public T? Value => Data;

    public static Result<T> Success(T data, string? message = null)
    {
        return new Result<T>
        {
            Succeeded = true,
            Data = data,
            Message = message
        };
    }

    public new static Result<T> Failure(string error)
    {
        return new Result<T>
        {
            Succeeded = false,
            Data = default,
            Errors = new List<string> { error }
        };
    }

    public new static Result<T> Failure(IEnumerable<string> errors, string? message = null)
    {
        return new Result<T>
        {
            Succeeded = false,
            Data = default,
            Message = message,
            Errors = errors.ToList()
        };
    }
}
