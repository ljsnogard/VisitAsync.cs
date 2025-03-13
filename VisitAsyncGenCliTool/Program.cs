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
            => node is StructDeclarationSyntax || node is ClassDeclarationSyntax || node is RecordDeclarationSyntax || node is InterfaceDeclarationSyntax;

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
                    !m.GetAttributes().Any(a => a.AttributeClass.Name == nameof(IgnoreVisitAttribute))
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
            var projPath = args[0];
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projPath);

            var symbols = new Dictionary<Document, List<ISymbol>>();
            foreach (var document in project.Documents)
            {
                var symbolsList = new List<ISymbol>();

                var model = await document.GetSemanticModelAsync();
                var root = await document.GetSyntaxRootAsync();
                if (root is null)
                    throw new NullReferenceException(nameof(root));

                var nodes =
                    from node in root.DescendantNodes()
                    where node.IsQualifiedSyntaxNode()
                    select node;

                foreach (var node in nodes)
                {
                    var symbol = model.GetDeclaredSymbol(node);
                    var attributes = symbol.GetAttributes();
                    if (attributes.Any(a => a.AttributeClass.Name == nameof(AllowVisitAttribute)))
                        symbolsList.Add(symbol);
                }
                if (symbolsList.Any())
                    symbols.Add(document, symbolsList);
            }

            var projFolderPath = Path.GetDirectoryName(Path.GetFullPath(projPath));

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
                    var folderPath = $"{projFolderPath}{Path.DirectorySeparatorChar}gen_visit";
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

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
                    await GenerateFileLevelCodeAsync(project.Name, documentId, file, document, symbolsList);
                }
                catch (Exception e)
                {
                    await Console.Out.WriteLineAsync($"{e}");
                    throw;
                }
            }
        }

        static async ValueTask GenerateFileLevelCodeAsync(
            string projName,
            string documentId,
            Stream ioStream,
            Document document,
            List<ISymbol> symbolsList)
        {
            try
            {
                var topComment = $"/// Generated by VisitAsyncGenCliTool at {DateTimeOffset.Now}\n\n";
                await ioStream.WriteAsync(topComment);

                var genFileNameSpace = $"{projName}.GenVisit";
                var nsLine = $"namespace {genFileNameSpace};\n";
                await ioStream.WriteAsync(nsLine);

                var usingLine = @"
using System.Threading;

using Cysharp.Threading.Tasks;

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
                    await GenerateTypeSymbolCodeAsync(ioStream, document, namedTypeSymbol);
                }
                await ioStream.WriteAsync("}\n");
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateTypeSymbolCodeAsync(Stream ioStream, Document document, INamedTypeSymbol namedTypeSymbol)
        {
            try
            {
                var hostTypeFullName = namedTypeSymbol.ToDisplayString();
                var commentLines = $"/// <summary>Generated extension method to access members of <c>{hostTypeFullName}</c> following the visitor pattern.</summary>";
                var extFnDecl = $"public static async UniTask<bool> AcceptAsync<V>(this {hostTypeFullName} host, V visitor, CancellationToken token = default) where V : IVisitor";
                var declLine = $"\n\t{commentLines}\n\t{extFnDecl}\n" + "\t{\n";
                await ioStream.WriteAsync(declLine);

                foreach (var member in namedTypeSymbol.GetVisitableMembers())
                    await GenerateMemberSymbolCodeAsync(ioStream, namedTypeSymbol, member);

                var enclosingLine = "\t\treturn true;\n\t}\n";
                await ioStream.WriteAsync(enclosingLine);
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateMemberSymbolCodeAsync(Stream ioStream, INamedTypeSymbol namedTypeSymbol, ISymbol memberSymbol)
        {
            try
            {
                var visitLine = $"bool visit_{memberSymbol.Name}_Succeeded = await visitor.VisitAsync(host.{memberSymbol.Name}, nameof(host.{memberSymbol.Name}), token);";
                var judgeLine = $"if (!visit_{memberSymbol.Name}_Succeeded)";
                var retLine = "return false;";
                var stmt = $"\t\t{visitLine}\n\t\t{judgeLine}\n\t\t\t{retLine}\n";
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
