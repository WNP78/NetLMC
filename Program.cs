using System;
using System.IO;
using System.Linq;
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
        if (args.Length != 2)
        {
            Console.WriteLine("dbg requires one argument");
        }

        Debug(args[1]);
        return;
    case "help":
        ShowHelp();
        return;
    default:
        Console.WriteLine($"Unknown command {command}");
        return;
}

void ShowHelp()
{
    Console.WriteLine(@"Commands list:
    help - shows this message
    val code.txt - assembles, validates and gives memory stats for LMC assembly code.
    run code.txt - assembles and runs code
    dbg code.txt - assembles, runs, and debugs code");
}

void Validate(string arg)
{
    Assembler.AssemblerState state;

    try
    {
        Assembler.Assemble(new FileInfo(arg), out state);
    }
    catch (Exception e)
    {
        Console.WriteLine("Assembly failed");
        Console.WriteLine($"Exception: {e}");
        return;
    }

    Console.WriteLine("Assembled successfully.");
    Console.WriteLine($"{state.totalSize} boxes, {state.tags.Count} tags");
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

    Interpreter.Run(ref state, new ConsoleInterface());
}

void Debug(string arg)
{
    Interpreter.InterpreterState state;
    var iface = new ConsoleInterface();
    Assembler.AssemblerState debugInfo;

    try
    {
        state = Interpreter.LoadFromAssembler(Assembler.Assemble(new FileInfo(arg), out debugInfo));
    }
    catch (Exception e)
    {
        Console.WriteLine($"Assembly failed: {e}");
        return;
    }

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine($"PC {state.pc:000}  CALC {state.calc:000}  {(state.nflag ? "NEGATIVE" : "")}");
        Console.WriteLine($"  next: {Assembler.Disassemble(state.GetMem(state.pc), state.pc, debugInfo)}");
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
                var addr = debugInfo.tags[tag];
                Console.WriteLine($"{addr:000} ({tag}) = {state.GetMem(addr)}");
                continue;
            }

            Console.WriteLine($"No such tag {tag}");
        }
        else if (cmd[0] == "vars")
        {
            foreach (var kvp in debugInfo.tags)
            {
                var tag = kvp.Key;
                var addr = kvp.Value;
                Console.WriteLine($"{addr:000} ({tag}) = {state.GetMem(addr)}");
            }
        }
        else if (cmd[0] == "runto" && cmd.Length == 2)
        {
            var target = cmd[1].Trim();
            int breakpoint;
            if (!int.TryParse(target, out breakpoint) && !debugInfo.tags.TryGetValue(target, out breakpoint))
            {
                Console.WriteLine($"Unknown breakpoint: {target}. Enter address or label.");
                continue;
            }

            while (Interpreter.Step(ref state, iface) && state.pc != breakpoint) { }
            break;
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
            int point;
            if (!int.TryParse(target, out point) && !debugInfo.tags.TryGetValue(target, out point))
            {
                Console.WriteLine($"Unknown target: {target}. Enter address or label.");
                continue;
            }

            state.pc = point;
            continue;
        }
        else
        {
            Console.WriteLine($"Unknown command: {command}");
        }
    }

    Console.WriteLine("Execution halted.");
}