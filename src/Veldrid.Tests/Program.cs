using System;
using System.Collections.Generic;

namespace Veldrid.Tests;

internal static class Program
{
    public static int Main(string[] args)
    {
        List<string> newArgs = new(args);
        newArgs.Insert(0, typeof(Program).Assembly.Location);
        int returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());
        Console.WriteLine("Tests finished. Press any key to exit.");
        if (!Console.IsInputRedirected)
            Console.ReadKey(true);

        return returnCode;
    }
}
