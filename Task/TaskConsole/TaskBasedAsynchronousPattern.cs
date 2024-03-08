#define SWITCH

using System.Runtime.CompilerServices;

namespace TaskConsole;

public static class TaskBasedAsynchronousPattern
{
    public static async Task CopyStreamToStreamAsync(Stream source, Stream destination)
    {
        var buffer = new byte[0x1000];
        int numRead;

        while ((numRead = await source.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            await destination.WriteAsync(buffer, 0, numRead);
        }
    }

    [AsyncStateMachine(typeof(CopyStreamToStreamAsyncStateMachine))]
    public static Task CompiledCopyStreamToStreamAsync(Stream source, Stream destination)
    {
        CopyStreamToStreamAsyncStateMachine stateMachine = default;

        stateMachine.builder = AsyncTaskMethodBuilder.Create();
        stateMachine.source = source;
        stateMachine.destination = destination;
        stateMachine.state = -1;
        stateMachine.builder.Start(ref stateMachine);

        return stateMachine.builder.Task;
    }

    private struct CopyStreamToStreamAsyncStateMachine : IAsyncStateMachine
    {
        public int state;
        public AsyncTaskMethodBuilder builder;
        public Stream source;
        public Stream destination;

        private byte[]? buffer;
        private int numRead;
        private int s;
        private TaskAwaiter localAwaiter1;
        private TaskAwaiter<int> localAwaiter2;

        public void MoveNext()
        {
            TaskAwaiter<int> awaiter1;
            TaskAwaiter awaiter2;

            int num = state;

            try
            {
                if (num != 0)
                {
                    if (num != 1)
                    {
                        buffer = new byte[4096];

                        goto STATE_N_1_START_READ;
                    }

                    awaiter1 = localAwaiter2;
                    localAwaiter2 = default;

                    num = (state = -1);

                    goto STATE_1_GET_READ_RESULT;

                }

                goto STATE_0_RESTORE_WRITE;


            STATE_1_START_WRITE:

                awaiter2 = destination.WriteAsync(buffer, 0, numRead).GetAwaiter();

                if (!awaiter2.IsCompleted)
                {
                    num = (state = 0);
                    localAwaiter1 = awaiter2;
                    CopyStreamToStreamAsyncStateMachine stateMachine = this;

                    builder.AwaitUnsafeOnCompleted(ref awaiter2, ref stateMachine);

                    goto Exit;
                }
                else
                {
                    goto STATE_1_GET_WRITE_RESULT;
                }

            STATE_0_RESTORE_WRITE:

                num = (state = -1);
                awaiter2 = localAwaiter1;
                localAwaiter1 = default;

            STATE_1_GET_WRITE_RESULT:

                awaiter2.GetResult();

            STATE_N_1_START_READ:

                awaiter1 = source.ReadAsync(buffer, 0, buffer.Length).GetAwaiter();

                if (!awaiter1.IsCompleted)
                {
                    num = (state = 1);

                    localAwaiter2 = awaiter1;
                    CopyStreamToStreamAsyncStateMachine stateMachine = this;

                    builder.AwaitUnsafeOnCompleted(ref awaiter1, ref stateMachine);

                    goto Exit;
                }

            STATE_1_GET_READ_RESULT:

                s = awaiter1.GetResult();

                if ((numRead = s) != 0)
                {
                    goto STATE_1_START_WRITE;
                }
            }
            catch (Exception exception)
            {
                state = -2;
                buffer = null;
                builder.SetException(exception);

                goto Exit;
            }

            state = -2;
            buffer = null;
            builder.SetResult();

        Exit:

            return;
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);

#if SWITCH

        private State currentState;

        private enum State
        {
            Initial,
            Write,
            Write_Restore,
            Write_Result,
            Read,
            Read_Restore,
            Read_Result,
            Completed,
        }

        public void MoveNextSwitch()
        {
            TaskAwaiter<int> awaiter1 = default;
            TaskAwaiter awaiter2 = default;

            try
            {
                while (true)
                {
                    switch (currentState)
                    {
                        case State.Initial:
                            buffer = new byte[4096];

                            currentState = State.Read;

                            break;
                        case State.Write:
                            awaiter2 = destination.WriteAsync(buffer, 0, numRead).GetAwaiter();

                            if (!awaiter2.IsCompleted)
                            {
                                currentState = State.Write_Restore;

                                localAwaiter1 = awaiter2;
                                CopyStreamToStreamAsyncStateMachine stateMachine = this;

                                builder.AwaitUnsafeOnCompleted(ref awaiter2, ref stateMachine);

                                return;
                            }

                            currentState = State.Write_Result;

                            break;
                        case State.Write_Restore:

                            awaiter2 = localAwaiter1;
                            localAwaiter1 = default;

                            currentState = State.Write_Result;

                            break;
                        case State.Write_Result:
                            awaiter2.GetResult();

                            currentState = State.Read;

                            break;
                        case State.Read:
                            awaiter1 = source.ReadAsync(buffer, 0, buffer.Length).GetAwaiter();

                            if (!awaiter1.IsCompleted)
                            {
                                currentState = State.Read_Restore;

                                localAwaiter2 = awaiter1;
                                CopyStreamToStreamAsyncStateMachine stateMachine = this;

                                builder.AwaitUnsafeOnCompleted(ref awaiter1, ref stateMachine);

                                return;
                            }

                            currentState = State.Read_Result;

                            break;

                        case State.Read_Restore:

                            awaiter1 = localAwaiter2;
                            localAwaiter2 = default;

                            currentState = State.Read_Result;

                            break;
                        case State.Read_Result:
                            numRead = awaiter1.GetResult();

                            if (numRead != 0)
                            {
                                currentState = State.Write;

                                break;
                            }

                            currentState = State.Completed;

                            break;
                        case State.Completed:

                            state = -2;
                            buffer = null;
                            builder.SetResult();

                            return;
                    }
                }
            }
            catch (Exception exception)
            {
                state = -2;
                buffer = null;
                builder.SetException(exception);
            }
        }

#endif

    }
}
