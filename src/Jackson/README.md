# Jackson

A library for parsing JSON using System.Text.Json with support for type discrimination.

## Quickstart

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jackson;

public class Type1;
public class Type2;

var json =
    """
    [
        { "f1": "type", "f2": "1" },
        { "f1": "type", "f2": "2" },
        { "f1": "1", "f2": "type" },
        { "f1": "2", "f2": "type" },
        { "f1": "dynamic", "f2": [1, 2, 3], "f3": { "f1": "type", "f2": "1" } }
    ]
    """;

var parser = Parser
    .Create(JsonSerializerOptions.Default, "f1", "f2")
    .Map<Type1>("type", "1")
    .Map<Type2>("type", "2")
    .Or(
        Parser
            .Create(JsonSerializerOptions.Default, "f1", "f2")
            .Map<Type1>("1", "type")
            .Map<Type2>("2", "type")
            .Build()
    )
    .Build();

var node = JsonSerializer.Deserialize<JsonNode>(json);

if (parser.Parse(node) is IEnumerable<object> seq)
{
    foreach (object item in seq)
    {
        Console.WriteLine(item);
    }
}
```