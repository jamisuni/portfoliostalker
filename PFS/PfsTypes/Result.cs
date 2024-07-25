
namespace Pfs.Types;

public abstract class Result    // https://josef.codes/my-take-on-the-result-class-in-c-sharp/
{
    public bool Ok { get; protected set; }
    public bool Fail => !Ok;
}

public abstract class Result<T> : Result
{
    private T _data;

    protected Result(T data)
    {
        Data = data;
    }

    public T Data
    {
        get => Ok ? _data : throw new Exception($"You can't access .{nameof(Data)} when .{nameof(Ok)} is false");
        set => _data = value;
    }
}

public class OkResult : Result
{
    public OkResult()
    {
        Ok = true;
    }
}

public class OkResult<T> : Result<T>
{
    public OkResult(T data) : base(data)
    {
        Ok = true;
    }
}

public class FailResult : Result, IFailResult
{
    public FailResult(string message) : this(message, Array.Empty<Fail>())
    {
    }

    public FailResult(string message, IReadOnlyCollection<Fail> errors)
    {
        Message = message;
        Ok = false;
        Errors = errors ?? Array.Empty<Fail>();
    }

    public string Message { get; }
    public IReadOnlyCollection<Fail> Errors { get; }
}

public class FailResult<T> : Result<T>, IFailResult
{
    public FailResult(string message) : this(message, Array.Empty<Fail>())
    {
    }

    public FailResult(string message, IReadOnlyCollection<Fail> errors) : base(default)
    {
        Message = message;
        Ok = false;
        Errors = errors ?? Array.Empty<Fail>();
    }

    public string Message { get; set; }
    public IReadOnlyCollection<Fail> Errors { get; }
}

public class Fail
{
    public Fail(string details) : this(null, details)
    {

    }

    public Fail(string code, string details)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }
    public string Details { get; }
}

internal interface IFailResult
{
    string Message { get; }
    IReadOnlyCollection<Fail> Errors { get; }
}