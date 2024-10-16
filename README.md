# Jackson

Jackson is a versatile library that simplifies the parsing of JSON nodes into strongly-typed objects. It enables
developers to map JSON structures to .NET types, facilitating seamless deserialization and type conversion. With
Jackson, handling complex JSON data, including nested objects and arrays, becomes straightforward, allowing you to map
them to custom types for further processing and validation.

## Motivation

Handling objects with complex discrimination requirements in JSON deserialization can be difficult. While
System.Text.Json supports basic deserialization well, it often requires cumbersome and error-prone custom converters for
more complex logic.

This library simplifies the process by providing an easy solution for deserializing JSON data into objects with complex
discrimination needs. By extending System.Text.Json's capabilities, it allows developers to seamlessly deserialize JSON,
even with intricate discrimination requirements.

With this library, developers can map JSON data to their object models efficiently, without needing extensive custom
converter implementations. This results in a simpler and more maintainable deserialization process.

## Usage

Refer to the [quickstart](src/Jackson/README.md#quickstart) section for additional information.


## License

See [License](LICENSE) for more details.