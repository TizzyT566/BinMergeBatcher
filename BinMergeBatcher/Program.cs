using BinMergeBatcher;

ConsoleColor color = Console.ForegroundColor;

string[] cuesFile = Directory.GetFiles(Path.GetFullPath(args[0]), "*.cue");

string output = Path.GetFullPath(args[1]);

Queue<string> failed = new();

foreach (string c in cuesFile)
{
    try
    {
        CueBin cue = new(c);
        await cue.ConsolidateCueBin(output);
    }
    catch (Exception)
    {
        failed.Enqueue(c);
    }
}

Console.ForegroundColor = ConsoleColor.Red;

foreach (string f in failed)
    Console.WriteLine(f);

if (failed.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Finished sucessfully.");
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Fi nished with errors.");
}

Console.ForegroundColor = color;