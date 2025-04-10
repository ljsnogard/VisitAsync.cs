# VisitAsync.cs

This is a C# dotnet tool that generates extension methods that provides access to the members and properties for types.  
The tool will scan the project for types that need to generate the extension method.  
And the generated code files will be placed into a single directory (`gen_visit` for default).  

## Usage

```
dotnet run -- ../path_to.csproj
```