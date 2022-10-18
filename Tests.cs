namespace NetLMC;

public static class Test
{
    public static readonly Dictionary<string, Action<Interpreter.InterpreterState>> Tests = new()
    {
        { "Polynomial", TestAllPolynomials },
    };

    public static IEnumerator<Tester.ExpectedAction> Polynomial(int a, int b, int c, int x)
    {
        yield return Tester.In(a);
        yield return Tester.In(b);
        yield return Tester.In(c);
        yield return Tester.In(x);
        yield return Tester.Out(Math.Clamp(a + b*x + c*x*x, 0, 999));
    }

    public static void TestAllPolynomials(Interpreter.InterpreterState inState)
    {
        long totalPass = 0, totalFail = 0, totalCrash = 0;
        object ioLock = new();

        Parallel.For(0, 1000, (int x, ParallelLoopState parallelState) => 
        {
            long total = totalPass + totalFail + totalCrash;
            lock (ioLock)
            {
                Console.WriteLine($"Done: {total}, Pass: {totalPass}, Remain: {1_000_000_000_000 - total}");
            }

            long numPass = 0, numFail = 0, numCrash = 0;
            var state = inState;
            for (int a = 0; a < 1000; a++)
            {
                for (int b = 0; b < 1000; b++)
                {
                    for (int c = 0; c < 1000; c++)
                    {
                        state.pc = 0;
                        state.calc = 0;
                        var res = Tester.RunTest(Polynomial(a, b, c, x), ref state);
                        switch (res)
                        {
                            case Tester.Result.PASS:
                                numPass++;
                                break;
                            case Tester.Result.FAIL:
                                numFail++;
                                break;
                            case Tester.Result.CRASH:
                                numCrash++;
                                break;
                        }
                    }
                }
            }

            Interlocked.Add(ref totalPass, numPass);
            Interlocked.Add(ref totalFail, numFail);
            Interlocked.Add(ref totalCrash, numCrash);
        });

        Console.WriteLine($"Pass: {totalPass}, Fail: {totalFail}, Crash: {totalCrash}");
    }
}