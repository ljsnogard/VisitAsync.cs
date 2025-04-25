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

    public sealed class ScanGen
    {
        private readonly MSBuildWorkspace workspace_;

        private readonly string projPath_;

        private readonly Project project_;

        private readonly ScanSettings scanSettings_;

        private readonly CodeGenSettings codeGenSettings_;

        private ScanGen
            ( MSBuildWorkspace workspace
            , string projPath
            , Project project
            , ScanSettings scanSettings
            , CodeGenSettings codeGenSettings)
        {
            this.workspace_ = workspace;
            this.projPath_ = projPath;
            this.project_ = project;
            this.scanSettings_ = scanSettings;
            this.codeGenSettings_ = codeGenSettings;
        }

        public static async Task<ScanGen> OpenAsync
            ( string projPath
            , ScanSettings scanSettings
            , CodeGenSettings codeGenSettings)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projPath);
            if (project is null)
                throw new Exception();
            return new ScanGen(workspace, projPath, project, scanSettings, codeGenSettings);
        }

        public async Task GenAsync(CancellationToken token = default)
        {
            var symbols = new Dictionary<Document, List<ISymbol>>();
            try
            {
                foreach (var document in this.project_.Documents)
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
                        if (this.scanSettings_.IsAttributeRequired && symbol.GetAttributes().Any(a => a.AttributeClass?.Name == nameof(AllowVisitAttribute)))
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

            var projFolderPath = Path.GetDirectoryName(Path.GetFullPath(this.projPath_));
            if (projFolderPath is null)
                throw new Exception($"Faield to get project folder path from projPath({this.projPath_})");

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
                    var folderPath = $"{projFolderPath}{Path.DirectorySeparatorChar}{this.codeGenSettings_.CodeGenFolderName}";
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

                    /// https://stackoverflow.com/questions/1225857/write-string-to-text-file-and-ensure-it-always-overwrites-the-existing-content/1225869
                    /// * If the file exists, this overwrites it.
                    /// * If the file does not exist, this creates it.
                    File.WriteAllText(absFilePath, string.Empty);

                    /// Exclusively open file and write generated code into it.
                    using var file = new FileStream(absFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
                    await GenerateFileLevelCodeAsync(
                        this.codeGenSettings_,
                        this.project_.Name,
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
            FileStream fileStream,
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
                await fileStream.WriteAsync(topComment);

                var genFileNameSpace = $"{projName}.GenVisit";
                var nsLine = $"namespace {genFileNameSpace};\n";
                await fileStream.WriteAsync(nsLine);

                var usingLine = @$"
using System.Threading; // To use CancellationToken

using {codeGenSettings.UsingLineStr}; 

using VisitAsyncUtils;

";
                await fileStream.WriteAsync(usingLine);

                var genExtensionName = $"{documentId}_AcceptVisitorExtensions";
                var extLine = $"public static class {genExtensionName}\n" + "{";
                await fileStream.WriteAsync(extLine);

                foreach (var typeSymbol in symbolsList)
                {
                    if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
                        throw new Exception($"Expect {nameof(INamedTypeSymbol)}, but {typeSymbol.GetType().FullName} encountered.");
                    await GenerateTypeSymbolCodeAsync(codeGenSettings, fileStream, document, namedTypeSymbol);
                }
                await fileStream.WriteAsync("}\n");
                await fileStream.FlushAsync();
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

                var extFnDecl0 = $"public static async {codeGenSettings.TasksTypeStr} AcceptAsync<F, V>(this {hostTypeFullName} host, F factory, CancellationToken token = default)";
                var extFnDeclF = $"where F : IVisitorFactory<{hostTypeFullName}, V>";
                var extFnDeclV = $"where V : IVisitor<{hostTypeFullName}>";

                var declLine = $"\n{commentLines}\n{tab}{extFnDecl0}\n{tab}{tab}{extFnDeclF}\n{tab}{tab}{extFnDeclV}\n{tab}" + "{\n";
                await ioStream.WriteAsync(declLine);

                var members = namedTypeSymbol.GetVisitableMembers();
                if (members.Any())
                {
                    var declUsingLine = $"{tab}{tab}using var visitor = await factory.GetVisitorAsync(host, token);\n\n";
                    await ioStream.WriteAsync(declUsingLine);
                    foreach (var member in namedTypeSymbol.GetVisitableMembers())
                        await GenerateMemberSymbolCodeAsync(codeGenSettings, ioStream, namedTypeSymbol, member);
                }
                else
                    await GenerateCodeForEmptyTypeSymbolAsync(codeGenSettings, ioStream, namedTypeSymbol);

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
                var stmt = $"{tab}{tab}{visitLine}\n{tab}{tab}{judgeLine}\n{tab}{tab}{tab}{retLine}\n\n";
                await ioStream.WriteAsync(stmt);
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateCodeForEmptyTypeSymbolAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            INamedTypeSymbol namedTypeSymbol)
        {
            var tab = codeGenSettings.TabStr;
            var commentLine = $"// type {namedTypeSymbol.Name} has no any appropriate members to visit";
            var visitLine = "await ValueTask.CompletedTask;";
            var stmt = $"{tab}{tab}{commentLine}\n{tab}{tab}{visitLine}\n";
            await ioStream.WriteAsync(stmt);
        }
    }

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