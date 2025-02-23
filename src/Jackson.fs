namespace Jackson

open System
open System.Linq
open System.Text.Json
open System.Globalization
open System.Text.Json.Nodes

module internal Parser =
    let parseDynamic (node: JsonNode) =
        let culture = CultureInfo.InvariantCulture

        let run (parser: string * CultureInfo -> bool * 'a) value =
            let ok, result = parser (value, culture)
            if ok then Option.Some result else Option.None

        let parseNumber number =
            run Byte.TryParse number
            |> Option.map box
            |> Option.orElseWith (fun () -> run Int16.TryParse number |> Option.map box)
            |> Option.orElseWith (fun () -> run Int32.TryParse number |> Option.map box)
            |> Option.orElseWith (fun () -> run Int64.TryParse number |> Option.map box)
            |> Option.orElseWith (fun () ->
                run Single.TryParse number |> Option.filter Single.IsFinite |> Option.map box)
            |> Option.orElseWith (fun () ->
                run Double.TryParse number |> Option.filter Double.IsFinite |> Option.map box)

        let parseValue (value: JsonValue) : obj =
            match value.GetValueKind() with
            | JsonValueKind.String -> value.GetValue<string>()
            | JsonValueKind.Number -> value.ToString() |> parseNumber |> Option.get
            | JsonValueKind.True -> true
            | JsonValueKind.False -> false
            | JsonValueKind.Undefined -> null
            | JsonValueKind.Null -> null
            | _ -> invalidOp $"Unknown value kind: {value.ToString()}."

        let rec parseNode (node: JsonNode) =
            match node with
            | :? JsonValue as value -> parseValue value
            | :? JsonObject as object -> object |> Seq.map (fun n -> n.Key, parseNode n.Value) |> readOnlyDict |> box
            | :? JsonArray as array -> array |> Seq.map parseNode |> box
            | _ -> null

        match parseNode node with
        | :? seq<obj> as seq -> seq :> obj
        | obj -> obj

    type TypeMap = string array * Map<string, Type>

    let parseMapped (map: TypeMap) (options: JsonSerializerOptions) (next: JsonNode -> obj) (node: JsonNode) =
        let findValue (obj: JsonObject) key =
            obj
            |> Seq.tryFind _.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            |> Option.map _.Value.ToString()

        let findType (types: TypeMap) (obj: JsonObject) =
            fst types
            |> Seq.map (findValue obj)
            |> Seq.filter Option.isSome
            |> Seq.map Option.get
            |> String.concat String.Empty
            |> (snd types).TryFind

        let parseObject object =
            findType map object
            |> Option.map (fun t -> object.Deserialize(t, options))
            |> Option.defaultValue (next object)

        match node with
        | :? JsonArray as arr -> arr.OfType<JsonObject>() |> Seq.map parseObject :> obj
        | :? JsonObject as obj -> parseObject obj
        | _ -> Seq.empty

type IParser =
    abstract member Parse: node: JsonNode -> obj

type ParserBuilder
    private (keys: string array, map: Map<string, Type>, serializer: JsonSerializerOptions, next: IParser option) =

    static member Create(options, [<ParamArray>] keys: string array) =
        ParserBuilder(keys, Map.empty, options, None)

    member _.Map<'T>([<ParamArray>] values: string array) =
        ParserBuilder(keys, map.Add((String.Join(String.Empty, values), typeof<'T>)), serializer, next)

    member _.Chain(next: IParser) =
        ParserBuilder(keys, map, serializer, Some next)

    member _.Build() : IParser =
        let next =
            next |> Option.map _.Parse |> Option.defaultValue Parser.parseDynamic

        let parser = Parser.parseMapped (keys, map) serializer next

        { new IParser with
            member _.Parse node = parser node }
