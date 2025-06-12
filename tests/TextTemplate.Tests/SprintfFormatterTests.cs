using System;
using Xunit;
using TextTemplate;

namespace TextTemplate.Tests;

public class SprintfFormatterTests
{
    [Fact]
    public void TestBasicInteger()
    {
        Assert.Equal("123", SprintfFormatter.Format("%d", 123));
        Assert.Equal("123", SprintfFormatter.Format("%i", 123));
        Assert.Equal("123", SprintfFormatter.Format("%u", 123u));
        Assert.Equal("-123", SprintfFormatter.Format("%d", -123));
    }

    [Fact]
    public void TestHexadecimal()
    {
        Assert.Equal("ff", SprintfFormatter.Format("%x", 255));
        Assert.Equal("FF", SprintfFormatter.Format("%X", 255));
        Assert.Equal("0xff", SprintfFormatter.Format("%#x", 255));
        Assert.Equal("0XFF", SprintfFormatter.Format("%#X", 255));
    }

    [Fact]
    public void TestOctal()
    {
        Assert.Equal("377", SprintfFormatter.Format("%o", 255));
        Assert.Equal("0377", SprintfFormatter.Format("%#o", 255));
    }

    [Fact]
    public void TestFloatingPoint()
    {
        Assert.Equal("3.140000", SprintfFormatter.Format("%f", 3.14));
        Assert.Equal("3.14", SprintfFormatter.Format("%.2f", 3.14));
        Assert.Equal("3.1e+00", SprintfFormatter.Format("%.1e", 3.1));
        Assert.Equal("3.1E+00", SprintfFormatter.Format("%.1E", 3.1));
        Assert.Equal("3.14", SprintfFormatter.Format("%g", 3.14));
        Assert.Equal("3.14", SprintfFormatter.Format("%G", 3.14));
    }

    [Fact]
    public void TestWidthAndAlignment()
    {
        Assert.Equal("   123", SprintfFormatter.Format("%6d", 123));
        Assert.Equal("123   ", SprintfFormatter.Format("%-6d", 123));
        Assert.Equal("000123", SprintfFormatter.Format("%06d", 123));
        Assert.Equal("  3.14", SprintfFormatter.Format("%6.2f", 3.14));
        Assert.Equal("3.14  ", SprintfFormatter.Format("%-6.2f", 3.14));
    }

    [Fact]
    public void TestCharacterAndString()
    {
        Assert.Equal("A", SprintfFormatter.Format("%c", 'A'));
        Assert.Equal("A", SprintfFormatter.Format("%c", 65));
        Assert.Equal("Hello", SprintfFormatter.Format("%s", "Hello"));
        Assert.Equal("     Hello", SprintfFormatter.Format("%10s", "Hello"));
        Assert.Equal("Hello     ", SprintfFormatter.Format("%-10s", "Hello"));
    }

    [Fact]
    public void TestSignsAndSpaces()
    {
        Assert.Equal("+123", SprintfFormatter.Format("%+d", 123));
        Assert.Equal("-123", SprintfFormatter.Format("%+d", -123));
        Assert.Equal(" 123", SprintfFormatter.Format("% d", 123));
        Assert.Equal("-123", SprintfFormatter.Format("% d", -123));
    }

    [Fact]
    public void TestSpecialCases()
    {
        Assert.Equal("%", SprintfFormatter.Format("%%"));
        Assert.Equal("0", SprintfFormatter.Format("%d", 0));
        Assert.Equal("0.000000", SprintfFormatter.Format("%f", 0.0));
    }

    [Fact]
    public void TestMultipleArguments()
    {
        Assert.Equal("Hello 123 3.14", SprintfFormatter.Format("%s %d %.2f", "Hello", 123, 3.14159));
        Assert.Equal("Value: 42, Hex: 2a", SprintfFormatter.Format("Value: %d, Hex: %x", 42, 42));
    }

    [Fact]
    public void TestErrorCases()
    {
        Assert.Throws<ArgumentException>(() => SprintfFormatter.Format("%d"));
        Assert.Throws<ArgumentException>(() => SprintfFormatter.Format("%d", "not a number"));
        Assert.Throws<FormatException>(() => SprintfFormatter.Format("%q", 123));
    }

    [Fact]
    public void TestPointer()
    {
        IntPtr ptr = new IntPtr(0x12345678);
        string result = SprintfFormatter.Format("%p", ptr);
        Assert.StartsWith("0x", result);
        Assert.Contains("12345678", result.ToLowerInvariant());
    }
}

