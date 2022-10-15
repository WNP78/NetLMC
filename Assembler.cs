namespace NetLMC;
using System;
using System.Collections.Generic;
using System.IO;



public static class Assembler
{
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

    private struct AssemblerLine
    {
        public int fileLine;
        public string tag;
        public InputOp opcode;
        public string arg;

        public AssemblerLine(int fileLine, string text)
        {
            this.fileLine = fileLine;
            int index = text.IndexOf('\t');
            if (index != -1)
            {
                tag = text.Substring(0, index++);
            }
            else
            {
                index = 0;
                tag = null;
            }

            string op;
            int space = text.IndexOf(' ', index);
            if (space != -1)
            {
                arg = text.Substring(space + 1).Trim();

                if (string.IsNullOrWhiteSpace(arg)) { arg = null; }

                op = text.Substring(index, space);
            }
            else
            {
                arg = null;
                op = text.Substring(index);
            }

            opcode = Enum.Parse<InputOp>(op);
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
                InputOp.HLT => false,
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