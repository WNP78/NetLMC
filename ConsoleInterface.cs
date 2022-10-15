namespace NetLMC;

using System;

public class ConsoleInterface : IInterface
{
    public int Input()
    {
        while (true)
        {
            Console.Write("Input> ");
            if (int.TryParse(Console.ReadLine(), out int v) && v >= 0 && v <= 999)
            {
                return v;
            }

            Console.WriteLine($"Invalid input, must be integer 0-999. Try again.");
        }
    }

    public void Output(int v)
    {
        Console.WriteLine($"Output: {v}");
    }

    public void DebugLog(string v)
    {
        Console.WriteLine(v);
    }
}