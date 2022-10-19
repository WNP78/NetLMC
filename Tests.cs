namespace NetLMC;
using System.Diagnostics;

public static class Test
{
    public static readonly Dictionary<string, Action<Interpreter.InterpreterState>> Tests = new()
    {
        { "Polynomial", TestAllPolynomials },
    };

    public static int EvalPolynomial(int a, int b, int c, int x) => Math.Clamp(a + b*x + c*x*x, 0, 999);

    public static IEnumerator<Tester.ExpectedAction> Polynomial(int a, int b, int c, int x)
    {
        yield return Tester.In(a);
        yield return Tester.In(b);
        yield return Tester.In(c);
        yield return Tester.In(x);
        yield return Tester.Out(EvalPolynomial(a,b,c,x));
    }

    public static void TestAllPolynomials(Interpreter.InterpreterState inState)
    {
        long totalPass = 0, totalFail = 0, totalCrash = 0, total = 0;
        object ioLock = new();

        var fails = new (int a, int b, int c, int x)[1024];
        int failIndex = 0;

        var w = Stopwatch.StartNew();

        Parallel.For(0, 1_000_000, (int i, ParallelLoopState parallelState) => 
        {
            long numPass = 0, numFail = 0, numCrash = 0;
            for (int j = 0; j < 1000; j++)
            {
                long total = totalPass + totalFail + totalCrash;
                var rand = new Random(i * 1000 + j);

                var state = inState;

                int a = rand.Next(0, 999),
                    b = rand.Next(0, 999),
                    c = rand.Next(0, 999),
                    x = rand.Next(0, 999);

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
                            int failIdx = Interlocked.Increment(ref failIndex);
                            if (failIdx < fails.Length) fails[failIdx] = (a,b,c,x);
                            lock (ioLock)
                            {
                                Console.WriteLine($"{a},{b},{c},{x}: {EvalPolynomial(a,b,c,x)}");
                            }
                            break;
                        case Tester.Result.CRASH:
                            numCrash++;
                            break;
                    }
                }
            }

            Interlocked.Add(ref totalPass, numPass);
            Interlocked.Add(ref totalFail, numFail);
            Interlocked.Add(ref totalCrash, numCrash);
            if (Interlocked.Add(ref total, 1000) % 1_000_000 == 0)
            {
                lock (ioLock)
                {
                    Console.WriteLine($"Done: {total}, Fail: {totalFail}, Pass: {totalPass}, Remain: {1_000_000_000 - total}");
                }
            }
        });

        w.Stop();

        Console.WriteLine($"Done in {w.Elapsed}");
        Console.WriteLine($"Pass: {totalPass}, Fail: {totalFail}, Crash: {totalCrash}");
    }
}