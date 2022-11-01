namespace NetLMC;

public static class Optimiser
{
    public static void Optimise(FileInfo infile, FileInfo outfile)
    {
        if (!infile.Exists) { throw new FileNotFoundException(); }

        if (outfile == null) { outfile = new(Path.Combine(infile.Directory.FullName, Path.GetFileNameWithoutExtension(infile.Name) + "_packed" + infile.Extension)); }

        Console.WriteLine("Getting assembled info...");
        var asm = Assembler.Assemble(infile, out var asmState);

        HashSet<string> varsToPack = new();

        int lastNonZero = -1;
        for (int i = 0; i < asm.Length; i++)
        {
            if (asm[i] != 0) { lastNonZero = i; }
        }

        foreach (var label in asmState.tags)
        {
            if (label.Value > lastNonZero)
            {
                varsToPack.Add(label.Key);
            }
        }

        Console.WriteLine($"Packing {varsToPack.Count} variables");

        Dictionary<int, (int dat, string tag)> linesToAmend = new();
        Dictionary<int, string> linesToOmit = new();
        Assembler.AssemblerState state2 = new();

        foreach (var parsedLine in Assembler.ParseFile(infile, state2))
        {
            int assembled = Assembler.AssembleLine(parsedLine, asmState);
            var arg = parsedLine.arg?.Trim();
            

            if (arg == null) continue;

            if (varsToPack.Contains(arg))
            {
                linesToAmend.Add(parsedLine.fileLine, (assembled, parsedLine.tag));
            }
            else if (state2.totalSize >= lastNonZero + 1)
            {
                linesToOmit.Add(parsedLine.fileLine, parsedLine.tag);
            }
        }

        Console.WriteLine("Saving output");

        if (outfile.Exists) { outfile.Delete(); }
        using var outF = outfile.OpenWrite();
        var writer = new StreamWriter(outF);

        writer.Write(string.Format(Help.OptimiserNotice, linesToOmit.Count));

        int lineNum = 0;
        foreach (var line in File.ReadLines(infile.FullName))
        {
            lineNum++;
            string ln = line.TrimEnd();

            if (linesToAmend.TryGetValue(lineNum, out var replaceWith))
            {
                if (!string.IsNullOrEmpty(replaceWith.tag))
                {
                    // remove tag from commented bit so it's not duplicated in source (just for neatness)
                    ln = ln.TrimStart()[replaceWith.tag.Length..];
                }

                ln = $"{replaceWith.tag}\tDAT\t{replaceWith.dat:000} # {ln.TrimStart()}";
                Console.WriteLine($"Patching instruction referring to `{Assembler.LookupTag((uint)(replaceWith.dat % 100), asmState) ?? "??"}` on line `{lineNum}`");
            }
            else if (linesToOmit.TryGetValue(lineNum, out string tag))
            {
                ln = $"# OMITTED #\t{ln.TrimStart()}";
                Console.WriteLine($"Omitting definition of `{tag}` on line {lineNum}");
            }

            writer.WriteLine(ln);
        }

        writer.Flush();
        outF.Flush();

        Console.WriteLine($"Optimised out {linesToOmit.Count} boxes, written to {outfile.FullName}");
    }
}