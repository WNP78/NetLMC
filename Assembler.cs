namespace NetLMC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class Assembler
{
    private static Regex lineExpression = new Regex(@"^(\S*)\t\s*([A-z]{1,3})\s*([\S]*)");

    public static ushort[] Assemble(FileInfo file) => Assemble(file, out _);

    public static ushort[] Assemble(FileInfo file, out AssemblerState state)
    {
        if (!file.Exists) { throw new FileNotFoundException(file.FullName); }

        state = new AssemblerState();
        List<AssemblerLine> lines = new List<AssemblerLine>(ParseFile(file, state));

        ushort[] res = new ushort[100];
        int i = 0;
        foreach (var compiled in AssembleLines(lines, state))
        {
            res[i++] = compiled;
        }

        return res;
    }

    public static string Disassemble(int code)
    {
        string op = LookupOp(code, out var arg);

        if (op == null) { return $"??? {code}"; }

        op = op.PadRight(3, ' ');
        if (arg.HasValue) { return $"{op} {arg.Value}"; }
        return op;
    }

    public static string LookupOp(int code, out int? arg)
    {
        if (code == (int)InputOp.IN || code == (int)InputOp.OUT)
        {
            arg = null;
            return ((InputOp)code).ToString();
        }
        else if (code == 0)
        {
            arg = null;
            return InputOp.HLT.ToString();
        }
        else
        {
            arg = code % 100;
            string v = Enum.GetName((InputOp)code - arg.Value);
            if (v != null)
            {
                return v;
            }

            arg = code;
            return null;
        }
    }

    public static string LookupTag(int addr, AssemblerState state)
    {
        return state.tags.Where(kvp => kvp.Value == addr).Select(kvp => kvp.Key).FirstOrDefault();
    }

    public static string Disassemble(int code, int addr, AssemblerState debugState)
    {
        //string dasm = Disassemble(code);
        string op = LookupOp(code, out var arg) ?? "???";
        string tag1 = LookupTag(addr, debugState) ?? string.Empty;

        if (!arg.HasValue)
            return $"[{addr:00}] {tag1.PadRight(10, ' ')} {op}";

        string tag2 = LookupTag(arg.Value, debugState) ?? string.Empty;
        return $"[{addr:00}] {tag1.PadRight(10, ' ')} {op} {tag2} ({arg:00})";
    }

    private struct AssemblerLine
    {
        public int fileLine;
        public string tag;
        public InputOp opcode;
        public string arg;

        public AssemblerLine(int fileLine, string text)
        {
            this.fileLine = fileLine;

            var match = lineExpression.Match(text);
            if (!match.Success)
            {
                throw new InvalidDataException($"Could not parse line instruction `{text}`");
            }

            if (!Enum.TryParse<InputOp>(match.Groups[2].Value, out this.opcode))
            {
                throw new InvalidDataException($"Unknown opcode {match.Groups[2].Value} on line {fileLine}");
            }

            this.tag = match.Groups[1].Value;
            this.arg = match.Groups[3].Value;

            if (string.IsNullOrWhiteSpace(this.tag)) { this.tag = null; }
            if (string.IsNullOrWhiteSpace(this.arg)) { this.arg = null; }
        }
    }

    public class AssemblerState
    {
        public int totalSize;

        public Dictionary<string, int> tags;

        public AssemblerState()
        {
            totalSize = 0;
            tags = new Dictionary<string, int>();
        }
    }

    private static IEnumerable<AssemblerLine> ParseFile(FileInfo file, AssemblerState state)
    {
        int lineNo = 0;
        foreach (var txt in File.ReadLines(file.FullName))
        {
            lineNo++;

            string toParse = txt;

            int comment = txt.IndexOf('#');
            if (comment != -1)
            {
                toParse = toParse.Substring(0, comment);
            }

            if (string.IsNullOrWhiteSpace(toParse)) { continue; }

            toParse = toParse.Trim();

            if (state.totalSize >= 100)
            {
                Console.WriteLine("Warning: Program Truncated, over 100 boxes");
                yield break;
            }

            AssemblerLine line;
            try
            {
                line = new AssemblerLine(lineNo, txt);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Parsing failed on line {lineNo}:\n{txt}\nException: {e}");
            }

            if (line.tag != null)
            {
                if (state.tags.ContainsKey(line.tag))
                {
                    throw new InvalidDataException($"Duplicate tag {line.tag} on line {lineNo}");
                }
                
                state.tags.Add(line.tag, state.totalSize);
            }

            state.totalSize++;

            yield return line;
        }
    }

    private static IEnumerable<ushort> AssembleLines(IEnumerable<AssemblerLine> input, AssemblerState state)
    {
        foreach (AssemblerLine line in input)
        {
            int op = (int)line.opcode;
            bool requiresArg = line.opcode switch {
                InputOp.HLT => line.arg != null, // if an arg is supplied, we assume DAT
                InputOp.IN => false,
                InputOp.OUT => false,
                _ => true,
            };

            if (requiresArg)
            {
                if (line.arg == null) { throw new InvalidDataException($"Line {line.fileLine}, op {line.opcode} requires an argument but none is provided."); }

                if (line.opcode == InputOp.DAT)
                {
                    if (ushort.TryParse(line.arg, out ushort val))
                    {
                        if (val > 999) { throw new InvalidDataException($"DAT on line {line.fileLine} is out of range ({val} > 999)"); }

                        yield return val;
                        continue;
                    }
                }
                else if (state.tags.TryGetValue(line.arg, out int tagLine))
                {
                    op += tagLine;
                }
                else
                {
                    throw new InvalidDataException($"Tag {line.arg} used on line {line.fileLine} not defined.");
                }
            }

            yield return (ushort)op;
        }
    }

    public enum InputOp
    {
        HLT = 0,
        DAT = 0,
        ADD = 100,
        SUB = 200,
        STO = 300,
        LDA = 500,
        BR = 600,
        BRZ = 700,
        BRP = 800,
        IN = 901,
        OUT = 902,
    }
}