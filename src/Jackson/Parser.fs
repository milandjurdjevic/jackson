namespace Jackson

open System
open System.Linq
open System.Text.Json
open System.Globalization
open System.Text.Json.Nodes
open System.Runtime.CompilerServices

module internal Parser =
    let dynamic (node: JsonNode) =
        let toNum (number: string) =
            let culture = CultureInfo.InvariantCulture

            let tOption (this: bool * 'a) =
                if fst this then Some(snd this) else None

            Byte.TryParse(number, culture)
            |> tOption
            |> Option.map box
            |> Option.orElseWith (fun () -> Int16.TryParse(number, culture) |> tOption |> Option.map box)
            |> Option.orElseWith (fun () -> Int32.TryParse(number, culture) |> tOption |> Option.map box)
            |> Option.orElseWith (fun () -> Int64.TryParse(number, culture) |> tOption |> Option.map box)
            |> Option.orElseWith (fun () ->
                Single.TryParse(number, culture)
                |> tOption
                |> Option.filter Single.IsFinite
                |> Option.map box)
            |> Option.orElseWith (fun () ->
                Double.TryParse(number, culture)
                |> tOption
                |> Option.filter Double.IsFinite
                |> Option.map box)

        let toObj (value: JsonValue) : obj =
            match value.GetValueKind() with
            | JsonValueKind.String -> value.GetValue<string>()
            | JsonValueKind.Number -> value.ToString() |> toNum |> Option.get
            | JsonValueKind.True -> true
            | JsonValueKind.False -> false
            | JsonValueKind.Undefined -> null
            | JsonValueKind.Null -> null
            | _ -> invalidOp $"Unknown value kind: {value.ToString()}."

        let rec dynamic (node: JsonNode) =
            match node with
            | :? JsonValue as value -> toObj value
            | :? JsonObject as object -> object |> Seq.map (fun n -> n.Key, dynamic n.Value) |> readOnlyDict |> box
            | :? JsonArray as array -> array |> Seq.map dynamic |> box
            | _ -> null

        match dynamic node with
        | :? seq<obj> as seq -> seq :> obj
        | obj -> obj

    type private TypeMap = string array * Map<string, Type>

    let mapped (map: TypeMap) (options: JsonSerializerOptions) (next: JsonNode -> obj) (node: JsonNode) =
        let typeOf (obj: JsonObject) =
            let valOf key =
                obj
                |> Seq.tryFind (_.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                |> Option.map (_.Value.ToString())

            fst map
            |> Seq.map valOf
            |> Seq.filter Option.isSome
            |> Seq.map Option.get
            |> String.concat String.Empty
            |> (snd map).TryFind

        let toObj obj =
            typeOf obj
            |> Option.map (fun t -> obj.Deserialize(t, options))
            |> Option.defaultValue (next obj)

        match node with
        | :? JsonArray as arr -> arr.OfType<JsonObject>() |> Seq.map toObj :> obj
        | :? JsonObject as obj -> toObj obj
        | _ -> Seq.empty

type IParser =
    abstract member Parse: JsonNode -> obj

type ParserBuilder
    private (keys: string array, map: Map<string, Type>, serializer: JsonSerializerOptions, next: IParser option) =

    static member Create(options, [<ParamArray>] keys: string array) =
        ParserBuilder(keys, Map.empty, options, None)

    member _.Map<'T>([<ParamArray>] values: string array) =
        ParserBuilder(keys, map.Add((String.Join(String.Empty, values), typeof<'T>)), serializer, next)

    member _.Chain(next: IParser) =
        ParserBuilder(keys, map, serializer, Some next)

    member _.Build() : IParser =
        let parser =
            Parser.mapped (keys, map) serializer (next |> Option.map (_.Parse) |> Option.defaultValue Parser.dynamic)

        { new IParser with
            member _.Parse node = parser node }

[<AbstractClass; Sealed>]
type ParserExtensions() =

    [<Extension>]
    static member CreateParserBuilder(options, [<ParamArray>] keys: string array) = ParserBuilder.Create(options, keys)

    [<Extension>]
    static member Chain(parser: IParser, builder: ParserBuilder) = builder.Chain(parser)
