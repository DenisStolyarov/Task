namespace TaskConsole.Iterators;

public static class AsyncIterator
{
    public static Task CopyStreamToStreamAsync(Stream source, Stream destination)
    {
        return IterateAsync(Iterate(source, destination));

        static IEnumerable<Task> Iterate(Stream source, Stream destination)
        {
            var buffer = new byte[0x1000];

            while (true)
            {
                Task<int> read = source.ReadAsync(buffer, 0, buffer.Length);

                yield return read;

                int numRead = read.Result;

                if (numRead <= 0)
                {
                    break;
                }

                Task write = destination.WriteAsync(buffer, 0, numRead);

                yield return write;

                write.Wait();
            }
        }
    }

    private static Task IterateAsync(IEnumerable<Task> tasks)
    {
        TaskCompletionSource tcs = new();

        IEnumerator<Task> e = tasks.GetEnumerator();

        Process();

        return tcs.Task;

        void Process()
        {
            try
            {
                if (e.MoveNext())
                {
                    e.Current.ContinueWith(t => Process());

                    return;
                }
            }
            catch (Exception e)
            {
                tcs.SetException(e);

                return;
            }

            tcs.SetResult();
        }
    }
}
