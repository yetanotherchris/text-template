using System;
using System.IO;

namespace TextTemplate.Tests;

internal static class TestDataHelper
{
    public static string GetPath(string fileName)
    {
        string candidate = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        if (!File.Exists(candidate))
        {
            candidate = Path.GetFullPath(Path.Combine("tests", "TextTemplate.Tests", "TestData", fileName));
        }
        return candidate;
    }
}
