namespace VisitAsyncUtils.CliTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using VisitAsyncUtils;

    internal static class DocumentQueryExtensions
    {
        /// <summary>
        /// <para>判断一个语法节点是否符为以下其中一种：</para>
        /// <para>1. 声明 struct </para>
        /// <para>2. 声明 class </para>
        /// <para>3. 声明 record </para>
        /// <para>4. 声明 interface </para>
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool IsQualifiedSyntaxNode(this SyntaxNode node)
        {
            if (node is ClassDeclarationSyntax clsDecl)
                return !clsDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
            return
                node is StructDeclarationSyntax ||
                node is RecordDeclarationSyntax ||
                node is InterfaceDeclarationSyntax;
        }

        /// <summary>
        /// 从一个具名类型中找出 Visitor 模式中需要访问的字段或者属性，即所有 public 的非 static 的字段或者属性
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static IEnumerable<ISymbol> GetVisitableMembers(this INamedTypeSymbol symbol)
        {
            return
                from m in symbol.GetMembers()
                where m.DeclaredAccessibility == Accessibility.Public &&
                    !m.IsStatic &&
                    (m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property) &&
                    !m.GetAttributes().Any(a => a.AttributeClass?.Name == nameof(IgnoreVisitAttribute))
                select m;
        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("请提供项目文件 (*.csproj) 路径");
                return;
            }

            var scanSettings = new ScanSettings();
            var codeGenSettings = new CodeGenSettings(TasksTypeOptions.ValueTasks);

            var projPath = args[0];
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projPath);

            var symbols = new Dictionary<Document, List<ISymbol>>();
            try
            {
                foreach (var document in project.Documents)
                {
                    var symbolsList = new List<ISymbol>();

                    var model = await document.GetSemanticModelAsync();
                    if (model is null)
                    {
                        await Console.Out.WriteLineAsync($"Unexpected null model for document(Name: {document.Name})");
                        continue;
                    }
                    var root = await document.GetSyntaxRootAsync();
                    if (root is null)
                    {
                        await Console.Out.WriteLineAsync($"Unexpected null syntax root in document(Name: {document.Name})");
                        continue;
                    }
                    var nodes =
                        from node in root.DescendantNodes()
                        where node.IsQualifiedSyntaxNode()
                        select node;

                    foreach (var node in nodes)
                    {
                        var symbol = model.GetDeclaredSymbol(node);
                        if (symbol is null)
                            continue;

                        // FIXME: 应改为由 CodeGenSettings 来生成这个 Scan 逻辑
                        if (scanSettings.ShouldRequireAttributed && symbol.GetAttributes().Any(a => a.AttributeClass?.Name == nameof(AllowVisitAttribute)))
                            symbolsList.Add(symbol);
                        else
                            symbolsList.Add(symbol);
                    }
                    if (symbolsList.Any())
                        symbols.Add(document, symbolsList);
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }

            var projFolderPath = Path.GetDirectoryName(Path.GetFullPath(projPath));
            if (projFolderPath is null)
                throw new Exception($"Faield to get project folder path from projPath({projPath})");

            foreach (var kv in symbols)
            {
                var document = kv.Key;
                var symbolsList = kv.Value;

                Console.WriteLine($"发现代码文件: {projFolderPath}{Path.DirectorySeparatorChar}{document.Name}");

                foreach (var symbol in symbolsList)
                {
                    if (symbol is not INamedTypeSymbol namedTypeSymbol)
                        throw new Exception($"Unexpected symbol type: {symbol.ToDisplayString()}");
                    else
                        Console.WriteLine($"\t发现目标类: {symbol.ToDisplayString()}");
                    foreach (var member in namedTypeSymbol.GetVisitableMembers())
                        Console.WriteLine($"\t\t目标成员: {member.ToDisplayParts().Last()}");
                }
                try
                {
                    var folderPath = $"{projFolderPath}{Path.DirectorySeparatorChar}{codeGenSettings.CodeGenFolderName}";
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    if (document.FilePath is null)
                        throw new Exception($"Document(Name: {document.Name}) file path does not exists");
                    var documentId = document
                        .FilePath
                        .Remove(startIndex: 0, count: projFolderPath.Length + 1)
                        .Replace('/', '_')
                        .Replace('\\', '_')
                        .Replace('.', '_');

                    var genFileName = $"{documentId}.gen.cs";
                    var absFilePath = $"{folderPath}{Path.DirectorySeparatorChar}{genFileName}";

                    await Console.Out.WriteLineAsync($"正在为 {document.Name} 生成代码文件 {absFilePath} (documentId: {documentId})");

                    using var file = File.Open(absFilePath, FileMode.OpenOrCreate);
                    await GenerateFileLevelCodeAsync(
                        codeGenSettings,
                        project.Name,
                        documentId,
                        file,
                        document,
                        symbolsList
                    );
                }
                catch (Exception e)
                {
                    await Console.Out.WriteLineAsync($"{e}");
                    throw;
                }
            }
        }

        static async ValueTask GenerateFileLevelCodeAsync(
            CodeGenSettings codeGenSettings,
            string projName,
            string documentId,
            Stream ioStream,
            Document document,
            List<ISymbol> symbolsList)
        {
            try
            {
                var topComment =
@$"/// <auto-generated>
///     Generated by VisitAsyncGenCliTool at {DateTimeOffset.Now} (DateTimeOffset).
///     Changes will be lost once regenerated.
/// </auto-generated>

";
                await ioStream.WriteAsync(topComment);

                var genFileNameSpace = $"{projName}.GenVisit";
                var nsLine = $"namespace {genFileNameSpace};\n";
                await ioStream.WriteAsync(nsLine);

                var usingLine = @$"
using System.Threading; // To use CancellationToken

using {codeGenSettings.UsingLineStr}; 

using VisitAsyncUtils;

";
                await ioStream.WriteAsync(usingLine);

                var genExtensionName = $"{documentId}_AcceptVisitorExtensions";
                var extLine = $"public static class {genExtensionName}\n" + "{";
                await ioStream.WriteAsync(extLine);

                foreach (var typeSymbol in symbolsList)
                {
                    if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
                        throw new Exception($"Expect {nameof(INamedTypeSymbol)}, but {typeSymbol.GetType().FullName} encountered.");
                    await GenerateTypeSymbolCodeAsync(codeGenSettings, ioStream, document, namedTypeSymbol);
                }
                await ioStream.WriteAsync("}\n");
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateTypeSymbolCodeAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            Document document,
            INamedTypeSymbol namedTypeSymbol)
        {
            var tab = codeGenSettings.TabStr;
            try
            {
                var hostTypeFullName = namedTypeSymbol.ToDisplayString();

                var commentLines = @$"{tab}/// <summary>
{tab}/// {tab}Generated extension method to allow visitor accessing to the members of <c>{hostTypeFullName}</c> declared in the source file <c>{document.Name}</c>.
{tab}/// </summary>";

                var extFnDecl = $"public static async {codeGenSettings.TasksTypeStr} AcceptAsync<V>(this {hostTypeFullName} host, V visitor, CancellationToken token = default) where V : IVisitor";
                var declLine = $"\n{commentLines}\n{tab}{extFnDecl}\n{tab}" + "{\n";
                await ioStream.WriteAsync(declLine);

                foreach (var member in namedTypeSymbol.GetVisitableMembers())
                    await GenerateMemberSymbolCodeAsync(codeGenSettings, ioStream, namedTypeSymbol, member);

                var enclosingLine = $"{tab}{tab}return true;\n{tab}" + "}\n";
                await ioStream.WriteAsync(enclosingLine);
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateMemberSymbolCodeAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            INamedTypeSymbol namedTypeSymbol,
            ISymbol memberSymbol)
        {
            var tab = codeGenSettings.TabStr;
            try
            {
                var visitLine = $"bool visit_{memberSymbol.Name}_Succeeded = await visitor.VisitAsync(host.{memberSymbol.Name}, nameof(host.{memberSymbol.Name}), token);";
                var judgeLine = $"if (!visit_{memberSymbol.Name}_Succeeded)";
                var retLine = "return false;";
                var stmt = $"{tab}{tab}{visitLine}\n{tab}{tab}{judgeLine}\n{tab}{tab}{tab}{retLine}\n";
                await ioStream.WriteAsync(stmt);
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }
    }

    internal static class StreamWriteStrUtf8Extensions
    {
        public static async ValueTask<int> WriteAsync(this Stream stream, string content, CancellationToken token = default)
        {
            try
            {
                var strBytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(strBytes, token);
                return strBytes.Length;
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }
    }
}
