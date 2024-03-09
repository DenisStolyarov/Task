using TaskConsole.Tasks;

namespace TaskConsole;

public static class CustomTaskBasedAsynchronousPattern
{
    public static async CustomTask Run()
    {
        CustomTask task_1 = Calcualte(1_000);
        CustomTask task_2 = Calcualte(100);

        await CustomTask.WhenAll(task_1, task_2);
    }

    public static async CustomTask Calcualte(int count)
    {
        long sum = 0;

        for (int i = 0; i < count; i++)
        {
            sum += i;
        }

        await CustomTask.Delay(TimeSpan.FromMilliseconds(count));

        Console.WriteLine($"{count}:{sum}");
    }
}
