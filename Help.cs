namespace NetLMC;

public static class Help
{
    public const string CommandHelp = @"Commands list:
    help - shows this message
    val code.txt - assembles, validates and gives memory stats for LMC assembly code.
    run code.txt - assembles and runs code
    dbg - opens debugger on empty state
    dbg code.txt - assembles, runs, and debugs code
    test testname code.txt - runs builtin test testname on code.txt
    test list - lists builtin tests
    testfile code.txt testfile.txt - runs LMinC standard test file testfile.txt on code.txt
    optimise in.txt [out.txt] - 'pack' LMC assembly file by not defining trailing zero DATs";

    public const string DebuggerHelp = @"Debugger commands:
    <enter> - emtpy input steps forward one instruction
    run - run the program until it ends
    var <var> - prints the value of <var> which can be a label or address
    vars - prints the names and values of all labels
    runto <point> - runs the program until the breakpoint is reached, which can be a label or address
    s - skips the current instruction, incrementing the PC without fetch-executing
    br <target> - jumps (sets the PC) to a target label or address
    dumpstr - output the LMC state to a human readable string
    loadstr - load the LMC state from a human readable string
    save <name> - temporarily save state in memory as name. Persists only to the end of the debug session
    load <name> - loads the temporarily saved state 'name' from memory
    export [file] - saves the LMC state in binary to a file
    import [file] - loads the LMC state in binary from a file
    quit/exit - exits the application
    help - shows this message

Debugger View:
    First line shows the value of the PC and the CALC
    Second line shows a dissasembly of the instruction at the PC (next instruction to be executed)
";

    public const string OptimiserNotice = @"##################################
# NOTICE - File optimised by automated tool
#   This file has been optimised to not define uninitialised variables at the end of the program
#   This has decreased the size of the compiled program by {0} boxes
#   This allows us to use the whole memory of the LMC wihtout inflating our program data with empty fields that serve no purpose at runtime, only as labels at compile time
#   Instructions that referred to those boxes have been rewritten using the DAT opcode to directly insert the correct machine code.
#   The original instruction text is included in a comment directly after the optimised instruction for readability
#   Variables that were optimised out are prefixed with `# OMITTED #`, but the text remains for readability
#   Full code for the optimiser (and other utilities) are available on my GitHub: https://github.com/WNP78/NetLMC
##################################

";
}