namespace NetLMC;

public static class Tester
{
    public enum Result
    {
        PASS,
        FAIL,
        CRASH,
    };

    public struct ExpectedAction
    {
        public bool isInput;
        public int value;
    }

    [Serializable]
    private class TesterException : System.Exception
    {
        public TesterException() { }
        public TesterException(string message) : base(message) { }
        public TesterException(string message, System.Exception inner) : base(message, inner) { }
        protected TesterException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    private class ValidatorInterface : IInterface
    {
        private readonly IEnumerator<ExpectedAction> actions;

        public ValidatorInterface(IEnumerable<ExpectedAction> action)
        {
            this.actions = action.GetEnumerator();
        }

        public ValidatorInterface(IEnumerator<ExpectedAction> action)
        {
            this.actions = action;
        }

        public int Input()
        {
            if (!actions.MoveNext() || !actions.Current.isInput) { throw new TesterException("Unexpected input request."); }

            return actions.Current.value;
        }

        public void Output(int v)
        {
            if (!actions.MoveNext() || actions.Current.isInput) { throw new TesterException("Unexpected output"); }

            if (actions.Current.value != v) { throw new TesterException($"Output {v} != expected {actions.Current.value}"); }
        }

        public void DebugLog(string _) { }

        public void CheckDone()
        {
            if (actions.MoveNext()) { throw new TesterException("Unexpected end of program."); }
        }
    }

    public static Result RunTest(IEnumerator<ExpectedAction> actions, ref Interpreter.InterpreterState state)
    {
        var iface = new ValidatorInterface(actions);
        try
        {
            Interpreter.Run(ref state, iface);
            iface.CheckDone();
        }
        catch (TesterException)
        {
            return Result.FAIL;
        }
        catch
        {
            return Result.CRASH;
        }

        return Result.PASS;
    }

    public static ExpectedAction In(int v) => new() { isInput = true, value = v };
    public static ExpectedAction Out(int v) => new() { isInput = false, value = v };
}