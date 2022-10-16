namespace NetLMC;
using System.IO;

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
        public int calc;
        public int pc;
        public bool nflag;
        public fixed ushort memory[100];

        public void SetCalc(int v)
        {
            if (v >= 0) 
            {
                nflag = false;
                v = v % 1000;
            }
            else
            {
                nflag = true;
                do
                {
                    v += 1000;
                } while (v < 0);
            }

            calc = v;
        }

        public int GetMem(int addr)
        {
            return memory[addr % 100];
        }
    }

    public unsafe static bool Step(ref InterpreterState state, IInterface iface)
    {
        ushort instr = state.memory[state.pc++];

        OpDigits op = (OpDigits)(instr / 100);

        var arg = instr % 100; 

        switch (op)
        {
            case OpDigits.HLT:
                return false;
            case OpDigits.ADD:
                state.SetCalc(state.calc + state.memory[arg]);
                return true;
            case OpDigits.SUB:
                state.SetCalc(state.calc - state.memory[arg]);
                return true;
            case OpDigits.STO:
                state.memory[arg] = (ushort)state.calc;
                return true;
            case OpDigits.LDA:
                state.SetCalc(state.memory[arg]);
                return true;
            case OpDigits.BR:
                state.pc = arg;
                return true;
            case OpDigits.BRZ:
                if (state.calc == 0) state.pc = arg;
                return true;
            case OpDigits.BRP:
                if (!state.nflag) state.pc = arg;
                return true;
            case OpDigits.IO:
                if (arg == 1)
                {
                    state.SetCalc(iface.Input());
                    return true;
                }
                else if (arg == 2)
                {
                    iface.Output(state.calc);
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

    public unsafe static InterpreterState LoadFromBin(FileInfo file)
    {
        using var f = File.OpenRead(file.FullName);
        BinaryReader reader = new(f);

        InterpreterState state = new();
        state.calc = reader.ReadUInt16();
        state.pc = reader.ReadUInt16();
        state.nflag = reader.ReadBoolean();
        for (int i = 0; i < 100; i++)
        {
            state.memory[i] = reader.ReadUInt16();
        }

        return state;
    }

    public unsafe static void SaveToBin(FileInfo file, in InterpreterState state)
    {
        using var f = File.OpenWrite(file.FullName);
        BinaryWriter writer = new(f);

        writer.Write((ushort)state.calc);
        writer.Write((ushort)state.pc);
        writer.Write(state.nflag);

        for (int i = 0; i < 100; i++)
        {
            writer.Write(state.memory[i]);
        }
    }

    public static InterpreterState LoadFromAssembler(ushort[] result)
    {
        if (result.Length > 100) { throw new System.ArgumentException("Assembled code is too long for interpreter!"); }

        InterpreterState state = new();
        state.pc = 0;
        state.calc = 0;
        state.nflag = false;

        unsafe
        {
            fixed (ushort* src = result)
            {
                System.Buffer.MemoryCopy(src, state.memory, 100 * sizeof(ushort), result.Length * sizeof(ushort));
            }
        }

        return state;
    }
}