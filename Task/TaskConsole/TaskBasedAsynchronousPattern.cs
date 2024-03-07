namespace TaskConsole;

public static class TaskBasedAsynchronousPattern
{
    public static async Task CopyStreamToStreamAsync(Stream source, Stream destination)
    {
        var buffer = new byte[0x1000];
        int numRead;

        while ((numRead = await source.ReadAsync(buffer)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, numRead));
        }
    }
}
