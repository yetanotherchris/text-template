using TextTemplate;
using Xunit;

namespace TextTemplate.Tests;

public class BreakContinueTests
{
    [Fact]
    public void RangeSupportsBreak()
    {
        var tpl = Template.New("t").Parse("{{range .}}{{.}}{{break}}{{end}}");
        var data = new[] {"a","b","c"};
        var outText = tpl.Execute(data);
        Assert.Equal("a", outText);
    }

    [Fact]
    public void RangeSupportsContinue()
    {
        var tpl = Template.New("t").Parse("{{range .}}{{continue}}{{.}}{{end}}");
        var data = new[] {"a","b","c"};
        var outText = tpl.Execute(data);
        Assert.Equal(string.Empty, outText);
    }
}

