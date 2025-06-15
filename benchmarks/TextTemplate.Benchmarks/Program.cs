using System.Collections.Generic;
using BenchmarkDotNet.Running;
using HandlebarsDotNet;
using Scriban;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
