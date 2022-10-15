using System;
using System.IO;
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
    val code.txt - assembles, validates and gives memory stats for LMC assembly code.");
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