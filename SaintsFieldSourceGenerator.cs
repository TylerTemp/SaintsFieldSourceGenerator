using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static SaintsFieldSourceGenerator.Utils;


namespace SaintsFieldSourceGenerator
{
    [Generator]
    public class SaintsFieldSourceGenerator : ISourceGenerator
    {

        private bool _generate = true;

        public void Execute(GeneratorExecutionContext context)
        {
            string commonPrefix =
                LongestCommonPrefix(context.Compilation.SyntaxTrees.Select(each => each.FilePath).ToArray());
            // DebugToFile($"Common Prefix: {commonPrefix}");

            if (commonPrefix == "")
            {
                return;
            }

            //string assetPathNotIncluded = FindAssetPathNotIncluded(commonPrefix);
            //if (assetPathNotIncluded == "")
            //{
            //    DebugToFile($"Asset not found in {commonPrefix}");
            //    return;
            //}
            string assetPathNotIncluded = "";

            // DebugToFile($"Found Asset Path: {assetPathNotIncluded}");


            try
            {
                foreach (SyntaxTree tree in context.Compilation.SyntaxTrees)
                {
                    //string norPath = tree.FilePath.Replace("\\", "/");
                    //if (!norPath.StartsWith(assetPathNotIncluded + "/Assets/"))
                    //{
                    //    DebugToFile($"not in asset path: {tree.FilePath}");
                    //    continue;
                    //}


                    string rcFile = assetPathNotIncluded + "Assets/~generate.saintsfieldrc";
                    if (File.Exists(rcFile))
                    {
                        string rcContent = File.ReadAllText(rcFile).Trim();
                        foreach (string line in rcContent.Split('\n'))
                        {
                            string[] lineParts = line.Trim().Split('=');
                            string controlName = lineParts[0];
                            int controlValue = int.Parse(lineParts[1]);
                            switch (controlName)
                            {
                                case "generate":
                                    _generate = controlValue != 0;
                                    break;
                                case "debug":
                                    Utils.Debug = controlValue != 0;
                                    break;
                            }
                        }
                    }

                    if (!_generate)
                    {
                        return;
                    }


                    if (!tree.FilePath.Contains("SerDictionaryExample"))
                    {
                        continue;
                    }

                    Utils.DebugToFile($"Processing {tree.FilePath}");

                    //string relativePath = norPath.Substring(assetPathNotIncluded.Length + "/Assets".Length + 1);
                    string fileBaseName = Path.GetFileNameWithoutExtension(tree.FilePath);

                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    // ScoopedWriter rootWriter = new ScoopedWriter();

                    List<string> usingNames = new List<string>();

                    // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                    foreach (UsingDirectiveSyntax usingDirectiveSyntax in root.Usings)
                    {
                        // DebugToFile(usingDirectiveSyntax.ToString());
                        usingNames.Add(usingDirectiveSyntax.ToString());
                    }

                    SemanticModel semanticModel = context.Compilation.GetSemanticModel(tree);

                    List<IWriter> writers = new List<IWriter>();

                    foreach (MemberDeclarationSyntax memberDeclarationSyntax in root.Members)
                    {
                        Utils.DebugToFile($"memberDeclarationSyntax.Kind()={memberDeclarationSyntax.Kind()}");
                        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                        switch (memberDeclarationSyntax.Kind())
                        {
                            case SyntaxKind.NamespaceDeclaration:
                                {
                                    NamespaceDeclarationSyntax namespaceDeclarationSyntax =
                                        (NamespaceDeclarationSyntax)memberDeclarationSyntax;

                                    Utils.DebugToFile($"Processing namespace {namespaceDeclarationSyntax.Name}");

                                    ScoopedWriter nameSpaceResult =
                                        ParseNamespace(context.Compilation, semanticModel, namespaceDeclarationSyntax);
                                    if (nameSpaceResult != null)
                                    {
                                        writers.Add(nameSpaceResult);
                                    }

                                }
                                break;
                            case SyntaxKind.ClassDeclaration:
                            {
                                ClassDeclarationSyntax classDecl =
                                    (ClassDeclarationSyntax)memberDeclarationSyntax;

                                ClassOrStructWriter classResult = ParseClassOrStructDeclarationSyntax(context.Compilation, semanticModel.GetDeclaredSymbol(classDecl));
                                if (classResult != null)
                                {
                                    writers.Add(classResult);
                                }
                            }
                                break;
                            case SyntaxKind.StructDeclaration:
                            {
                                StructDeclarationSyntax structDecl = (StructDeclarationSyntax)memberDeclarationSyntax;
                                ClassOrStructWriter structResult =
                                    ParseClassOrStructDeclarationSyntax(context.Compilation, semanticModel.GetDeclaredSymbol(structDecl));
                                if (structResult != null)
                                {
                                    writers.Add(structResult);
                                }
                            }
                                break;
                        }
                    }

                    if (writers.Count == 0)
                    {
                        continue;
                    }

                    StringBuilder sourceBuilder = new StringBuilder();
                    foreach (string usingName in usingNames)
                    {
                        sourceBuilder.Append($"{usingName}\n");
                    }

                    foreach (IWriter writer in writers)
                    {
                        sourceBuilder.Append(writer.Write());
                    }

                    string folder = Path.GetDirectoryName(tree.FilePath);
                    string fileName = $"{fileBaseName}.SaintsSerialized.cs";
                    string saveToPath;
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (string.IsNullOrEmpty(folder))
                    {
                        saveToPath = fileName;
                        // folder = folder + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        // saveToPath = Path.Combine(folder.Replace('/', Path.DirectorySeparatorChar), fileName);
                        saveToPath = $"{folder}/{fileName}";
                    }

                    context.AddSource(saveToPath.Replace('\\', '_').Replace('/', '_').Replace(':', '_'),
                        SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                }
            }
            catch (Exception e)
            {
                Utils.DebugToFile(e.Message);
                Utils.DebugToFile(e.StackTrace);
            }
        }

        private ScoopedWriter ParseNamespace(Compilation compilation, SemanticModel semanticModel, NamespaceDeclarationSyntax namespaceDeclarationSyntax)
        {
            ScoopedWriter writer = new ScoopedWriter
            {
                NamespaceName = namespaceDeclarationSyntax.Name.ToString(),
            };
            foreach (UsingDirectiveSyntax usingDirectiveSyntax in namespaceDeclarationSyntax.Usings)
            {
                writer.UsingLines.Add(usingDirectiveSyntax.Name.ToString());
            }

            List<ClassOrStructWriter> classOrStructWriters = new List<ClassOrStructWriter>();

            foreach (MemberDeclarationSyntax memberDeclarationSyntax in namespaceDeclarationSyntax.Members)
            {
                DebugToFile($"Processing {memberDeclarationSyntax.Kind()} in namespace {namespaceDeclarationSyntax.Name}");
                switch (memberDeclarationSyntax.Kind())
                {
                    case SyntaxKind.ClassDeclaration:
                        {
                            ClassDeclarationSyntax classDeclaration =
                                (ClassDeclarationSyntax)memberDeclarationSyntax;
                            DebugToFile($"Processing class {classDeclaration.Identifier} in namespace {namespaceDeclarationSyntax.Name}");
                            ClassOrStructWriter classWriter = ParseClassOrStructDeclarationSyntax(compilation, semanticModel.GetDeclaredSymbol(classDeclaration));
                            if (classWriter != null)
                            {
                                classOrStructWriters.Add(classWriter);
                            }
                        }
                        break;
                    case SyntaxKind.StructDeclaration:
                        {
                            StructDeclarationSyntax structDeclaration =
                                (StructDeclarationSyntax)memberDeclarationSyntax;
                            ClassOrStructWriter structWriter = ParseClassOrStructDeclarationSyntax(compilation, semanticModel.GetDeclaredSymbol(structDeclaration));
                            if (structWriter != null)
                            {
                                classOrStructWriters.Add(structWriter);
                            }
                        }
                        break;
                }
            }

            if (classOrStructWriters.Count == 0)
            {
                return null;
            }

            writer.SubClassOrStructWriters = classOrStructWriters;
            return writer;
        }

        bool IsPartial(INamedTypeSymbol typeSymbol)
        {
            foreach (SyntaxReference syntaxRef in typeSymbol.DeclaringSyntaxReferences)
            {
                SyntaxNode syntax = syntaxRef.GetSyntax();
                switch (syntax)
                {
                    case ClassDeclarationSyntax classDecl:
                        if (classDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                            return true;
                        break;
                    case StructDeclarationSyntax structDecl:
                        if (structDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                            return true;
                        break;
                }
            }
            return false;
        }

        private ClassOrStructWriter ParseClassOrStructDeclarationSyntax(Compilation compilation,
            INamedTypeSymbol namedTypeSymbol)
        {
            bool isPartial = IsPartial(namedTypeSymbol);
            DebugToFile($"Processing class/struct {namedTypeSymbol.Name} isPartial={isPartial}");
            if (!isPartial)
            {
                return null;
            }
            bool isClass = namedTypeSymbol.TypeKind == TypeKind.Class;

            StringBuilder sb = new StringBuilder();
            switch (namedTypeSymbol.DeclaredAccessibility)
            {
                case Accessibility.Public: sb.Append("public "); break;
                case Accessibility.Internal: sb.Append("internal "); break;
                case Accessibility.Protected: sb.Append("protected "); break;
                case Accessibility.Private: sb.Append("private "); break;
            }
            if (namedTypeSymbol.IsStatic)
            {
                sb.Append("static ");
            }
            if (namedTypeSymbol.IsAbstract && !namedTypeSymbol.IsSealed)
            {
                sb.Append("abstract ");
            }
            if (namedTypeSymbol.IsSealed && !namedTypeSymbol.IsAbstract)
            {
                sb.Append("sealed ");
            }
            sb.Append("partial ");
            sb.Append(namedTypeSymbol.TypeKind == TypeKind.Class ? "class " : "struct ");
            sb.Append(namedTypeSymbol.Name);
            if (namedTypeSymbol.TypeParameters.Any())
            {
                sb.Append("<");
                sb.Append(string.Join(", ", namedTypeSymbol.TypeParameters.Select(tp => tp.Name)));
                sb.Append(">");
            }

            // sb.Append(": global::UnityEngine.ISerializationCallbackReceiver");

            string declare = sb.ToString();

            DebugToFile($"Processing class/struct {namedTypeSymbol.Name} template={declare}");

            (IReadOnlyList<GenSerInfo> serInfos, IReadOnlyList<ClassOrStructWriter> subWriters) = GetGenSerInfo(compilation, namedTypeSymbol);
            if (serInfos.Count == 0 && subWriters.Count == 0)
            {
                DebugToFile($"Processing class/struct {namedTypeSymbol.Name} no serInfos, no subWriters, return null");
                return null;
            }

            return new ClassOrStructWriter
            {
                IsClass = isClass,
                Declare = declare,
                SerializedInfos = serInfos,
                SubClassOrStructWriters = subWriters,
            };
        }

        private (IReadOnlyList<GenSerInfo> serInfos, IReadOnlyList<ClassOrStructWriter> subWriters) GetGenSerInfo(Compilation compilation, INamedTypeSymbol namedTypeSymbol)
        {
            List<GenSerInfo> genSerInfos = new List<GenSerInfo>();
            List<ClassOrStructWriter> subWriters = new List<ClassOrStructWriter>();
            DebugToFile($"GetSer for {namedTypeSymbol.Name}");
            foreach (ISymbol member in namedTypeSymbol.GetMembers())
            {
                switch (member)
                {
                    case IFieldSymbol fieldSymbol:
                    {
                        Utils.DebugToFile($"GetSer IFieldSymbol {fieldSymbol.Name} in {namedTypeSymbol.Name}");
                        ITypeSymbol varType = fieldSymbol.Type;
                        ImmutableArray<AttributeData> attributes = fieldSymbol.GetAttributes();

                        ICollection<(AttributeData, string)> attributeStrings = FoundGenSerInfo(compilation, attributes);
                        if (attributeStrings != null)
                        {
                            Utils.DebugToFile($"Found SaintsSerialized on {varType} {fieldSymbol.Name}");
                            genSerInfos.Add(new GenSerInfo(
                                compilation,
                                varType,
                                fieldSymbol.Name,
                                attributeStrings
                            ));
                        }
                    }
                        break;
                    case IPropertySymbol propertySymbol:
                    {
                        Utils.DebugToFile($"GetSer IFieldSymbol {propertySymbol.Name} in {namedTypeSymbol.Name}");
                        ITypeSymbol varType = propertySymbol.Type;
                        ImmutableArray<AttributeData> attributes = propertySymbol.GetAttributes();

                        ICollection<(AttributeData, string)> attributeStrings = FoundGenSerInfo(compilation, attributes);
                        if (attributeStrings != null)
                        {
                            Utils.DebugToFile($"Found SaintsSerialized on {varType} {propertySymbol.Name}");
                            genSerInfos.Add(new GenSerInfo(
                                compilation,
                                varType,
                                propertySymbol.Name,
                                attributeStrings
                            ));
                        }
                    }
                        break;
                    case INamedTypeSymbol subNamedTypeSymbol:
                    {
                        INamedTypeSymbol containingType = member.ContainingType;
                        if (containingType.TypeKind == TypeKind.Class)
                        {
                            Utils.DebugToFile($"GetSer Class {subNamedTypeSymbol.Name} in {namedTypeSymbol.Name}");
                            ClassOrStructWriter classR = ParseClassOrStructDeclarationSyntax(compilation, subNamedTypeSymbol);
                            if (classR != null)
                            {
                                subWriters.Add(classR);
                            }
                        }
                        else if (containingType.TypeKind == TypeKind.Struct)
                        {
                            Utils.DebugToFile($"GetSer Struct {subNamedTypeSymbol.Name} in {namedTypeSymbol.Name}");
                            ClassOrStructWriter structR = ParseClassOrStructDeclarationSyntax(compilation, subNamedTypeSymbol);
                            if (structR != null)
                            {
                                subWriters.Add(structR);
                            }
                        }
                    }
                        break;
                }
            }

            if (genSerInfos.Count > 0 || subWriters.Count > 0)
            {
                return (genSerInfos, subWriters);
            }

            return (Array.Empty<GenSerInfo>(), Array.Empty<ClassOrStructWriter>());
        }

        private static string FindAssetPathNotIncluded(string commonPrefix)
        {
            List<string> parts = new List<string>(commonPrefix.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            while (parts.Count > 0)
            {
                if (parts[parts.Count - 1] == "Assets")
                {
                    List<string> preParts = new List<string>(parts);
                    preParts.RemoveAt(preParts.Count - 1);

                    string joinedPath = string.Join("/", preParts);
                    bool notFound = false;
                    foreach (string subFolder in new[] { "Packages", "ProjectSettings" })
                    {
                        string subPath = $"{joinedPath}/{subFolder}";
                        // DebugToFile(subPath);
                        if (!Directory.Exists(subPath))
                        {
                            notFound = true;
                            break;
                            // return joinedPath;
                        }
                    }

                    if (!notFound)
                    {
                        return joinedPath;
                    }
                }
                parts.RemoveAt(parts.Count - 1);
            }

            // DebugToFile($"Failed to find any asset folder in {commonPrefix}");
            return string.Empty;
        }

        private static ICollection<(AttributeData, string)> FoundGenSerInfo(Compilation compilation, ImmutableArray<AttributeData> attributes)
        {
            bool foundSaintsSerialized = false;
            List<(AttributeData, string)> extraAttributes = new List<(AttributeData, string)>();

            INamedTypeSymbol saintsSerialized = compilation.GetTypeByMetadataName("SaintsField.Playa.SaintsSerializedAttribute");
            INamedTypeSymbol nonSerialized = compilation.GetTypeByMetadataName("System.NonSerialized");
            INamedTypeSymbol serializeField = compilation.GetTypeByMetadataName("UnityEngine.SerializeField");
            INamedTypeSymbol hideInInspector = compilation.GetTypeByMetadataName("UnityEngine.HideInInspector");
            INamedTypeSymbol formerlySerializedAs = compilation.GetTypeByMetadataName("UnityEngine.Serialization.FormerlySerializedAs");

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (AttributeData attr in attributes)
            {
                if(Utils.EqualType(attr.AttributeClass, saintsSerialized, "SaintsField.Playa.SaintsSerializedAttribute"))
                {
                    foundSaintsSerialized = true;
                    continue;
                }
                else
                {
                    DebugToFile($"no SaintsSerialized: {attr}, compared with {saintsSerialized}");
                }

                if (Utils.EqualType(attr.AttributeClass, nonSerialized, "System.NonSerialized")
                    || Utils.EqualType(attr.AttributeClass, serializeField, "UnityEngine.SerializeField")
                    || Utils.EqualType(attr.AttributeClass, hideInInspector, "UnityEngine.HideInInspector")
                   )
                {
                    // ignore
                    continue;
                }

                if(Utils.EqualType(attr.AttributeClass, formerlySerializedAs, "UnityEngine.Serialization.FormerlySerializedAs"))
                {
                    if (attr.ConstructorArguments.Length == 1)
                    {
                        TypedConstant arg = attr.ConstructorArguments[0];
                        extraAttributes.Add((
                            attr,
                            $"global::UnityEngine.Serialization.FormerlySerializedAs({arg.ToCSharpString()} + \"__SaintsSerialized__\")"
                        ));
                    }

                    continue;
                }

                extraAttributes.Add((attr, attr.ToString()));
            }

            return foundSaintsSerialized
                ? extraAttributes
                : null;
        }


        public void Initialize(GeneratorInitializationContext context)
        {
        }

        private static string LongestCommonPrefix(IReadOnlyList<string> strs)
        {
            if (strs == null || strs.Count == 0)
            {
                return string.Empty;
            }

            string prefix = strs[0];
            for (int i = 1; i < strs.Count; i++)
            {
                int j = 0;
                while (j < prefix.Length && j < strs[i].Length && prefix[j] == strs[i][j])
                {
                    j++;
                }
                prefix = prefix.Substring(0, j);
                if (prefix == string.Empty) break;
            }
            return prefix;
        }
    }
}
