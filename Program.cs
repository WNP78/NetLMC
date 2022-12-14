using NetLMC;

if (args.Length == 0)
{
    Console.WriteLine("No arguments provided. Showing help");
    ShowHelp();
    return;
}

string command = args[0];

switch (command)
{
    case "val":
        if (args.Length != 2)
        {
            Console.WriteLine("val requires one argument");
        }

        Validate(args[1]);
        return;
    case "run":
        if (args.Length != 2)
        {
            Console.WriteLine("run requires one argument");
        }

        Run(args[1]);
        return;
    case "dbg":
        if (args.Length > 1)
            Debug(args[1]);
        else if (args.Length == 1)
            Debug(null);
        return;
    case "help":
        ShowHelp();
        return;
    case "test":
        if (args.Length == 2 && args[1].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            ListTests();
            return;
        }

        if (args.Length != 3)
        {
            Console.WriteLine("test requires two arguments");
        }

        RunTest(args[1], args[2]);
        return;
    case "testfile":
        if (args.Length != 3)
        {
            Console.WriteLine("testcase requires two arguments");
        }

        FileTest(args[1], args[2]);
        return;
    case "optimise":
        if (args.Length == 2)
        {
            Optimise(args[1], null);
        }
        else if (args.Length == 3)
        {
            Optimise(args[2], args[3]);
        }
        else
        { 
            Console.WriteLine("Wrong number of arguments (1-2) for optimise.");
        }
        return;
    default:
        Console.WriteLine($"Unknown command {command}");
        return;
}

void ShowHelp()
{
    Console.WriteLine(Help.CommandHelp);
}

void Optimise(string arg1, string arg2)
{
    Optimiser.Optimise(new(arg1), string.IsNullOrWhiteSpace(arg2) ? null : new(arg2));
}

void Validate(string arg)
{
    Assembler.AssemblerState state;
    ushort[] result;

    try
    {
        result = Assembler.Assemble(new FileInfo(arg), out state);
    }
    catch (Exception e)
    {
        Console.WriteLine("Assembly failed");
        Console.WriteLine($"Exception: {e}");
        return;
    }

    int nonZero = 0;
    for (int i = 0; i < result.Length; i++)
    {
        if (result[i] != 0)
        {
            nonZero++;
        }
    }

    Console.WriteLine("Assembled successfully.");
    Console.WriteLine($"{state.totalSize} boxes, {state.tags.Count} tags, {nonZero} non-zero cells");
}

void Run(string arg)
{
    Interpreter.InterpreterState state;

    try
    {
        state = Interpreter.LoadFromAssembler(Assembler.Assemble(new FileInfo(arg)));
    }
    catch (Exception e)
    {
        Console.WriteLine($"Assembly failed: {e}");
        return;
    }

    int steps = Interpreter.Run(ref state, new ConsoleInterface());

    Console.WriteLine($"Finished in {steps} steps");
}

void FileTest(string codeFilePath, string testFilePath)
{
    Interpreter.InterpreterState state;
    Assembler.AssemblerState asmState;
    FileInfo codeFile = new(codeFilePath);

    try
    {
        state = Interpreter.LoadFromAssembler(Assembler.Assemble(codeFile, out asmState));
    }
    catch (Exception e)
    {
        Console.WriteLine($"Assembly failed: {e}");
        return;
    }

    FileInfo testFile = new(testFilePath);
    if (!testFile.Exists)
    {
        Console.WriteLine($"Test file not found");
        return;
    }

    Console.WriteLine($"Testing {codeFile.Name} against {testFile.Name}");
    Console.WriteLine($"{asmState.totalSize} boxes, {asmState.tags.Count} tags");

    Test.RunTxtTests(state, testFile);
}

void ListTests()
{
    foreach (var test in Test.Tests.Keys)
    {
        Console.WriteLine(test);
    }
}

void RunTest(string test, string file)
{
    Interpreter.InterpreterState state;

    try
    {
        state = Interpreter.LoadFromAssembler(Assembler.Assemble(new FileInfo(file)));
    }
    catch (Exception e)
    {
        Console.WriteLine($"Assembly failed: {e}");
        return;
    }

    if (!Test.Tests.TryGetValue(test, out var testFunc))
    {
        Console.WriteLine($"No such test {test}");
        return;
    }

    testFunc(state);
}

void Debug(string arg)
{
    Interpreter.InterpreterState state;
    var iface = new ConsoleInterface();
    Assembler.AssemblerState debugInfo;

    if (arg == null)
    {
        state = default;
        debugInfo = new();
    }
    else
    {
        try
        {
            state = Interpreter.LoadFromAssembler(Assembler.Assemble(new FileInfo(arg), out debugInfo));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Assembly failed: {e}");
            return;
        }
    }

    Console.WriteLine(" == LMC DEBUGGER == ");
    Console.WriteLine("Type `help` for help.");

    Dictionary<string, Interpreter.InterpreterState> savedStates = new();

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine($"PC {state.pc:000}  CALC {state.calc:000}  {(state.nflag ? "NEGATIVE" : "")}");
        Console.WriteLine($"  next: {Assembler.Disassemble(state[state.pc], state.pc, debugInfo)}");
        Console.Write(">");
        var command = Console.ReadLine();
        var cmd = command.Trim().Split(' ');
        if (string.IsNullOrWhiteSpace(command))
        {
            bool cont = Interpreter.Step(ref state, iface);

            if (cont) continue;
            else break;
        }
        else if (cmd[0] == "run")
        {
            Interpreter.Run(ref state, iface);
            break;
        }
        else if (cmd[0] == "var" && cmd.Length == 2)
        {
            var tag = cmd[1].Trim();
            if (debugInfo.tags.ContainsKey(tag))
            {
                var addr = (uint)debugInfo.tags[tag];
                Console.WriteLine($"{addr:000} ({tag}) = {state[addr]}");
                continue;
            }

            Console.WriteLine($"No such tag {tag}");
        }
        else if (cmd[0] == "vars")
        {
            foreach (var kvp in debugInfo.tags)
            {
                var tag = kvp.Key;
                var addr = (uint)kvp.Value;
                Console.WriteLine($"{addr:000} ({tag}) = {state[addr]}");
            }
        }
        else if (cmd[0] == "runto" && cmd.Length == 2)
        {
            var target = cmd[1].Trim();
            if (!int.TryParse(target, out int breakpoint) && !debugInfo.tags.TryGetValue(target, out breakpoint))
            {
                Console.WriteLine($"Unknown breakpoint: {target}. Enter address or label.");
                continue;
            }

            Console.WriteLine($"Stepping to {breakpoint}");
            int exec = 0;
            bool end = false;
            while (true)
            {
                exec++;
                if (!Interpreter.Step(ref state, iface)) { end = true; break; }
                if (state.pc == breakpoint) { break; }
            }
            Console.WriteLine($"Stepped {exec} instructions");

            if (end) { break; }
            continue;
        }
        else if (cmd[0] == "s")
        {
            Console.WriteLine("Skip instruction");
            state.pc++;
            continue;
        }
        else if (cmd[0] == "br" && cmd.Length == 2)
        {
            var target = cmd[1].Trim();
            if (!int.TryParse(target, out int point) && !debugInfo.tags.TryGetValue(target, out point))
            {
                Console.WriteLine($"Unknown target: {target}. Enter address or label.");
                continue;
            }

            state.pc = (uint)point;
            continue;
        }
        else if (cmd[0] == "dumpstr")
        {
            Console.WriteLine(Interpreter.SaveToText(in state));
        }
        else if (cmd[0] == "loadstr")
        {
            Console.Write("Load>");
            string s = Console.ReadLine().Trim();
            using MemoryStream ms = new(300);

            {
                StreamWriter writer = new(ms, leaveOpen: true);
                writer.Write(s);
                writer.Flush();
                writer.Dispose();
                ms.Position = 0;
            }

            try
            {
                Interpreter.LoadFromText(new StreamReader(ms), out var newState);
                state = newState; // necessary so that state doesn't get corrupted if the string doesn't parse
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid input: {e}");
            }
        }
        else if (cmd[0] == "load" && cmd.Length == 2)
        {
            string key = cmd[1].Trim();
            if (savedStates.ContainsKey(key))
            {
                if (!savedStates.TryGetValue(key, out state))
                {
                    // this should never happen but if it does, then state has been overwritten but no saved state was found.
                    Console.WriteLine("Well I'll be damned. >:( Looks like ur state is messed up.");
                    continue;
                }

                Console.WriteLine($"Loaded stored state {key}");
            }
            else
            {
                Console.WriteLine($"No stored state found: {key}");
            }
        }
        else if (cmd[0] == "save" && cmd.Length == 2)
        {
            string key = cmd[1].Trim();
            savedStates[key] = state;
            Console.WriteLine($"Stored state as {key}.");
        }
        else if (cmd[0] == "export")
        {
            string fname;
            if (cmd.Length == 2)
            {
                fname = cmd[1];
            }
            else
            {
                Console.Write("Filename> ");
                fname = Console.ReadLine();
            }
            try
            {
                Interpreter.SaveToBin(new FileInfo(fname), in state);
            }
            catch (Exception e) { Console.WriteLine($"Failed to write: {e}"); }
        }
        else if (cmd[0] == "import")
        {
            string fname;
            if (cmd.Length == 2)
            {
                fname = cmd[1];
            }
            else
            {
                Console.Write("Filename> ");
                fname = Console.ReadLine();
            }
            var fi = new FileInfo(fname);
            if (!fi.Exists)
            {
                Console.WriteLine("File not found");
                continue;
            }
            try
            {
                state = Interpreter.LoadFromBin(fi);
            }
            catch (Exception e) { Console.WriteLine($"Failed to write: {e}"); }
        }
        else if (cmd[0] == "quit" || cmd[0] == "exit")
        {
            return;
        }
        else if (cmd[0] == "help")
        {
            Console.WriteLine(Help.DebuggerHelp);
        }
        else
        {
            Console.WriteLine($"Unknown command: {command}");
        }
    }

    Console.WriteLine($"Execution halted");
    Console.WriteLine($"PC {state.pc:000}  CALC {state.calc:000}  {(state.nflag ? "NEGATIVE" : "")}");
    Console.WriteLine($"  on: {Assembler.Disassemble(state[state.pc - 1], state.pc - 1, debugInfo)}");
}