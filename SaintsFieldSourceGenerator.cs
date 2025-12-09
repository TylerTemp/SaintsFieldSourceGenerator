// ReSharper disable RedundantStringInterpolation
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.CompilerServices;


namespace SaintsFieldSourceGenerator
{
    [Generator]
    public class SaintsFieldSourceGenerator : ISourceGenerator
    {

        private bool _generate = true;
        private static bool _debug = false;

        private interface IWriter
        {
            string Write();
        }

        private class ClassOrStructWriter: IWriter
        {
            public bool IsClass;

            public IReadOnlyList<ClassOrStructWriter> SubClassOrStructWriters = new List<ClassOrStructWriter>();
            public string Declare;
            public IReadOnlyList<GenSerInfo> SerializedInfos;

            public string Write()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"{Indent.GetIndentString()}{Declare}");
                if(SerializedInfos.Count > 0)
                {
                    sb.Append(": global::UnityEngine.ISerializationCallbackReceiver");
                }
                sb.Append("\n");
                sb.Append($"{Indent.GetIndentString()}{{\n");

                using (new Indent())
                {
                    if(SerializedInfos.Count > 0)
                    {
                        foreach (GenSerInfo genSerInfo in SerializedInfos)
                        {
                            foreach (string line in WriteGenSerInfoFields(IsClass, genSerInfo))
                            {
                                sb.Append($"{Indent.GetIndentString()}{line}");
                            }
                        }


                        sb.Append($"{Indent.GetIndentString()}public void OnBeforeSerialize()\n");
                        sb.Append($"{Indent.GetIndentString()}{{\n");
                        using (new Indent())
                        {
                            foreach (GenSerInfo genSerInfo in SerializedInfos)
                            {
                                foreach (string line in WriteOnBeforeSerialize(genSerInfo))
                                {
                                    sb.Append($"{Indent.GetIndentString()}{line}");
                                }
                            }
                        }

                        sb.Append($"{Indent.GetIndentString()}}}\n");

                        sb.Append($"{Indent.GetIndentString()}public void OnAfterDeserialize()\n");
                        sb.Append($"{Indent.GetIndentString()}{{\n");
                        using (new Indent())
                        {
                            foreach (GenSerInfo genSerInfo in SerializedInfos)
                            {
                                foreach (string line in WriteOnAfterDeserialize(genSerInfo))
                                {
                                    sb.Append($"{Indent.GetIndentString()}{line}");
                                }
                            }
                        }

                        sb.Append($"{Indent.GetIndentString()}}}\n");
                    }

                    foreach (ClassOrStructWriter subClassOrStructWriter in SubClassOrStructWriters)
                    {
                        sb.Append(subClassOrStructWriter.Write());
                    }
                }

                sb.Append($"{Indent.GetIndentString()}}}\n");

                return sb.ToString();
            }
        }

        private class ScoopedWriter: IWriter
        {
            public readonly List<string> UsingLines = new List<string>();
            public string NamespaceName;
            public List<ClassOrStructWriter> SubClassOrStructWriters = new List<ClassOrStructWriter>();
            public string Write()
            {
                StringBuilder sb = new StringBuilder();
                foreach (string usingLine in UsingLines)
                {
                    sb.Append($"{Indent.GetIndentString()}using {usingLine};\n");
                }

                if (NamespaceName != null)
                {
                    sb.Append("\n");
                    sb.Append($"{Indent.GetIndentString()}namespace {NamespaceName}\n");
                    sb.Append($"{Indent.GetIndentString()}{{\n");
                }

                using (new Indent(NamespaceName == null? 0: 1))
                {
                    DebugToFile($"write SubClassOrStructWriters {SubClassOrStructWriters.Count}");
                    foreach (ClassOrStructWriter classOrStructWriter in SubClassOrStructWriters)
                    {
                        DebugToFile($"write sub {classOrStructWriter}");
                        sb.Append(classOrStructWriter.Write());
                    }
                }

                if (NamespaceName != null)
                {
                    sb.Append($"{Indent.GetIndentString()}}}\n");
                }

                return sb.ToString();
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            string commonPrefix =
                LongestCommonPrefix(context.Compilation.SyntaxTrees.Select(each => each.FilePath).ToArray());
            // DebugToFile($"Common Prefix: {commonPrefix}");
            if (commonPrefix == "")
            {
                return;
            }

            string assetPathNotIncluded = FindAssetPathNotIncluded(commonPrefix);
            if (assetPathNotIncluded == "")
            {
                return;
            }

            // DebugToFile($"Found Asset Path: {assetPathNotIncluded}");

            try
            {
                foreach (SyntaxTree tree in context.Compilation.SyntaxTrees)
                {
                    string norPath = tree.FilePath.Replace("\\", "/");
                    if (!norPath.StartsWith(assetPathNotIncluded + "/Assets/"))
                    {
                        DebugToFile($"not in asset path: {tree.FilePath}");
                        continue;
                    }

                    string rcFile = assetPathNotIncluded + "/Assets/SaintsField.Generated/~generate.saintsfieldrc";
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
                                    _debug = controlValue != 0;
                                    break;
                            }
                        }
                    }

                    if (!_generate)
                    {
                        return;
                    }


                    string relativePath = norPath.Substring(assetPathNotIncluded.Length + "/Assets".Length + 1);
                    string fileBaseName = Path.GetFileNameWithoutExtension(relativePath);

                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    // ScoopedWriter rootWriter = new ScoopedWriter();

                    List<string> usingNames = new List<string>();

                    // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                    foreach (UsingDirectiveSyntax usingDirectiveSyntax in root.Usings)
                    {
                        // DebugToFile(usingDirectiveSyntax.ToString());
                        usingNames.Add(usingDirectiveSyntax.ToString());
                    }

                    List<IWriter> writers = new List<IWriter>();

                    foreach (MemberDeclarationSyntax memberDeclarationSyntax in root.Members)
                    {
                        // DebugToFile(memberDeclarationSyntax.Kind());
                        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                        switch (memberDeclarationSyntax.Kind())
                        {
                            case SyntaxKind.NamespaceDeclaration:
                            {
                                NamespaceDeclarationSyntax namespaceDeclarationSyntax =
                                    (NamespaceDeclarationSyntax)memberDeclarationSyntax;

                                ScoopedWriter nameSpaceResult =
                                    ParseNamespace(namespaceDeclarationSyntax);
                                if (nameSpaceResult != null)
                                {
                                    writers.Add(nameSpaceResult);
                                }

                            }
                                break;
                            case SyntaxKind.ClassDeclaration:
                            {
                                ClassOrStructWriter classResult =
                                    ParseClassDeclarationSyntax((ClassDeclarationSyntax)memberDeclarationSyntax);
                                if (classResult != null)
                                {
                                    writers.Add(classResult);
                                }
                            }
                                break;
                            case SyntaxKind.StructDeclaration:
                            {
                                ClassOrStructWriter structResult =
                                    ParseStructDeclarationSyntax((StructDeclarationSyntax)memberDeclarationSyntax);
                                if (structResult != null)
                                {
                                    writers.Add(structResult);
                                }
                            }
                                break;
                        }
                    }

                    if(writers.Count == 0)
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

                    string folder = Path.GetDirectoryName(relativePath);
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

                    context.AddSource(saveToPath.Replace('\\', '_').Replace('/', '_'),
                        SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                }
            }
            catch (Exception e)
            {
                DebugToFile(e.Message);
                DebugToFile(e.StackTrace);
            }
        }

        private static IEnumerable<string> WriteGenSerInfoFields(bool isClass, GenSerInfo genSerInfo)
        {
            yield return "[global::UnityEngine.SerializeField]\n";
            // yield return $"[global::SaintsField.Utils.SaintsSerializedActual(nameof({genSerInfo.FieldName}), typeof({genSerInfo.FieldType}))]\n";
            yield return $"[global::SaintsField.Utils.SaintsSerializedActual(nameof({genSerInfo.FieldName}))]\n";
            if(genSerInfo.SerializationType != SerializationType.Dictionary && genSerInfo.SerializationType != SerializationType.HashSet)
            {
                yield return "[global::SaintsField.SaintsRow(inline: true)]\n";
            }
            if(genSerInfo.Attributes.Count > 0)
            {
                yield return $"[{string.Join(", ", genSerInfo.Attributes.Select(each => each.rawName))}]\n";
            }
            // if (genSerInfo.IsDateTime())
            // {
            //     yield return "[global::SaintsField.DateTime]\n";
            // }
            // else if (genSerInfo.IsTimeSpan())
            // {
            //     yield return "[global::SaintsField.TimeSpan]\n";
            // }

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (genSerInfo.CollectionType == CollectionType.None)
            {
                // if(genSerInfo.IsDateTime())
                // {
                //     yield return $"private long {genSerInfo.FieldName}__SaintsSerialized__;\n";
                // }
                // else if (genSerInfo.IsTimeSpan())
                // {
                //     yield return $"private long {genSerInfo.FieldName}__SaintsSerialized__;\n";
                // }
                // else
                // {
                    string equal;
                    if (isClass)
                    {
                        if (genSerInfo.SerializationType == SerializationType.Dictionary)
                        {
                            equal = $" = new global::SaintsField.SaintsDictionary<{string.Join(", ", genSerInfo.FieldTypes)}>()";
                        }
                        else if (genSerInfo.SerializationType == SerializationType.HashSet)
                        {
                            if(genSerInfo.IsSerializeReference)
                            {
                                equal =
                                    $" = new global::SaintsField.ReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>()";
                            }
                            else
                            {
                                equal =
                                    $" = new global::SaintsField.SaintsHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>()";
                            }
                        }
                        else
                        {
                            equal = " = new global::SaintsField.SaintsSerialization.SaintsSerializedProperty()";
                        }
                    }
                    else
                    {
                        equal = "";
                    }

                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return
                            $"private global::SaintsField.SaintsDictionary<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if (genSerInfo.IsSerializeReference)
                        {
                            yield return
                                $"private global::SaintsField.ReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                        }
                        else
                        {
                            yield return
                                $"private global::SaintsField.SaintsHashSet<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                        }
                    }
                    else
                    {
                        yield return
                            $"private global::SaintsField.SaintsSerialization.SaintsSerializedProperty {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                    }
                // }

            }
            else
            {
                DebugToFile($"genSerInfo.SerializationType={genSerInfo.SerializationType} for {genSerInfo.FieldName}");
                // if(genSerInfo.IsDateTime())
                // {
                //     string equal = isClass
                //         ? " = global::System.Array.Empty<long>()"
                //         : "";
                //     yield return $"private long[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                // }
                // else if(genSerInfo.IsTimeSpan())
                // {
                //     string equal = isClass
                //         ? " = global::System.Array.Empty<long>()"
                //         : "";
                //     yield return $"private long[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                // }
                // else
                {
                    string equal;
                    if (isClass)
                    {
                        if (genSerInfo.SerializationType == SerializationType.Dictionary)
                        {
                            equal = $" = global::System.Array.Empty<global::SaintsField.SaintsDictionary<{string.Join(", ", genSerInfo.FieldTypes)}>>()";
                        }
                        else if (genSerInfo.SerializationType == SerializationType.HashSet)
                        {
                            if(genSerInfo.IsSerializeReference)
                            {
                                equal =
                                    $" = global::System.Array.Empty<global::SaintsField.ReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>>()";
                            }
                            else
                            {
                                equal =
                                    $" = global::System.Array.Empty<global::SaintsField.SaintsHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>>()";
                            }
                        }
                        else
                        {
                            equal =
                                " = global::System.Array.Empty<global::SaintsField.SaintsSerialization.SaintsSerializedProperty>()";
                        }

                    }
                    else
                    {
                        equal = "";
                    }

                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return
                            $"private global::SaintsField.SaintsDictionary<{string.Join(", ", genSerInfo.FieldTypes)}>[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if (genSerInfo.IsSerializeReference)
                        {
                            yield return
                                $"private global::SaintsField.ReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                        }
                        else
                        {
                            yield return
                                $"private global::SaintsField.SaintsHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                        }
                    }
                    else
                    {
                        yield return
                            $"private global::SaintsField.SaintsSerialization.SaintsSerializedProperty[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
                    }
                }
            }
        }

        private ScoopedWriter ParseNamespace(NamespaceDeclarationSyntax namespaceDeclarationSyntax)
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
                switch (memberDeclarationSyntax.Kind())
                {
                    case SyntaxKind.ClassDeclaration:
                    {
                        ClassDeclarationSyntax classDeclaration =
                            (ClassDeclarationSyntax)memberDeclarationSyntax;
                        ClassOrStructWriter classWriter = ParseClassDeclarationSyntax(classDeclaration);
                        if(classWriter != null)
                        {
                            classOrStructWriters.Add(classWriter);
                        }
                    }
                        break;
                    case SyntaxKind.StructDeclaration:
                    {
                        StructDeclarationSyntax structDeclaration =
                            (StructDeclarationSyntax)memberDeclarationSyntax;
                        ClassOrStructWriter structWriter = ParseStructDeclarationSyntax(structDeclaration);
                        if(structWriter != null)
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

        private ClassOrStructWriter ParseClassDeclarationSyntax(ClassDeclarationSyntax classDeclaration)
        {
            return ParseClassOrStructDeclarationSyntax(
                true,
                classDeclaration.Modifiers,
                classDeclaration.Identifier.Text,
                classDeclaration.TypeParameterList,
                classDeclaration.ConstraintClauses,
                classDeclaration.Members);

        }

        private ClassOrStructWriter ParseStructDeclarationSyntax(StructDeclarationSyntax structDeclaration)
        {
            return ParseClassOrStructDeclarationSyntax(
                false,
                structDeclaration.Modifiers,
                structDeclaration.Identifier.Text,
                structDeclaration.TypeParameterList,
                structDeclaration.ConstraintClauses,
                structDeclaration.Members);
        }

        private ClassOrStructWriter ParseClassOrStructDeclarationSyntax(
            bool isClass,
            SyntaxTokenList modifiers,
            string identifierText,
            TypeParameterListSyntax typeParameterList,
            SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
            SyntaxList<MemberDeclarationSyntax> members
        )
        {
            bool isPartial = false;
            List<string> modifiersList = new List<string>();
            foreach (SyntaxToken modifier in modifiers)
            {
                // DebugToFile(modifier);

                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    isPartial = true;
                    // break;
                }

                modifiersList.Add(modifier.Text);
            }

            if (!isPartial)
            {
                return null;
            }

            string typeParams = typeParameterList?.ToString() ?? "";
            string constraints = constraintClauses.Count > 0
                ? " " + string.Join(" ", constraintClauses.Select(c => c.ToString()))
                : "";

            string declare = $"{string.Join(" ", modifiersList)} {(isClass? "class": "struct")} {identifierText}{typeParams}{constraints}";

            // sourceBuilder.Append($"namespace {nameSpace}\n{{\n");
            // // sourceBuilder.Append($@"    {classDeclaration.Keyword} partial class {classDeclaration.Identifier.Text}\n    {{\n");
            // sourceBuilder.Append(
            //     $"    {string.Join(" ", modifiersList)} class {classDeclaration.Identifier.Text}: global::UnityEngine.ISerializationCallbackReceiver\n    {{\n");
            (IReadOnlyList<GenSerInfo> serInfos, IReadOnlyList<ClassOrStructWriter> subWriters) = GetGenSerInfo(members);
            if(serInfos.Count == 0 && subWriters.Count == 0)
            {
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

        private (IReadOnlyList<GenSerInfo> serInfos, IReadOnlyList<ClassOrStructWriter> subWriters) GetGenSerInfo(SyntaxList<MemberDeclarationSyntax> members)
        {
            List<GenSerInfo> genSerInfos = new List<GenSerInfo>();
            List<ClassOrStructWriter> subWriters = new List<ClassOrStructWriter>();
            foreach (MemberDeclarationSyntax member in members)
            {
                switch (member.Kind())
                {
                    case SyntaxKind.FieldDeclaration:
                    {
                        FieldDeclarationSyntax fieldDeclarationSyntax = (FieldDeclarationSyntax)member;
                        TypeSyntax varType = fieldDeclarationSyntax.Declaration.Type;
                        // DebugToFile(varType.ToString());

                        ICollection<(AttributeSyntax, string)> attributeStrings = FoundGenSerInfo(fieldDeclarationSyntax.AttributeLists);
                        if (attributeStrings != null)
                        {
                            foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
                            {
                                DebugToFile($"Found SaintsSerialized on {variable.Identifier.Text}");
                                genSerInfos.Add(new GenSerInfo(
                                    varType.ToString(),
                                    variable.Identifier.Text,
                                    attributeStrings
                                ));
                            }
                        }
                    }
                        break;
                    case SyntaxKind.PropertyDeclaration:
                    {
                        PropertyDeclarationSyntax propertyDeclarationSyntax =
                            (PropertyDeclarationSyntax)member;
                        TypeSyntax varType = propertyDeclarationSyntax.Type;
                        // DebugToFile(varType.ToString());
                        SyntaxToken identifier = propertyDeclarationSyntax.Identifier;
                        // DebugToFile(identifier.Text);

                        ICollection<(AttributeSyntax, string)> attributeStrings = FoundGenSerInfo(propertyDeclarationSyntax.AttributeLists);
                        if (attributeStrings != null)
                        {
                            DebugToFile($"Found SaintsSerialized on {identifier.Text}");
                            genSerInfos.Add(new GenSerInfo(
                                varType.ToString(),
                                identifier.Text,
                                attributeStrings
                            ));
                        }
                    }
                        break;
                    case SyntaxKind.ClassDeclaration:
                    {
                        ClassOrStructWriter classR = ParseClassDeclarationSyntax((ClassDeclarationSyntax)member);
                        if (classR != null)
                        {
                            subWriters.Add(classR);
                        }
                    }
                        break;
                    case SyntaxKind.StructDeclaration:
                    {
                        ClassOrStructWriter structR = ParseStructDeclarationSyntax((StructDeclarationSyntax)member);
                        if (structR != null)
                        {
                            subWriters.Add(structR);
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

        private static IEnumerable<string> WriteOnBeforeSerialize(GenSerInfo genSerInfo)
        {
            if(genSerInfo.CollectionType != CollectionType.None)
            {
                yield return $"if ({genSerInfo.FieldName} == null)\n";
                yield return "{\n";
                string equal;
                string fieldTypeString;
                switch (genSerInfo.SerializationType)
                {
                    case SerializationType.Dictionary:
                        fieldTypeString = $"global::System.Collections.Generic.Dictionary<{string.Join(", ", genSerInfo.FieldTypes)}>";
                        break;
                    case SerializationType.HashSet:
                        fieldTypeString = $"global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}>";
                        break;
                    case SerializationType.Default:
                    default:
                        fieldTypeString = genSerInfo.FieldTypes[0];
                        break;
                }
                switch (genSerInfo.CollectionType)
                {
                    case CollectionType.Array:
                        equal = $"global::System.Array.Empty<{fieldTypeString}>()";
                        break;
                    case CollectionType.List:
                        equal = $"new global::System.Collections.Generic.List<{fieldTypeString}>()";
                        break;
                    case CollectionType.None:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(genSerInfo.SerializationType), genSerInfo.SerializationType, null);
                }

                yield return $"    {genSerInfo.FieldName} = {equal};\n";

                yield return "}\n";
            }

            switch (genSerInfo.CollectionType)
            {
                case CollectionType.None:
                {
                    // if(genSerInfo.IsDateTime())
                    // {
                    //
                    //     yield return $"if({genSerInfo.FieldName} is global::System.DateTime)\n";
                    //     yield return $"{{\n";
                    //     yield return $"    {genSerInfo.FieldName}__SaintsSerialized__ = global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeDateTime({genSerInfo.FieldName});\n";
                    //     yield return $"}}\n";
                    // }
                    // else if (genSerInfo.IsTimeSpan())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.TimeSpan)\n";
                    //     yield return $"{{\n";
                    //     yield return
                    //         $"    {genSerInfo.FieldName}__SaintsSerialized__ = global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeTimeSpan({genSerInfo.FieldName});\n";
                    //     yield return $"}}\n";
                    //
                    // }
                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return $"(bool {genSerInfo.FieldName}Assign, global::SaintsField.SaintsDictionary<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}Result) "
                            + $"= global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeDictionary<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                        yield return $"if ({genSerInfo.FieldName}Assign)\n";
                        // ReSharper disable once ExtractCommonBranchingCode
                        yield return $"{{\n";
                        yield return $"    {genSerInfo.FieldName}__SaintsSerialized__ = {genSerInfo.FieldName}Result;\n";
                        yield return $"}}\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if (genSerInfo.IsSerializeReference)
                        {
                            yield return $"(bool {genSerInfo.FieldName}Assign, global::SaintsField.ReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}Result) "
                                         + $"= global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                            // ReSharper disable once ExtractCommonBranchingCode
                            yield return $"if ({genSerInfo.FieldName}Assign)\n";
                            // ReSharper disable once ExtractCommonBranchingCode
                            yield return $"{{\n";
                            yield return $"    {genSerInfo.FieldName}__SaintsSerialized__ = {genSerInfo.FieldName}Result;\n";
                            yield return $"}}\n";
                        }
                        else
                        {
                            yield return $"(bool {genSerInfo.FieldName}Assign, global::SaintsField.SaintsHashSet<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}Result) "
                                         + $"= global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                            yield return $"if ({genSerInfo.FieldName}Assign)\n";
                            yield return $"{{\n";
                            yield return $"    {genSerInfo.FieldName}__SaintsSerialized__ = {genSerInfo.FieldName}Result;\n";
                            yield return $"}}\n";
                        }

                    }
                    else
                    {
                        yield return $"(bool {genSerInfo.FieldName}Ok, global::SaintsField.SaintsSerialization.SaintsSerializedProperty {genSerInfo.FieldName}Result) = "
                            + $"global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerialize({genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName}, typeof({genSerInfo.FieldTypes[0]}));\n";
                        yield return $"if ({genSerInfo.FieldName}Ok)\n";
                        yield return $"{{\n";
                        yield return $"    {genSerInfo.FieldName}__SaintsSerialized__ = {genSerInfo.FieldName}Result;\n";
                        yield return $"}}\n";
                    }
                }
                    break;
                case CollectionType.Array:
                case CollectionType.List:

                    // if(genSerInfo.IsDateTime())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is System.Collections.Generic.IReadOnlyList<global::System.DateTime>)\n";
                    //     yield return $"{{\n";
                    //     yield return $"    global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollectionDateTime(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                    //     yield return $"}}\n";
                    // }
                    // else if(genSerInfo.IsTimeSpan())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is System.Collections.Generic.IReadOnlyList<global::System.TimeSpan>)\n";
                    //     yield return $"{{\n";
                    //     yield return $"    global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollectionTimeSpan(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                    //     yield return $"}}\n";
                    // }
                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return $"global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollectionDictionary<{string.Join(", ", genSerInfo.FieldTypes)}>(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if(genSerInfo.IsSerializeReference)
                        {
                            yield return
                                $"global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollectionReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                        }
                        else
                        {
                            yield return
                                $"global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollectionHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName});\n";
                        }
                    }
                    else
                    {
                        yield return $"global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollection<{genSerInfo.FieldTypes[0]}>(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName}, typeof({genSerInfo.FieldTypes[0]}));\n";
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static IEnumerable<string> WriteOnAfterDeserialize(GenSerInfo genSerInfo)
        {
            switch (genSerInfo.CollectionType)
            {
                case CollectionType.None:
                {
                    // if (genSerInfo.IsDateTime())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.DateTime)\n";
                    //     yield return $"{{\n";
                    //     yield return $"    {genSerInfo.FieldName} = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeDateTime({genSerInfo.FieldName}__SaintsSerialized__);\n";
                    //     yield return $"}}\n";
                    // }
                    // else if (genSerInfo.IsTimeSpan())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.TimeSpan)\n";
                    //     yield return $"{{\n";
                    //     yield return
                    //         $"    {genSerInfo.FieldName} = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeTimeSpan({genSerInfo.FieldName}__SaintsSerialized__);\n";
                    //     yield return $"}}\n";
                    // }
                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return $"(bool {genSerInfo.FieldName}Assign, global::System.Collections.Generic.Dictionary<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}Result) = "
                                     + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeDictionary<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                        yield return $"if({genSerInfo.FieldName}Assign)\n";
                        yield return $"{{\n";
                        yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}Result;\n";
                        yield return $"}}\n";

                        // No, this will break the serialization either
                        // yield return $"if({genSerInfo.FieldName} == null)\n";
                        // yield return $"{{\n";
                        // yield return $"    {genSerInfo.FieldName} = new global::System.Collections.Generic.Dictionary<{string.Join(", ", genSerInfo.FieldTypes)}>();\n";
                        // yield return $"}}\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if (genSerInfo.IsSerializeReference)
                        {
                            yield return $"(bool {genSerInfo.FieldName}Assign, global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}Result) = "
                                         + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeReferenceHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                            yield return $"if({genSerInfo.FieldName}Assign)\n";
                            yield return $"{{\n";
                            yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}Result;\n";
                            yield return $"}}\n";
                        }
                        else
                        {
                            yield return $"(bool {genSerInfo.FieldName}Assign, global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}> {genSerInfo.FieldName}Result) = "
                                         + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeHashSet<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                            yield return $"if({genSerInfo.FieldName}Assign)\n";
                            yield return $"{{\n";
                            yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}Result;\n";
                            yield return $"}}\n";
                        }


                        // No, this will break the serialization either
                        // yield return $"if({genSerInfo.FieldName} == null)\n";
                        // yield return $"{{\n";
                        // yield return $"    {genSerInfo.FieldName} = new global::System.Collections.Generic.Dictionary<{string.Join(", ", genSerInfo.FieldTypes)}>();\n";
                        // yield return $"}}\n";
                    }
                    else
                    {
                        yield return $"(bool {genSerInfo.FieldName}Ok, {genSerInfo.FieldTypes[0]} {genSerInfo.FieldName}Result) = "
                                     + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserialize<{genSerInfo.FieldTypes[0]}>({genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldTypes[0]}));\n";
                        yield return $"if({genSerInfo.FieldName}Ok)\n";
                        yield return $"{{\n";
                        yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}Result;\n";
                        yield return $"}}\n";
                    }
                }
                    break;
                case CollectionType.Array:
                {
                    // if (genSerInfo.IsDateTime())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.DateTime[])\n";
                    //     yield return $"{{\n";
                    //     yield return $"    (bool {genSerInfo.FieldName}SaintsFieldFilled, {genSerInfo.FieldType}[] {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeArrayDateTime({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                    //     yield return $"    if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                    //     yield return $"    {{\n";
                    //     yield return $"        {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                    //     yield return $"    }}\n";
                    //     yield return $"}}\n";
                    // }
                    // else if (genSerInfo.IsTimeSpan())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.DateTime[])\n";
                    //     yield return $"{{\n";
                    //     yield return
                    //         $"    (bool {genSerInfo.FieldName}SaintsFieldFilled, {genSerInfo.FieldType}[] {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeArrayTimeSpan({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                    //     yield return $"    if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                    //     yield return $"    {{\n";
                    //     yield return $"        {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                    //     yield return $"    }}\n";
                    //     yield return $"}}\n";
                    // }
                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return
                            $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.Dictionary<{string.Join(", ", genSerInfo.FieldTypes)}>[] {genSerInfo.FieldName}SaintsFieldResult) = "
                            + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeDictionaryArray<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                        yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                        yield return "{\n";
                        yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                        yield return "}\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if (genSerInfo.IsSerializeReference)
                        {
                            yield return
                                $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}>[] {genSerInfo.FieldName}SaintsFieldResult) = "
                                + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeReferenceHashSetArray<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                            yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                            yield return "{\n";
                            yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                            yield return "}\n";

                        }
                        else
                        {
                            yield return
                                $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}>[] {genSerInfo.FieldName}SaintsFieldResult) = "
                                + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeHashSetArray<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                            yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                            yield return "{\n";
                            yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                            yield return "}\n";

                        }
                    }
                    else
                    {
                        yield return
                            $"(bool {genSerInfo.FieldName}SaintsFieldFilled, {genSerInfo.FieldTypes[0]}[] {genSerInfo.FieldName}SaintsFieldResult) = "
                            + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeArray<{genSerInfo.FieldTypes[0]}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldTypes[0]}));\n";
                        yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                        yield return "{\n";
                        yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                        yield return "}\n";
                    }
                }
                    break;
                case CollectionType.List:
                {
                    // if (genSerInfo.IsDateTime())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.Collections.Generic.List<global::System.DateTime>)\n";
                    //     yield return $"{{\n";
                    //     yield return $"    (bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<{genSerInfo.FieldType}> {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeListDateTime({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                    //     yield return $"    if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                    //     yield return $"    {{\n";
                    //     yield return $"        {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                    //     yield return $"    }}\n";
                    //     yield return $"}}\n";
                    // }
                    // else if (genSerInfo.IsTimeSpan())
                    // {
                    //     yield return $"if({genSerInfo.FieldName} is global::System.Collections.Generic.List<global::System.TimeSpan>)\n";
                    //     yield return $"{{\n";
                    //     yield return
                    //         $"    (bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<{genSerInfo.FieldType}> {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeListTimeSpan({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                    //     yield return $"    if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                    //     yield return $"    {{\n";
                    //     yield return $"        {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                    //     yield return $"    }}\n";
                    //     yield return $"}}\n";
                    // }
                    if (genSerInfo.SerializationType == SerializationType.Dictionary)
                    {
                        yield return $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<{string.Join(", ", genSerInfo.FieldTypes)}>> {genSerInfo.FieldName}SaintsFieldResult) = "
                                     + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeDictionaryList<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                        yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                        yield return $"{{\n";
                        yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                        yield return $"}}\n";
                    }
                    else if (genSerInfo.SerializationType == SerializationType.HashSet)
                    {
                        if (genSerInfo.IsSerializeReference)
                        {
                            yield return $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}>> {genSerInfo.FieldName}SaintsFieldResult) = "
                                         + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeReferenceHashSetList<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                            yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                            yield return $"{{\n";
                            yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                            yield return $"}}\n";

                        }
                        else
                        {
                            yield return $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<global::System.Collections.Generic.HashSet<{string.Join(", ", genSerInfo.FieldTypes)}>> {genSerInfo.FieldName}SaintsFieldResult) = "
                                         + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeHashSetList<{string.Join(", ", genSerInfo.FieldTypes)}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__);\n";
                            yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                            yield return $"{{\n";
                            yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                            yield return $"}}\n";
                        }
                    }
                    else
                    {
                        yield return $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<{genSerInfo.FieldTypes[0]}> {genSerInfo.FieldName}SaintsFieldResult) = "
                                     + $"global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeList<{genSerInfo.FieldTypes[0]}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldTypes[0]}));\n";
                        yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                        yield return $"{{\n";
                        yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                        yield return $"}}\n";
                    }
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string FindAssetPathNotIncluded(string commonPrefix)
        {
            List<string> parts = new List<string>(commonPrefix.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            while (parts.Count > 0)
            {
                if(parts[parts.Count - 1] == "Assets")
                {
                    List<string> preParts = new List<string>(parts);
                    preParts.RemoveAt(preParts.Count - 1);

                    string joinedPath = string.Join("/", preParts);
                    bool notFound = false;
                    foreach (string subFolder in new[]{"Packages", "ProjectSettings"})
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

        private enum SerializationType
        {
            Default,
            Dictionary,
            HashSet,
        }

        private enum CollectionType
        {
            None,
            Array,
            List,
        }

        private readonly struct GenSerInfo
        {
            // public readonly bool IsProperty;
            public readonly SerializationType SerializationType;
            public readonly CollectionType CollectionType;
            public readonly IReadOnlyList<string> FieldTypes;
            public readonly string FieldName;
            public readonly ICollection<(AttributeSyntax syntax, string rawName)> Attributes;
            public readonly bool IsSerializeReference;

            public GenSerInfo(string fieldType, string fieldName, ICollection<(AttributeSyntax, string)> attributes)
            {
                FieldName = fieldName;

                // IsProperty = isProperty;
                if (fieldType.EndsWith("[]"))
                {
                    CollectionType = CollectionType.Array;
                    string rawTypeString = fieldType.Substring(0, fieldType.Length - 2);
                    (SerializationType, FieldTypes) = ParseType(rawTypeString);
                    DebugToFile($"Parse GenSerInfo SerializationType={SerializationType} CollectionType={CollectionType} for {FieldName} with {rawTypeString}");
                }
                else if(fieldType.StartsWith("List<") && fieldType.EndsWith(">"))
                {
                    CollectionType = CollectionType.List;
                    (SerializationType, FieldTypes) = ParseType(fieldType.Substring(5, fieldType.Length - 6));
                }
                else
                {
                    CollectionType = CollectionType.None;
                    (SerializationType, FieldTypes) = ParseType(fieldType);
                }

                (IsSerializeReference, Attributes) = CheckIsSerializeReference(attributes);
                // Attributes = attributes;



                DebugToFile($"New GenSerInfo SerializationType={SerializationType} CollectionType={CollectionType} for {FieldName}");
            }

            private static (bool, ICollection<(AttributeSyntax syntax, string rawName)>) CheckIsSerializeReference(ICollection<(AttributeSyntax, string)> attributes)
            {
                bool found = false;
                List<(AttributeSyntax syntax, string rawName)> resultAttributes =
                    new List<(AttributeSyntax syntax, string rawName)>();

                foreach ((AttributeSyntax originAttribute, string _) r in attributes)
                {
                    string nameString = r.originAttribute.Name.ToString();
                    switch (nameString)
                    {
                        case "ValueAttribute":
                        case "SaintsField.ValueAttribute":
                        case "global::SaintsField.ValueAttribute":
                        {
                            AttributeArgumentListSyntax argList = r.originAttribute.ArgumentList;
                            if (argList == null || argList.Arguments.Count != 1)
                            {
                                break;
                            }
                            if (!(argList.Arguments[0].Expression is TypeOfExpressionSyntax typeofExpr))
                            {
                                break;
                            }
                            string typeName = typeofExpr.Type.ToString();
                            bool isType = typeName == "SerializeReference"
                                   || typeName == "UnityEngine.SerializeReference"
                                   || typeName == "global::UnityEngine.SerializeReference";
                            if (isType)
                            {
                                found = true;
                                continue;
                            }
                        }
                            break;
                        // case "SerializeReference":
                        // case "UnityEngine.SerializeReference":
                        // case "global::UnityEngine.SerializeReference":
                        //     IsSerializeReference = true;
                        //     break;
                    }
                    resultAttributes.Add(r);
                }

                return (found, resultAttributes);
            }

            private static readonly string[] DictionaryTypePref =
            {
                "Dictionary<",
                "Generic.Dictionary<",
                "Collections.Generic.Dictionary<",
                "System.Collections.Generic.Dictionary<",
                "global::System.Collections.Generic.Dictionary<",
            };
            private static readonly string[] HashSetTypePref =
            {
                "HashSet<",
                "Generic.HashSet<",
                "Collections.Generic.HashSet<",
                "System.Collections.Generic.HashSet<",
                "global::System.Collections.Generic.HashSet<",
            };

            private static (SerializationType serializationType, IReadOnlyList<string> fieldTypes) ParseType(string typeString)
            {
                foreach (string checkDict in DictionaryTypePref)
                {
                    if (typeString.StartsWith(checkDict))
                    {
                        string rawType = typeString
                            .Substring(checkDict.Length, typeString.Length - checkDict.Length - 1);
                        DebugToFile($"Found dictionary of type {rawType}");
                        return (
                            SerializationType.Dictionary,
                            rawType.Split(',')
                                .Select(each => each.Trim())
                                .ToArray());

                    }
                }
                foreach (string checkHashSet in HashSetTypePref)
                {
                    if (typeString.StartsWith(checkHashSet))
                    {
                        string rawType = typeString
                            .Substring(checkHashSet.Length, typeString.Length - checkHashSet.Length - 1);
                        DebugToFile($"Found hashset of type {rawType}");
                        return (
                            SerializationType.HashSet,
                            rawType.Split(',')
                                .Select(each => each.Trim())
                                .ToArray());

                    }
                }

                return (SerializationType.Default, new []{typeString});
            }

            // public bool IsDateTime()
            // {
            //     return FieldType == "DateTime" || FieldType == "System.DateTime" ||
            //            FieldType == "global::System.DateTime";
            // }
            //
            // public bool IsTimeSpan()
            // {
            //     return FieldType == "TimeSpan" || FieldType == "System.TimeSpan" ||
            //            FieldType == "global::System.TimeSpan";
            // }
        }

        private static ICollection<(AttributeSyntax, string)> FoundGenSerInfo(SyntaxList<AttributeListSyntax> attributes)
        {
            bool foundSaintsSerialized = false;
            List<(AttributeSyntax, string)> extraAttributes = new List<(AttributeSyntax, string)>();

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (AttributeListSyntax attributeList in attributes)
            {
                foreach (AttributeSyntax attributeSyntax in attributeList.Attributes)
                {
                    // DebugToFile(attributeSyntax.Name.ToString());
                    string attrName = attributeSyntax.Name.ToString();
                    switch (attrName)
                    {
                        case "SaintsField.Playa.SaintsSerialized":
                        case "Playa.SaintsSerialized":
                        case "SaintsSerialized":
                            foundSaintsSerialized = true;
                            // DebugToFile($"Found SaintsSerialized on {variable.Identifier.Text}");
                            //
                            // genSerInfos.Add(new GenSerInfo(varType.ToString(),
                            //     variable.Identifier.Text
                            // ));
                            break;
                        case "NonSerialized":
                        case "System.NonSerialized":
                        case "global::System.NonSerialized":
                        case "SerializeField":
                        case "UnityEngine.SerializeField":
                        case "global::UnityEngine.SerializeField":
                        case "HideInInspector":
                        case "UnityEngine.HideInInspector":
                        case "global::UnityEngine.HideInInspector":
                            // ignore
                            break;
                        case "FormerlySerializedAs":
                        case "Serialization.FormerlySerializedAs":
                        case "UnityEngine.Serialization.FormerlySerializedAs":
                        case "global::UnityEngine.Serialization.FormerlySerializedAs":
                        {
                            AttributeArgumentListSyntax argList = attributeSyntax.ArgumentList;
                            if (argList != null && argList.Arguments.Count == 1)
                            {
                                AttributeArgumentSyntax arg = argList.Arguments[0];
                                extraAttributes.Add(
                                    (attributeSyntax, $"global::UnityEngine.Serialization.FormerlySerializedAs({arg.ToString()} + \"__SaintsSerialized__\")"));
                            }

                            break;
                        }
                        default:
                            extraAttributes.Add((attributeSyntax, attributeSyntax.ToString()));
                            break;
                    }
                }
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

// #if DEBUG
        private static string _tempFolderPath;
// #endif

        // ReSharper disable once UnusedParameter.Local
        private static void DebugToFile(string toWrite, [CallerLineNumber] int lineNumber = 0)
        {
            if (!_debug)
            {
                return;
            }
// #if DEBUG
            if(string.IsNullOrEmpty(_tempFolderPath))
            {
                _tempFolderPath = Path.GetTempPath();
            }

            // const string filePath = @"C:\Users\tyler\AppData\Local\Temp\SaintsDebug.txt";
            string tempFilePath = Path.Combine(_tempFolderPath, "SaintsDebug.txt");
            using (StreamWriter writer = new StreamWriter(tempFilePath, true, Encoding.UTF8))
            {
                writer.WriteLine($"[{lineNumber}] {toWrite}");
            }
// #endif
        }

    }

    public class Indent : IDisposable
    {
        private static int _level;
        private readonly int _addLevel;

        public Indent(int level = 1)
        {
            _addLevel = level;
            _level += level;
        }

        public void Dispose()
        {
            _level -= _addLevel;
        }

        public static string GetIndentString()
        {
            return new string(' ', _level * 4);
        }
    }
}
