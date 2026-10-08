namespace FileFlowHR.Application.Common;

public enum FailureKind { None, Invalid, Forbidden, NotFound }

/// <summary>Lets the pipeline build a failed response of any result type (static abstract factory).</summary>
public interface IResult
{
    bool Succeeded { get; }
    FailureKind Kind { get; }
    IReadOnlyList<string> Errors { get; }
}

public interface IResultFactory<TSelf> where TSelf : IResultFactory<TSelf>
{
    static abstract TSelf Fail(FailureKind kind, IReadOnlyList<string> errors);
}

/// <summary>Outcome of a command with no return value. Errors are safe to show to users.</summary>
public sealed class Result : IResult, IResultFactory<Result>
{
    private Result(FailureKind kind, IReadOnlyList<string> errors) { Kind = kind; Errors = errors; }

    public bool Succeeded => Kind == FailureKind.None;
    public FailureKind Kind { get; }
    public IReadOnlyList<string> Errors { get; }
    public string ErrorMessage => string.Join(" ", Errors);

    public static Result Success() => new(FailureKind.None, []);
    public static Result Invalid(params string[] errors) => new(FailureKind.Invalid, errors);
    public static Result NotFound(string what) => new(FailureKind.NotFound, [$"{what} was not found."]);
    public static Result Fail(FailureKind kind, IReadOnlyList<string> errors) => new(kind, errors);
}

/// <summary>Outcome of a command or query that returns a value.</summary>
public sealed class Result<T> : IResult, IResultFactory<Result<T>>
{
    private Result(T? value, FailureKind kind, IReadOnlyList<string> errors) { Value = value; Kind = kind; Errors = errors; }

    public T? Value { get; }
    public bool Succeeded => Kind == FailureKind.None;
    public FailureKind Kind { get; }
    public IReadOnlyList<string> Errors { get; }
    public string ErrorMessage => string.Join(" ", Errors);

    public static Result<T> Success(T value) => new(value, FailureKind.None, []);
    public static Result<T> Invalid(params string[] errors) => new(default, FailureKind.Invalid, errors);
    public static Result<T> NotFound(string what) => new(default, FailureKind.NotFound, [$"{what} was not found."]);
    public static Result<T> Fail(FailureKind kind, IReadOnlyList<string> errors) => new(default, kind, errors);

    public static implicit operator Result<T>(T value) => Success(value);
}
