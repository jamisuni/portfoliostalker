
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

#if false // !!!THINK!!! Perplexity

The concept you're referring to is often called a "Result" pattern, which is indeed useful for error handling in C#. While the implementation you've been 
using is robust, there are simpler approaches that can reduce the need for explicit casting and inheritance. Here's an alternative approach that maintains 
the core functionality while simplifying usage:

** A Simplified Result Pattern

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }

    private Result(bool isSuccess, T value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new Result<T>(true, value, null);
    public static Result<T> Fail(string error) => new Result<T>(false, default, error);

    public TResult Match<TResult>(Func<T, TResult> success, Func<string, TResult> failure)
        => IsSuccess ? success(Value) : failure(Error);
}
```

This implementation offers several advantages:

1. **No inheritance**: It uses a single generic class, eliminating the need for inheritance hierarchies[1].

2. **No casting**: The `Match` method allows you to handle both success and failure cases without casting[2].

3. **Type safety**: The generic parameter `T` ensures type safety for the success value[3].

** Usage Examples

Here's how you might use this simplified Result pattern:

```csharp
public Result<int> Divide(int a, int b)
{
    if (b == 0)
        return Result<int>.Fail("Cannot divide by zero");
    return Result<int>.Ok(a / b);
}

// Using the result
var result = Divide(10, 2);

var output = result.Match(
    success: value => $"Result: {value}",
    failure: error => $"Error: {error}"
);

Console.WriteLine(output);
```

if (result.IsSuccess)
{
    Console.WriteLine($"Result: {result.Value}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}

** Additional Features

You can extend this pattern with useful methods:

```csharp
public static class ResultExtensions
{
    public static Result<TNew> Map<T, TNew>(this Result<T> result, Func<T, TNew> mapper)
        => result.Match(
            success: value => Result<TNew>.Ok(mapper(value)),
            failure: error => Result<TNew>.Fail(error)
        );

    public static async Task<Result<T>> AsResultAsync<T>(this Task<T> task)
    {
        try
        {
            var result = await task;
            return Result<T>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex.Message);
        }
    }
}
```

These extensions allow for more functional-style programming and easier integration with asynchronous code[4].

** Benefits

This approach provides a balance between simplicity and functionality:

- **Reduced complexity**: No need for multiple classes or inheritance.
- **Type safety**: Maintains strong typing for success values.
- **Flexible usage**: The `Match` method allows for easy handling of both success and failure cases.
- **Extensible**: Can be easily extended with additional utility methods as needed.

By using this pattern, you can achieve clean error handling without the complexity of extensive class hierarchies or the need for frequent casting[5].

Citations:
[1] https://github.com/altmann/FluentResults
[2] https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/
[3] https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record?f1url=%3FappId%3DDev16IDEF1%26l%3DEN-US%26k%3Dk%28record_CSharpKeyword%29%3Bk%28DevLang-csharp%29%26rd%3Dtrue
[4] https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/inheritance
[5] https://www.reddit.com/r/csharp/comments/171wac9/return_one_or_another_type_in_a_function_does_c/
[6] https://stackoverflow.com/questions/8699053/how-to-check-if-a-class-inherits-another-class-without-instantiating-it
[7] https://stackoverflow.com/questions/7563269/find-only-non-inherited-interfaces/7563614
[8] https://www.youtube.com/watch?v=0wiJbCbNAKg

#endif
