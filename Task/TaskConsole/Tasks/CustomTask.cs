using System.Runtime.ExceptionServices;

namespace TaskConsole.Tasks;

public class CustomTask
{
    private bool _completed;
    private Exception? _error;
    private Action<CustomTask>? _continuation;
    private ExecutionContext? _executionContext;

    public void SetResult() => Complete(null);

    public void SetException(Exception error) => Complete(error);

    private void Complete(Exception? error)
    {
        lock (this)
        {
            if (_completed)
            {
                throw new InvalidOperationException("The task has been already complited.");
            }

            _error = error;
            _completed = true;

            if (_continuation is not null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (_executionContext is not null)
                    {
                        ExecutionContext.Run(_executionContext, _ => _continuation(this), null);
                    }
                    else
                    {
                        _continuation(this);
                    }
                });
            }
        }
    }

    public void ContinueWith(Action<CustomTask> action)
    {
        lock (this)
        {
            if (_completed)
            {
                ThreadPool.QueueUserWorkItem(_ => action(this));
            }
            else if (_continuation is not null)
            {
                throw new InvalidOperationException("This implementation only supports a single continuation.");
            }
            else
            {
                _continuation = action;
                _executionContext = ExecutionContext.Capture();
            }
        }
    }

    public void Wait()
    {
        ManualResetEventSlim? mres = null;

        lock (this)
        {
            if (!_completed)
            {
                mres = new ManualResetEventSlim();

                this.ContinueWith(_ => mres.Set());
            }
        }

        mres?.Wait();

        if (_error is not null)
        {
            ExceptionDispatchInfo.Throw(_error);
        }
    }

    public static CustomTask Run(Action action)
    {
        CustomTask task = new();

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                action();
                task.SetResult();
            }
            catch (Exception e)
            {
                task.SetException(e);
            }
        });

        return task;
    }

    public static CustomTask WhenAll(CustomTask task1, CustomTask task2)
    {
        int remaining = 2;
        Exception? exception = null;
        CustomTask task = new();        

        task1.ContinueWith(Continuation);
        task2.ContinueWith(Continuation);

        return task;

        void Continuation(CustomTask completedTask)
        {
            exception ??= completedTask._error;

            if (Interlocked.Decrement(ref remaining) == 0)
            {
                if (exception is not null)
                {
                    task.SetException(exception);
                }
                else
                {
                    task.SetResult();
                }
            }
        }
    }

    public static CustomTask Delay(TimeSpan delay)
    {
        CustomTask task = new();
        Timer timer = new(_ => task.SetResult());

        timer.Change(delay, Timeout.InfiniteTimeSpan);

        return task;
    }
}
