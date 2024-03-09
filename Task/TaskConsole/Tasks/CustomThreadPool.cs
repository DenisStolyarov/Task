using System.Collections.Concurrent;

namespace TaskConsole.Tasks;

internal static class CustomThreadPool
{
    private static readonly BlockingCollection<(Action, ExecutionContext?)> s_workItems = [];

    static CustomThreadPool()
    {
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() =>
            {
                while (true)
                {
                    (Action action, ExecutionContext? ec) = s_workItems.Take();

                    if (ec is null)
                    {
                        action();
                    }
                    else
                    {
                        ExecutionContext.Run(ec, s => ((Action)s!)(), action);
                    }
                }
            })
            { IsBackground = true }.UnsafeStart();
        }
    }

    public static void QueueUserWorkItem(Action workItem)
    {
        s_workItems.Add((workItem, ExecutionContext.Capture()));
    }
}
