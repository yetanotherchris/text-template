using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;

namespace TextTemplate;

/// <summary>
/// Represents a text template similar to Go's template.Template.
/// </summary>
public class Template
{
    private string _templateString = string.Empty;
    private GoTextTemplateParser.TemplateContext? _parseTree;

    /// <summary>
    /// The template name.
    /// </summary>
    public string Name { get; }

    private Template(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new Template with the given name.
    /// </summary>
    public static Template New(string name) => new(name);

    /// <summary>
    /// Parses the supplied template string and returns the Template instance.
    /// </summary>
    public Template Parse(string templateString)
    {
        if (templateString == null) throw new ArgumentNullException(nameof(templateString));
        _parseTree = TemplateEngine.Parse(templateString);
        _templateString = templateString;
        return this;
    }

    /// <summary>
    /// Reads the given files, combines their contents, and parses the result.
    /// </summary>
    public Template ParseFiles(params string[] filenames)
    {
        if (filenames == null) throw new ArgumentNullException(nameof(filenames));
        var sb = new StringBuilder();
        foreach (var file in filenames)
        {
            sb.Append(File.ReadAllText(file));
        }
        return Parse(sb.ToString());
    }

    /// <summary>
    /// Executes the template using the provided dictionary.
    /// </summary>
    public string Execute(IDictionary<string, object> data)
    {
        if (_parseTree == null)
            _parseTree = TemplateEngine.Parse(_templateString);
        return TemplateEngine.Process(_parseTree, data);
    }

    /// <summary>
    /// Executes the template using the public properties of <typeparamref name="T"/>.
    /// </summary>
    public string Execute<T>(T model)
    {
        if (_parseTree == null)
            _parseTree = TemplateEngine.Parse(_templateString);
        return TemplateEngine.Process(_parseTree, model);
    }

    /// <summary>
    /// Registers a global function callable from templates using <c>call</c>.
    /// </summary>
    public static void RegisterFunction(string name, Delegate function)
    {
        TemplateEngine.RegisterFunction(name, function);
    }

}
