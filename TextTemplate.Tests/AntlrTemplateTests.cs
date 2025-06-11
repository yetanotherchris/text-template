using System.Collections.Generic;
using TextTemplate;
using Xunit;

namespace TextTemplate.Tests;

public class AntlrTemplateTests
{
    [Fact]
    public void SimpleReplacement_Works()
    {
        const string tmpl = "Hello {{name}}, your order #{{orderId}} for {{itemCount}} items totaling ${{total}} has been processed.";
        var data = new Dictionary<string, object>
        {
            {"name", "John Doe"},
            {"orderId", "12345"},
            {"itemCount", "3"},
            {"total", "29.99"}
        };
        var tpl = Template.New("t").Parse(tmpl);
        var result = tpl.Execute(data);
        Assert.Equal("Hello John Doe, your order #12345 for 3 items totaling $29.99 has been processed.", result);
    }
}
