namespace NsAbsVisitAsync.NsCliTools
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

    public sealed class ScanGen
    {
        private readonly MSBuildWorkspace workspace_;

        private readonly string projPath_;

        private readonly Project project_;

        private readonly ScanSettings scanSettings_;

        private readonly CodeGenSettings codeGenSettings_;

        private ScanGen
            (MSBuildWorkspace workspace
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
            (string projPath
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

            var codeGenFolderPath = $"{projFolderPath}{Path.DirectorySeparatorChar}{this.codeGenSettings_.CodeGenFolderName}";
            var genFileNameSpace = $"{this.project_.Name}.NsGenVisit";
            var injectTypeName = "ReceptionistInject";

            foreach (var kv in symbols)
            {
                var document = kv.Key;
                var symbolsList = kv.Value;

                if (document.FilePath is string documentPathStr)
                {
                    if (documentPathStr.StartsWith(codeGenFolderPath) && documentPathStr.EndsWith(".gen.cs"))
                    {
                        Console.Out.WriteLine($"跳过生成文件: {documentPathStr}");
                        continue;
                    }
                }
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
                    if (!Directory.Exists(codeGenFolderPath))
                        Directory.CreateDirectory(codeGenFolderPath);

                    if (document.FilePath is null)
                        throw new Exception($"Document(Name: {document.Name}) file path does not exists");
                    var documentId = document
                        .FilePath
                        .Remove(startIndex: 0, count: projFolderPath.Length + 1)
                        .Replace('/', '_')
                        .Replace('\\', '_')
                        .Replace('.', '_');

                    var genFileName = $"{documentId}.gen.cs";
                    var absFilePath = $"{codeGenFolderPath}{Path.DirectorySeparatorChar}{genFileName}";

                    await Console.Out.WriteLineAsync($"正在为 {document.Name} 生成代码文件 {absFilePath} (documentId: {documentId})");

                    /// https://stackoverflow.com/questions/1225857/write-string-to-text-file-and-ensure-it-always-overwrites-the-existing-content/1225869
                    /// * If the file exists, this overwrites it.
                    /// * If the file does not exist, this creates it.
                    File.WriteAllText(absFilePath, string.Empty);

                    /// Exclusively open file and write generated code into it.
                    using var file = new FileStream(absFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await GenerateFileLevelCodeAsync(
                        codeGenSettings: this.codeGenSettings_,
                        project: this.project_,
                        documentId: documentId,
                        fileStream: file,
                        document: document,
                        genFileNameSpace: genFileNameSpace,
                        injectTypeName: injectTypeName,
                        symbolsList: symbolsList
                    );
                }
                catch (Exception e)
                {
                    await Console.Out.WriteLineAsync($"{e}");
                    throw;
                }
            }

            await GenerateInjectCodeAsync(
                this.codeGenSettings_,
                genFileNameSpace,
                codeGenFolderPath,
                injectTypeName
            );
        }

        static async ValueTask GenerateFileLevelCodeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            string documentId,
            FileStream fileStream,
            Document document,
            string genFileNameSpace,
            string injectTypeName,
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

                var nsLine = $"namespace {genFileNameSpace}\n{{\n";
                await fileStream.WriteAsync(nsLine);

                var usingLine = @$"
using System; // To use NotSupportedException
using System.Threading; // To use CancellationToken

using {codeGenSettings.UsingLineStr}; 

using NsAbsVisitAsync;

";
                await fileStream.WriteAsync(usingLine);

                var tab = codeGenSettings.TabStr;
                var tab2 = $"{tab}{tab}";

                foreach (var typeSymbol in symbolsList)
                {
                    if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
                        throw new Exception($"Expect {nameof(INamedTypeSymbol)}, but {typeSymbol.GetType().FullName} encountered.");

                    var dataTypeFullName = namedTypeSymbol.ToDisplayString();
                    var receptionistKlassSuffix = dataTypeFullName.Replace('.', '_');
                    var genTypeName = $"Receptionist_{receptionistKlassSuffix}";

                    var clsDeclLine = $"public sealed class {genTypeName} : IReceptionist<{dataTypeFullName}>\n" + "{\n";
                    await fileStream.WriteAsync(clsDeclLine);

                    // Code gen for the static constructor of the receptionist class
                    // static Receptionist_GenTypeName()
                    //     => ReceptionistInject.Register<dataTypeFullName, Receptionist_GenTypeName>();
                    if (true)
                    {
                        var ctorStaticDecl0 = $"static {genTypeName}()";
                        var ctorStaticImpl0 = $"=> {injectTypeName}.Register<{dataTypeFullName}, {genTypeName}>();";

                        await fileStream.WriteAsync($"{tab}{ctorStaticDecl0}\n");
                        await fileStream.WriteAsync($"{tab}{tab}{ctorStaticImpl0}\n");
                    }

                    var methDecl0 = $"public async {codeGenSettings.TasksTypeStr} AcceptAsync(";
                    var methDeclParamData = $"{dataTypeFullName} data,";
                    var methDeclParamVisitor = $"IVisitor<{dataTypeFullName}> visitor,";
                    var methDeclParamFactory = $"IVisitorFactory<{dataTypeFullName}> factory,";
                    var methDeclParamToken = "CancellationToken token = default)";

                    await fileStream.WriteAsync($"\n{tab}{methDecl0}\n");
                    await fileStream.WriteAsync($"{tab2}{methDeclParamData}\n");
                    await fileStream.WriteAsync($"{tab2}{methDeclParamVisitor}\n");
                    await fileStream.WriteAsync($"{tab2}{methDeclParamFactory}\n");
                    await fileStream.WriteAsync($"{tab2}{methDeclParamToken}\n");
                    await fileStream.WriteAsync($"{tab}{{\n");

                    if (namedTypeSymbol.TypeKind == TypeKind.Interface || (namedTypeSymbol.TypeKind == TypeKind.Class && namedTypeSymbol.IsAbstract))
                        await GenerateAbstractTypeSymbolCodeAsync(codeGenSettings, project, fileStream, document, namedTypeSymbol, genTypeName);
                    else
                        await GenerateConcreteTypeSymbolCodeAsync(codeGenSettings, project, fileStream, document, namedTypeSymbol, genTypeName);

                    await fileStream.WriteAsync("}\n\n");
                    await fileStream.FlushAsync();
                }

                // Write the closing right brace of the namespace
                await fileStream.WriteAsync("}\n\n");
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateConcreteTypeSymbolCodeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            Stream ioStream,
            Document document,
            INamedTypeSymbol namedTypeSymbol,
            string genTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";
            try
            {
                var dataTypeFullName = namedTypeSymbol.ToDisplayString();
                var members = namedTypeSymbol.GetVisitableMembers();
                var memberCount = unchecked((uint)members.Count());
                if (memberCount > 0u)
                {
                    foreach (var member in namedTypeSymbol.GetVisitableMembers())
                    {
                        await GenerateMemberSymbolCodeAsync(codeGenSettings, ioStream, namedTypeSymbol, member, memberCount);
                        memberCount -= 1u;
                    }
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

        static async ValueTask GenerateAbstractTypeSymbolCodeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            Stream ioStream,
            Document document,
            INamedTypeSymbol namedTypeSymbol,
            string genTypeName)
        {
            static async ValueTask<List<INamedTypeSymbol>> FindImplementationsAsync(
                Project project,
                INamedTypeSymbol targetType,
                CancellationToken cancellationToken = default)
            {
                var implementations = new List<INamedTypeSymbol>();

                foreach (var document in project.Documents)
                {
                    var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                    var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                    if (syntaxRoot == null || semanticModel == null)
                        continue;

                    // Find all type declarations in the file
                    var typeDeclarations = syntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>();

                    foreach (var typeDecl in typeDeclarations)
                    {
                        var symbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken) as INamedTypeSymbol;
                        if (symbol == null || symbol.IsAbstract)
                            continue;

                        if (InheritsOrImplements(symbol, targetType))
                            implementations.Add(symbol);
                    }
                }
                return implementations;
            }

            static bool InheritsOrImplements(INamedTypeSymbol typeSymbol, INamedTypeSymbol targetSymbol)
            {
                // Check interface implementation
                if (targetSymbol.TypeKind == TypeKind.Interface)
                {
                    return typeSymbol.AllInterfaces.Any(i =>
                        SymbolEqualityComparer.Default.Equals(i, targetSymbol));
                }

                // Check class inheritance
                if (targetSymbol.TypeKind == TypeKind.Class && targetSymbol.IsAbstract)
                {
                    var baseType = typeSymbol.BaseType;
                    while (baseType != null)
                    {
                        if (SymbolEqualityComparer.Default.Equals(baseType, targetSymbol))
                            return true;
                        baseType = baseType.BaseType;
                    }
                }

                return false;
            }

            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";
            try
            {
                var dataVarName = "data";
                var factoryName = "factory";
                var implementations = await FindImplementationsAsync(project, namedTypeSymbol);
                if (!implementations.Any())
                {
                    await GenerateCodeForEmptyTypeSymbolAsync(codeGenSettings, ioStream, namedTypeSymbol);
                    return;
                }
                else
                {
                    var count = unchecked((uint)implementations.Count());
                    foreach (var subTypeSymbol in implementations)
                    {
                        await GenerateSubTypeSymbolCodeAsync(codeGenSettings, ioStream, subTypeSymbol, dataVarName, factoryName, count);
                        count -= 1u;
                    }
                }
                if (true)
                {
                    var dataTypeFullName = namedTypeSymbol.ToDisplayString();
                    var braceBegin = "{";
                    var initErrMsg = $"var m = $\"Unsupported type {{{dataVarName}.GetType()}} encountered when visiting {{typeof({dataTypeFullName})}}\";";
                    var stmtThrow = "throw new NotSupportedException(m);";
                    var braceEnd = "}";
                    var enclosingElseStmt = $"\n{tab2}else\n{tab2}{braceBegin}\n{tab2}{tab}{initErrMsg}\n{tab2}{tab}{stmtThrow}\n{tab2}{braceEnd}\n{tab}{braceEnd}\n";
                    await ioStream.WriteAsync(enclosingElseStmt);
                }
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
            ISymbol memberSymbol,
            uint memberSymbolsCount)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";
            try
            {
                var visitorDeclName = $"visitor_{memberSymbol.Name}";
                var memberTypeSymbol = memberSymbol switch
                {
                    IFieldSymbol fieldSymbol => fieldSymbol.Type,
                    IPropertySymbol propertySymbol => propertySymbol.Type,
                    _ => throw new Exception($"unsupported symbol type {memberSymbol.GetType()}"),
                };
                var memberTypeFullName = memberTypeSymbol.ToDisplayString();
                var visitorDecl = $"using var {visitorDeclName} = await factory.GetItemVisitorAsync<{memberTypeFullName}>(visitor, {memberSymbolsCount}u, \"{memberSymbol.Name}\", token);";
                var visitLine = $"bool visit_{memberSymbol.Name}_Succeeded = await {visitorDeclName}.VisitAsync(data.{memberSymbol.Name}, token);";
                var judgeLine = $"if (!visit_{memberSymbol.Name}_Succeeded)";
                var retLine = "return false;";

                await ioStream.WriteAsync($"{tab2}{visitorDecl}\n");
                await ioStream.WriteAsync($"{tab2}{visitLine}\n");
                await ioStream.WriteAsync($"{tab2}{judgeLine}\n");
                await ioStream.WriteAsync($"{tab2}{tab}{retLine}\n");
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask GenerateSubTypeSymbolCodeAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            INamedTypeSymbol subTypeSymbol,
            string dataVarName,
            string factoryName,
            uint variantsCount)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";
            var subTypeFullName = subTypeSymbol.ToDisplayString();
            var subTypeShortName = subTypeSymbol.Name;
            var dataVarNewName = $"x_{subTypeShortName}";
            var visitorVarName = $"visitor_{subTypeShortName}";
            try
            {
                var hostCast = $"if ({dataVarName} is {subTypeFullName} {dataVarNewName})";
                var braceBegin = "{";
                var l1 = $"using var {visitorVarName} = await {factoryName}.GetVariantVisitorAsync<{subTypeFullName}>(visitor, {variantsCount}u, token);";
                var l2 = $"return await {visitorVarName}.VisitAsync({dataVarNewName}, token);";
                var braceEnd = "}";
                var castErrLine = $"\n{tab2}{hostCast}\n{tab2}{braceBegin}\n{tab2}{tab}{l1}\n{tab2}{tab}{l2}\n{tab2}{braceEnd}";
                await ioStream.WriteAsync(castErrLine);
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
            var visitLine = "await System.Threading.Tasks.Task.CompletedTask;";
            var stmt = $"{tab}{tab}{commentLine}\n{tab}{tab}{visitLine}\n";
            await ioStream.WriteAsync(stmt);
        }

        static async ValueTask GenerateInjectCodeAsync(
            CodeGenSettings codeGenSettings,
            string genFileNameSpace,
            string codeGenFolderPath,
            string injectTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var genFileName = $"{injectTypeName}.gen.cs";
            var absFilePath = $"{codeGenFolderPath}{Path.DirectorySeparatorChar}{genFileName}";
            try
            {
                using var file = new FileStream(absFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                var code =
@$"namespace {genFileNameSpace}
{{
    using NsAnyLR;
    using NsAbsVisitAsync;

    public static class {injectTypeName}
    {{
        private static readonly NsAbsVisitAsync.ReceptionistManager manager_ = new ReceptionistManager();

        public static Option<IReceptionist<T>> GetReceptionist<T>()
            => manager_.GetReceptionist<T>();

        internal static void Register<T, R>() where R : class, IReceptionist<T>, new()
            => manager_.RegisterReceptionist<T, R>();
    }}
}}
";
                await file.WriteAsync(code);
                await file.FlushAsync();
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
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
                    (m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property)
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