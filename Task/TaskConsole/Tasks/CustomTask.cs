using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace TaskConsole.Tasks;

[AsyncMethodBuilder(typeof(CustomTaskMethodBuilder))]
public class CustomTask
{
    private bool _completed;
    private Exception? _error;
    private Action<CustomTask>? _continuation;
    private ExecutionContext? _executionContext;

    public static readonly CustomTask CompletedTask = new();

    internal CustomTask() { }

    public bool IsCompleted => _completed;

    public CustomTaskAwaiter GetAwaiter() => new() { _task = this };

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

    public struct CustomTaskAwaiter : ICriticalNotifyCompletion
    {
        internal CustomTask _task;

        public bool IsCompleted => _task.IsCompleted;

        public void OnCompleted(Action continuation) => _task.ContinueWith(_ => continuation());

        public void UnsafeOnCompleted(Action continuation) => _task.ContinueWith(_ => continuation());

        public void GetResult() => _task.Wait();
    }
}

public struct CustomTaskMethodBuilder
{
    private CustomTask task;

    public CustomTask Task => task ?? InitializeTaskAsPromise();

    private CustomTask InitializeTaskAsPromise() => task = new CustomTask();

    public static CustomTaskMethodBuilder Create() => default;

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        ArgumentNullException.ThrowIfNull(nameof(stateMachine));

        Thread currentThread = Thread.CurrentThread;
        ExecutionContext? previousExecutionCtx = currentThread.ExecutionContext;

        try
        {
            stateMachine.MoveNext();
        }
        finally
        {
            ExecutionContext? currentExecutionCtx = currentThread.ExecutionContext;

            if (previousExecutionCtx != currentExecutionCtx)
            {
                ExecutionContext.Restore(previousExecutionCtx);
            }
        }
    }

    [Obsolete]
    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        // SetStateMachine was originally needed in order to store the boxed state machine reference into
        // the boxed copy. Now that a normal box is no longer used, SetStateMachine is also legacy.
    }

    public void SetResult()
    {
        if (task is null)
        {
            task = CustomTask.CompletedTask;
        }
        else
        {
            task.SetResult();
        }
    }

    public void SetException(Exception exception)
    {
        task.SetException(exception);
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    { }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        IAsyncStateMachineBox box = GetStateMachineBox(ref stateMachine, ref task);

        awaiter.UnsafeOnCompleted(box.MoveNextAction);
    }

    private static IAsyncStateMachineBox GetStateMachineBox<TStateMachine>(
            ref TStateMachine stateMachine,
            ref CustomTask? taskField)
            where TStateMachine : IAsyncStateMachine
    {
        ExecutionContext? currentContext = ExecutionContext.Capture();

        if (taskField is AsyncStateMachineBox<IAsyncStateMachine> typedBox)
        {
            // If this is the first await, we won't yet have a state machine, so store it.
            typedBox.StateMachine ??= stateMachine;
            typedBox.Context = currentContext;

            return typedBox;
        }

        AsyncStateMachineBox<TStateMachine> box = new AsyncStateMachineBox<TStateMachine>();

        taskField = box; // important: this must be done before storing stateMachine into box.StateMachine!
        box.StateMachine = stateMachine;
        box.Context = currentContext;

        return box;
    }

    internal interface IAsyncStateMachineBox
    {
        void MoveNext();

        Action MoveNextAction { get; }

        void ClearStateUponCompletion();
    }

    private class AsyncStateMachineBox<TStateMachine> :
        CustomTask, IAsyncStateMachineBox
        where TStateMachine : IAsyncStateMachine
    {
        private Action? _moveNextAction;

        public TStateMachine? StateMachine;
        public ExecutionContext? Context;

        private static readonly ContextCallback callback = s =>
            Unsafe.As<AsyncStateMachineBox<TStateMachine>>(s).StateMachine!.MoveNext();

        public Action MoveNextAction => _moveNextAction ??= new Action(MoveNext);

        public void MoveNext()
        {
            ExecutionContext? context = Context;

            if (context == null)
            {
                StateMachine.MoveNext();
            }
            else
            {
                ExecutionContext.Run(context, callback, this);
            }

            if (IsCompleted)
            {
                ClearStateUponCompletion();
            }
        }

        public void ClearStateUponCompletion()
        {
            StateMachine = default;
            Context = default;
        }
    }
}
