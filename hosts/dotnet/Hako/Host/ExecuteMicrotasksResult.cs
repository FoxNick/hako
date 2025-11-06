using HakoJS.Exceptions;
using HakoJS.VM;

namespace HakoJS.Host;

public class ExecuteMicrotasksResult
{
    private ExecuteMicrotasksResult(bool isSuccess, int microtasksExecuted, JSValue? error, Realm? errorContext)
    {
        IsSuccess = isSuccess;
        MicrotasksExecuted = microtasksExecuted;
        Error = error;
        ErrorContext = errorContext;
    }

    private bool IsSuccess { get; init; }
    public int MicrotasksExecuted { get; init; }
    private JSValue? Error { get; init; }
    private Realm? ErrorContext { get; init; }

    public static ExecuteMicrotasksResult Success(int microtasksExecuted)
    {
        return new ExecuteMicrotasksResult(true, microtasksExecuted, null, null);
    }

    public static ExecuteMicrotasksResult Failure(JSValue error, Realm context)
    {
        return new ExecuteMicrotasksResult(false, 0, error, context);
    }


    public void EnsureSuccess()
    {
        if (!IsSuccess)
        {
            var error = ErrorContext!.GetLastError(Error!.GetHandle());
            throw new HakoException("Event loop error", error);
        }
    }
}