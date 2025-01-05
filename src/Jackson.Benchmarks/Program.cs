using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Jackson;

BenchmarkRunner.Run<ParserBenchmark>();


[MemoryDiagnoser]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
[SuppressMessage("Design", "CA1050:Declare types in namespaces")]
public class ParserBenchmark
{
    private readonly IParser _parser = 
        ParserBuilder.Create(JsonSerializerOptions.Default, "Type")
        .Map<Circle>("circle")
        .Map<Line>("line")
        .Map<Triangle>("triangle")
        .Map<Rectangle>("rectangle")
        .Build();

    private JsonNode _node = null!; // Initialized on set up.

    [Params(2000, 4000, 12000)] public int Total { get; set; }

    [Params(true, false)] public bool Dynamic { get; set; }

    [GlobalSetup]
    public void SetUp()
    {
        IEnumerable<int> range = Enumerable.Range(1, Dynamic ? Total / 2 / 4 : Total / 4).ToArray();
        var circles = range.Select(i => new { Type = "circle", Radius = i });
        var lines = range.Select(i => new { Type = "line", Length = i });
        var triangles = range.Select(i => new { Type = "triangle", Base = i, Height = i });
        var rectangles = range.Select(i => new { Type = "rectangle", Width = i, Height = i });
        var unknown = Enumerable.Range(1, Dynamic ? Total / 2 : 0)
            .Select(i => new { Type = "unknown", Value = i });
        
        var objects = circles
            .Concat<object>(lines)
            .Concat(triangles)
            .Concat(rectangles)
            .Concat(unknown);

        _node = JsonSerializer.SerializeToNode(objects)!;
    }

    [Benchmark]
    public void Parse()
    {
        foreach (object _ in (IEnumerable<object>)_parser.Parse(_node))
        {
        }
    }

    class Circle
    {
        public int Radius { get; set; }
    }

    class Line
    {
        public int Length { get; set; }
    }

    class Triangle
    {
        public int Base { get; set; }
        public int Height { get; set; }
    }

    class Rectangle
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}