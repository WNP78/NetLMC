namespace NetLMC;
using System.Diagnostics;

public static class Test
{
    public static readonly Dictionary<string, Action<Interpreter.InterpreterState>> Tests = new()
    {
        { "Polynomial", TestAllPolynomials },
    };

    public static int EvalPolynomial(int a, int b, int c, int x) => Math.Clamp(a + b * x + c * x * x, 0, 999);

    public static IEnumerator<Tester.ExpectedAction> Polynomial(int a, int b, int c, int x)
    {
        yield return Tester.In(a);
        yield return Tester.In(b);
        yield return Tester.In(c);
        yield return Tester.In(x);
        yield return Tester.Out(EvalPolynomial(a, b, c, x));
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
                            if (failIdx < fails.Length) fails[failIdx] = (a, b, c, x);
                            lock (ioLock)
                            {
                                Console.WriteLine($"{a},{b},{c},{x}: {EvalPolynomial(a, b, c, x)}");
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

    public static void RunTxtTests(Interpreter.InterpreterState inState, FileInfo testFile)
    {
        using var f = File.OpenRead(testFile.FullName);
        var sr = new StreamReader(f);
        string l;
        bool allPassed = false;
        while ((l = sr.ReadLine()) != null)
        {
            l = l.Trim();
            if (!string.IsNullOrEmpty(l))
            {
                bool passed = RunTxtTest(l, ref inState, out string testName, out string msg);
                Console.WriteLine($"{testName,-12}  {(passed ? "PASS" : "FAIL")}: {msg}");
                allPassed |= passed;
            }
        }

        if (allPassed) Console.WriteLine("All tests passed");
        else Console.WriteLine("Some tests failed");
    }

    private static bool RunTxtTest(string test, ref Interpreter.InterpreterState state, out string testName, out string msg)
    {
        string[] flds = test.Trim().Split(';');

        if (flds.Length != 4)
        {
            msg = $"Unable to read test";
            testName = "N/A";
            return false;
        }

        testName = flds[0];
        int[] inputs, outputs;
        int maxInstr;

        try
        {
            inputs = flds[1].Trim().Split(',').Select(x => int.Parse(x)).ToArray();
            outputs = flds[2].Trim().Split(',').Select(x => int.Parse(x)).ToArray();
            maxInstr = int.Parse(flds[3]);
        }
        catch (Exception e)
        {
            msg = $"Unable to read test: {e}";
            return false;
        }

        var iface = new TxtTestInterface(inputs, outputs);
        
        try
        {
            state.Reset();
            int steps = Interpreter.Run(ref state, iface, maxInstr);
            iface.Done();
            msg = $"{steps} steps used.";
            return true;
        }
        catch (TxtTestInterface.TestException exc)
        {
            msg = exc.Message;
        }
        catch (Exception e)
        {
            msg = $"Execution failed on instruction {state.pc}. Inner exception: {e}";
        }

        return false;
    }

    private class TxtTestInterface : IInterface
    {
        private readonly int[] inputs, outputs;
        private int inIdx, outIdx;

        public TxtTestInterface(int[] ins, int[] outs)
        {
            inputs = ins;
            outputs = outs;
        }

        void IInterface.DebugLog(string v) => Console.WriteLine($"DEBUG: {v}");
        int IInterface.Input() 
        {
            int idx = inIdx++;
            if (idx < inputs.Length) return inputs[idx];
            throw new InputException(true);
        } 

        void IInterface.Output(int s)
        {
            int idx = outIdx++;
            if (idx < inputs.Length) AssertEqual(s, outputs[idx]);
            else throw new OutputException(true);
        } 

        public void Done()
        {
            if (inIdx != inputs.Length) throw new InputException(false);
            if (outIdx != outputs.Length) throw new OutputException(false);
        }

        private static void AssertEqual(int got, int expected)
        {
            if (got != expected) throw new WrongOutputException(got, expected);
        }

        public abstract class TestException : Exception
        {
            public TestException(string message) : base(message) { }
        }

        public class WrongOutputException : TestException
        {
            public WrongOutputException(int got, int expected) : base($"Got output {got} when expecting {expected}") { }
        }

        public class InputException : Exception
        {
            public InputException(bool overflow) : base($"Program requested too {(overflow ? "many" : "few")} inputs") { }
        }

        public class OutputException : Exception
        {
            public OutputException(bool overflow) : base($"Program gave too {(overflow ? "many" : "few")} outputs") { }
        }
    }
}