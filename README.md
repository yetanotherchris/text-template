# text/template

[![NuGet](https://img.shields.io/nuget/v/go-text-template.svg)](https://www.nuget.org/packages/go-text-template/)

This project is a C# adaptation of Go's template engine. It began as an
experiment to see whether OpenAI Codex could port the Go implementation to
.NET. Claude.AI helped with explanations and refinements along the way.
The source code in this repository was largely produced by Codex with input
from Claude.AI, and this README itself was also authored using Codex.

The original Go package can be found here:

- https://pkg.go.dev/text/template#pkg-overview
- https://cs.opensource.google/go/go/+/refs/tags/go1.24.4:src/text/template/template.go

Currently this repository demonstrates a relatively small but functional
implementation. The `TemplateEngine.Process` helper reads templates using the
ANTLR-generated `GoTemplateLexer` and `GoTemplateParser` and performs variable
substitution, loops and conditionals.

## Features

- Replace `{{ variable }}` placeholders with values from dictionaries or model
  objects.
- Conditional blocks with `if`, `else if` and `else` clauses.
- `for` loops and Go-style `range` loops over arrays, collections and maps.
- Built-in functions: `eq`, `ne`, numeric comparisons (`lt`, `le`, `gt`, `ge`),
  logical operators (`and`, `or`, `not`) supporting multiple arguments.
- Basic pipelines with the `lower` function for transforming output.
- Access nested properties, map keys and indexes, including dynamic indexing via
  variables.
- Whitespace trimming with `{{-` and `-}}` and comment syntax `{{/* ... */}}`.

## Example Scenarios

### 1. Variable Interpolation

#### Access properties
`{{ .Property }}`

#### Nested property access
`{{ .User.Name }}`

#### Index arrays or slices
`{{ .Items[0] }}`

#### Access map entries
`{{ .Data.key }}`

#### Control whitespace
`{{- .Name -}}`

### 2. Conditional Statements

#### Basic `if` blocks
`{{ if condition }}...{{ end }}`

#### `if`/`else` blocks
`{{ if condition }}...{{ else }}...{{ end }}`

#### `else if` chains
`{{ if condition }}...{{ else if other }}...{{ end }}`

#### Supported conditions include
- `{{ if .IsActive }}`
- `{{ if eq .Status "active" }}`
- `{{ if .User }}`

### 3. Loop Statements

#### Iterate slices or arrays
`{{ range .Items }}...{{ end }}`

#### Capture index/value
`{{ range $i, $v := .Items }}...{{ end }}`

#### Iterate maps
`{{ range .Map }}...{{ end }}`

#### Map key/value variables
`{{ range $k, $v := .Map }}...{{ end }}`

#### Handle empty collections
`{{ range .Items }}...{{ else }}...{{ end }}`

### 4. Built-in Functions

#### Equality and inequality
`eq`, `ne`

#### Numeric comparisons
`lt`, `le`, `gt`, `ge`

#### Logical operators
`and`, `or`, `not`

### 5. Comments

#### Embedding comments
`{{/* comment */}}`

### 6. Pipelines

#### Chaining functions with `|`
`{{ .Name | lower }}`

#### Available pipeline helpers include
- `lower` - convert to lowercase
- `print` - concatenate values using default formatting
- `printf` - printf-style formatting using `SprintfFormatter`
- `html` - HTML escape the value
- `js` - JavaScript escape the value
- `urlquery` - escape for URL query parameters
- `len` - length of a collection or string
- `index` - retrieve an element by index or key
- `slice` - slice strings or lists
- `call` - invoke a function value

Registered functions can be invoked via `call` by name:

```csharp
TemplateEngine.RegisterFunction("Add", new Func<int, int, int>((a, b) => a + b));
var result = TemplateEngine.Process("{{ call \"Add\" 1 2 }}", new {});
// result == "3"
```

## Not Implemented Yet

- `with`, `define`, `template` and `block` directives.
- Custom functions beyond basic comparisons and boolean operators.
- Custom delimiter support.

## Usage

```csharp
var tmpl = "Hello {{ .Name }}!";
var result = TemplateEngine.Process(tmpl, new { Name = "World" });
Console.WriteLine(result); // Hello World!
```

### Example Template

```csharp
string letter = @"Dear {{ .Name }}
{{ if .Attended }}
It was a pleasure to see you.
{{ else }}
Sorry you couldn't make it.
{{ end }}
You brought: {{ for item in Items }}{{ item }},{{ end }}
Thank you for the lovely {{ .Gift }}.";

var output = TemplateEngine.Process(letter, new
{
    Name = "Bob",
    Gift = "toaster",
    Attended = false,
    Items = new[] { "book", "pen" }
});
Console.WriteLine(output);
```

See the unit tests for more examples covering loops, conditionals and range
expressions.

## Claude's suggestions
https://gist.github.com/yetanotherchris/c80d0fadb5a2ee5b4beb0a4384020dbf.js

## License

This project is released under the MIT license. Source code was produced by
OpenAI Codex with assistance from Claude.AI.
This README was written using OpenAI Codex.
