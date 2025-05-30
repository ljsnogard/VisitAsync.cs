# VisitAsync.cs

## About

### src\AbsVisitAsync

An abstract library defines the interfaces including `IVisitor<T>`, `IReceptionist<T>`, `IParser<T>`, `IBuilder<T>`.

### src\VisitAsyncGenCliTool

#### Intro

A C# code gen tool that generates receptionist types and the builder types.  

The receptionist types are to guide the visitors accessing to the members and properties.  

And the builder types are to guide the parsers extracting primitive data items to build a complex custom type.

The tool will scan the whole project for types that need to generate code for.  
They must be `public`, non-`static`.

And the generated code files will be placed into a single directory (`gen_visit` for default).  

#### Usage

Use this to scan the project from which the types will be added to the codegen context.

```
dotnet run -- ../path_to.csproj
```

### demo\SamplGenVisit

The hand-written code that shows what will be generate.

### tests\TestLibGenVisitAsync

The playground for the developing code gen tool to debug and see what are actually generated.

## Concept

The `IReceptionist<T>` knows how to iterate over fields and properties of a type.  
The `IVisitor<T>` knows what to do with the data, for example, serializing.
The `IParser<T>` knows how to transform the primitive data in a certain data format
(e.g. JSON, XML, Protobuf, MessagePack), and parse it to the C# data type (e.g. `string`, `uint`, `float`, `Dictionary`.)
The `IBuilder<T>` knows how to group the fields and properties data into a user-defined type.
