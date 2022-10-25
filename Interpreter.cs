namespace NetLMC;
using System;
using System.IO;
using System.Text;

public static class Interpreter
{
    public enum OpDigits
    {
        HLT = 0,
        ADD = 1,
        SUB = 2,
        STO = 3,
        LDA = 5,
        BR = 6,
        BRZ = 7,
        BRP = 8,
        IO = 9,
    }

    public unsafe struct InterpreterState
    {
        public const int MemorySize = 100;

        public int calc;
        public uint pc;
        public bool nflag;
        private fixed ushort memory[MemorySize];

        public unsafe ref ushort this[uint i] => ref memory[i % MemorySize];

        public int Calculator
        {
            get => calc;
            set
            {
                if (value >= 0)
                {
                    nflag = false;
                    value %= 1000;
                }
                else
                {
                    nflag = true;
                    do
                    {
                        value += 1000;
                    } while (value < 0);
                }

                calc = value;
            }
        }

        public void Reset()
        {
            this.pc = 0;
            this.calc = 0;
        }
    }

    public static bool IsIO(in InterpreterState state)
    {
        ushort instr = state[state.pc];
        return (instr == 901) || (instr == 902);
    }

    public static bool Step(ref InterpreterState state, IInterface iface)
    {
        ushort instr = state[state.pc++];

        OpDigits op = (OpDigits)(instr / 100);

        ushort arg = (ushort)(instr % 100);

        switch (op)
        {
            case OpDigits.HLT:
                return false;
            case OpDigits.ADD:
                state.Calculator += state[arg];
                return true;
            case OpDigits.SUB:
                state.Calculator -= state[arg];
                return true;
            case OpDigits.STO:
                state[arg] = (ushort)state.Calculator;
                return true;
            case OpDigits.LDA:
                state.Calculator = state[arg];
                return true;
            case OpDigits.BR:
                state.pc = arg;
                return true;
            case OpDigits.BRZ:
                if (state.Calculator == 0) state.pc = arg;
                return true;
            case OpDigits.BRP:
                if (!state.nflag) state.pc = arg;
                return true;
            case OpDigits.IO:
                if (arg == 1)
                {
                    state.Calculator = iface.Input();
                    return true;
                }
                else if (arg == 2)
                {
                    iface.Output(state.Calculator);
                    return true;
                }
                goto default;
            default:
                iface.DebugLog($"Unknown instruction {instr} in box {state.pc - 1}");
                return false;
        }
    }

    public static int Run(ref InterpreterState state, IInterface iface)
    {
        int steps = 0;
        do { steps++; } while (Step(ref state, iface));
        return steps;
    }

    public static int Run(ref InterpreterState state, IInterface iface, int maxSteps)
    {
        int steps = 0;
        do { steps++; } while (steps <= maxSteps && Step(ref state, iface));
        return steps;
    }

    public static InterpreterState LoadFromBin(FileInfo file)
    {
        using var f = File.OpenRead(file.FullName);
        BinaryReader reader = new(f);

        InterpreterState state = new()
        {
            calc = reader.ReadUInt16(),
            pc = reader.ReadUInt16(),
            nflag = reader.ReadBoolean(),
        };

        for (uint i = 0; i < 100; i++)
        {
            state[i] = reader.ReadUInt16();
        }

        return state;
    }

    public static void SaveToBin(FileInfo file, in InterpreterState state)
    {
        using var f = File.OpenWrite(file.FullName);
        BinaryWriter writer = new(f);

        writer.Write((ushort)state.calc);
        writer.Write((ushort)state.pc);
        writer.Write(state.nflag);

        for (uint i = 0; i < 100; i++)
        {
            writer.Write(state[i]);
        }
    }

    public static InterpreterState LoadFromAssembler(ushort[] result)
    {
        if (result.Length > 100) { throw new System.ArgumentException("Assembled code is too long for interpreter!"); }

        InterpreterState state = new()
        {
            pc = 0,
            calc = 0,
            nflag = false,
        };

        unsafe
        {
            fixed (ushort* src = result, dst = &state[0])
            {
                Buffer.MemoryCopy(src, dst, 100 * sizeof(ushort), result.Length * sizeof(ushort));
            }
        }

        return state;
    }

    public static string SaveToText(in InterpreterState state)
    {
        /* Size:
            3 LMC
            2 []
            1 P/N
            102 * 3 (values)
            102 spaces

            total 414
        */
        StringBuilder s = new("LMC[", 414);
        s.Append(state.nflag ? 'N' : 'P');
        s.Append(' ');
        s.Append(state.pc.ToString("000"));
        s.Append(' ');
        s.Append(state.calc.ToString("000"));

        for (uint i = 0; i < 100; i++)
        {
            s.Append(' ');
            s.Append(state[i].ToString("000"));
        }

        s.Append(']');

        return s.ToString();
    }

    public static void LoadFromText(StreamReader reader, out InterpreterState state)
    {
        state = default;

        Span<char> buffer = stackalloc char[4];
        ReadOnlySpan<char> ro = buffer;

        if (reader.Read(buffer) < 4 || !MemoryExtensions.Equals(ro, "LMC[", StringComparison.Ordinal)) { throw new ArgumentException($"Invalid header: {ro}"); }

        state.nflag = reader.Read() switch
        {
            'P' => false,
            'N' => true,
            _ => throw new ArgumentException($"Expected nflag"),
        };

        if (reader.Read() != ' ') throw new ArgumentException("Expected space after P/N");

        char lastRead = '\0';

        for (int i = -2; i < 100; i++)
        {
            int len = 0;
            for (int j = 0; j < 4; j++)
            {
                int read = reader.Read();
                if (read == -1) throw new ArgumentException("Unexpected end of string");
                buffer[j] = lastRead = (char)read;

                if (read == ' ' || read == ']')
                {
                    for (int k = j; j < 4; j++)
                    {
                        buffer[k] = ' ';
                    }

                    break;
                }

                len++;
            }

            int v = int.Parse(ro[0..len]);

            int max = i == -2 ? 99 : 999;

            if (v > max || v < 0) { throw new ArgumentException($"Input number {v} out of range"); }

            if (i == -2)
            {
                state.pc = (uint)v;
            }
            else if (i < 0)
            {
                state.calc = v;
            }
            else
            {
                state[(uint)i] = (ushort)v;
            }
        }

        if (lastRead != ']') { throw new ArgumentException("Expected ]"); }
    }
}