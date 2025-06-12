using System;
using Xunit;
using Shouldly;
using TextTemplate;

namespace TextTemplate.Tests;

public class SprintfFormatterTests
{
    [Fact]
    public void TestBasicInteger()
    {
        SprintfFormatter.Format("%d", 123).ShouldBe("123");
        SprintfFormatter.Format("%i", 123).ShouldBe("123");
        SprintfFormatter.Format("%u", 123u).ShouldBe("123");
        SprintfFormatter.Format("%d", -123).ShouldBe("-123");
    }

    [Fact]
    public void TestHexadecimal()
    {
        SprintfFormatter.Format("%x", 255).ShouldBe("ff");
        SprintfFormatter.Format("%X", 255).ShouldBe("FF");
        SprintfFormatter.Format("%#x", 255).ShouldBe("0xff");
        SprintfFormatter.Format("%#X", 255).ShouldBe("0XFF");
    }

    [Fact]
    public void TestOctal()
    {
        SprintfFormatter.Format("%o", 255).ShouldBe("377");
        SprintfFormatter.Format("%#o", 255).ShouldBe("0377");
    }

    [Fact]
    public void TestFloatingPoint()
    {
        SprintfFormatter.Format("%f", 3.14).ShouldBe("3.140000");
        SprintfFormatter.Format("%.2f", 3.14).ShouldBe("3.14");
        SprintfFormatter.Format("%.1e", 3.1).ShouldBe("3.1e+00");
        SprintfFormatter.Format("%.1E", 3.1).ShouldBe("3.1E+00");
        SprintfFormatter.Format("%g", 3.14).ShouldBe("3.14");
        SprintfFormatter.Format("%G", 3.14).ShouldBe("3.14");
    }

    [Fact]
    public void TestWidthAndAlignment()
    {
        SprintfFormatter.Format("%6d", 123).ShouldBe("   123");
        SprintfFormatter.Format("%-6d", 123).ShouldBe("123   ");
        SprintfFormatter.Format("%06d", 123).ShouldBe("000123");
        SprintfFormatter.Format("%6.2f", 3.14).ShouldBe("  3.14");
        SprintfFormatter.Format("%-6.2f", 3.14).ShouldBe("3.14  ");
    }

    [Fact]
    public void TestCharacterAndString()
    {
        SprintfFormatter.Format("%c", 'A').ShouldBe("A");
        SprintfFormatter.Format("%c", 65).ShouldBe("A");
        SprintfFormatter.Format("%s", "Hello").ShouldBe("Hello");
        SprintfFormatter.Format("%10s", "Hello").ShouldBe("     Hello");
        SprintfFormatter.Format("%-10s", "Hello").ShouldBe("Hello     ");
    }

    [Fact]
    public void TestSignsAndSpaces()
    {
        SprintfFormatter.Format("%+d", 123).ShouldBe("+123");
        SprintfFormatter.Format("%+d", -123).ShouldBe("-123");
        SprintfFormatter.Format("% d", 123).ShouldBe(" 123");
        SprintfFormatter.Format("% d", -123).ShouldBe("-123");
    }

    [Fact]
    public void TestSpecialCases()
    {
        SprintfFormatter.Format("%%").ShouldBe("%");
        SprintfFormatter.Format("%d", 0).ShouldBe("0");
        SprintfFormatter.Format("%f", 0.0).ShouldBe("0.000000");
    }

    [Fact]
    public void TestMultipleArguments()
    {
        SprintfFormatter.Format("%s %d %.2f", "Hello", 123, 3.14159).ShouldBe("Hello 123 3.14");
        SprintfFormatter.Format("Value: %d, Hex: %x", 42, 42).ShouldBe("Value: 42, Hex: 2a");
    }

    [Fact]
    public void TestErrorCases()
    {
        Should.Throw<ArgumentException>(() => SprintfFormatter.Format("%d"));
        Should.Throw<ArgumentException>(() => SprintfFormatter.Format("%d", "not a number"));
        Should.Throw<FormatException>(() => SprintfFormatter.Format("%q", 123));
    }

    [Fact]
    public void TestPointer()
    {
        IntPtr ptr = new IntPtr(0x12345678);
        string result = SprintfFormatter.Format("%p", ptr);
        result.ShouldStartWith("0x");
        result.ToLowerInvariant().ShouldContain("12345678");
    }
}

