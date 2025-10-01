using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SaintsFieldSourceGenerator
{
    [Generator]
    public class SaintsSerializedGenerator : ISourceGenerator
    {

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


                    string relativePath = norPath.Substring(assetPathNotIncluded.Length + "/Assets".Length + 1);
                    string fileBaseName = Path.GetFileNameWithoutExtension(relativePath);

                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    // ScoopedWriter rootWriter = new ScoopedWriter();

                    List<string> usingNames = new List<string>();

                    foreach (UsingDirectiveSyntax usingDirectiveSyntax in root.Usings)
                    {
                        // DebugToFile(usingDirectiveSyntax.Name);
                        usingNames.Add(usingDirectiveSyntax.Name.ToString());
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
                        sourceBuilder.Append($"using {usingName};\n");
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
            yield return $"[global::SaintsField.Utils.SaintsSerializedActual(nameof({genSerInfo.FieldName}), typeof({genSerInfo.FieldType}))]\n";
            yield return "[global::SaintsField.SaintsRow(inline: true)]\n";
            if(genSerInfo.Attributes.Count > 0)
            {
                yield return $"[{string.Join(", ", genSerInfo.Attributes)}]\n";
            }

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (genSerInfo.CollectionType == CollectionType.None)
            {
                string equal = isClass ? " = new global::SaintsField.SaintsSerialization.SaintsSerializedProperty()": "";
                yield return $"private global::SaintsField.SaintsSerialization.SaintsSerializedProperty {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";
            }
            else
            {
                string equal = isClass ? " = global::System.Array.Empty<global::SaintsField.SaintsSerialization.SaintsSerializedProperty>()": "";
                yield return $"private global::SaintsField.SaintsSerialization.SaintsSerializedProperty[] {genSerInfo.FieldName}__SaintsSerialized__{equal};\n";

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
            return ParseClassOrStructDeclarationSyntax(true, classDeclaration.Modifiers, classDeclaration.Identifier.Text, classDeclaration.Members);

        }

        private ClassOrStructWriter ParseStructDeclarationSyntax(StructDeclarationSyntax structDeclaration)
        {
            return ParseClassOrStructDeclarationSyntax(false, structDeclaration.Modifiers, structDeclaration.Identifier.Text, structDeclaration.Members);
        }

        private ClassOrStructWriter ParseClassOrStructDeclarationSyntax(bool isClass, SyntaxTokenList modifiers, string identifierText, SyntaxList<MemberDeclarationSyntax> members)
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

            string declare = $"{string.Join(" ", modifiersList)} {(isClass? "class": "struct")} {identifierText}";

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
                        DebugToFile(varType.ToString());

                        ICollection<string> attributeStrings = FoundGenSerInfo(fieldDeclarationSyntax.AttributeLists);
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
                        DebugToFile(varType.ToString());
                        SyntaxToken identifier = propertyDeclarationSyntax.Identifier;
                        DebugToFile(identifier.Text);

                        ICollection<string> attributeStrings = FoundGenSerInfo(propertyDeclarationSyntax.AttributeLists);
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
                string equal = null;
                switch (genSerInfo.CollectionType)
                {
                    case CollectionType.Array:
                        equal = $"global::System.Array.Empty<{genSerInfo.FieldType}>()";
                        break;
                    case CollectionType.List:
                        equal = $"new global::System.Collections.Generic.List<{genSerInfo.FieldType}>()";
                        break;
                }

                yield return $"    {genSerInfo.FieldName} = {equal};\n";

                yield return "}\n";
            }

            switch (genSerInfo.CollectionType)
            {
                case CollectionType.None:
                    yield return
                        $"{genSerInfo.FieldName}__SaintsSerialized__ = global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerialize({genSerInfo.FieldName}, typeof({genSerInfo.FieldType}));\n";
                    break;
                case CollectionType.Array:
                case CollectionType.List:
                    yield return $"global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollection<{genSerInfo.FieldType}>(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName}, typeof({genSerInfo.FieldType}));\n";
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
                    yield return $"{genSerInfo.FieldName} = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserialize<{genSerInfo.FieldType}>({genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldType}));\n";
                    break;
                case CollectionType.Array:
                    yield return $"(bool {genSerInfo.FieldName}SaintsFieldFilled, {genSerInfo.FieldType}[] {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeArray<{genSerInfo.FieldType}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldType}));\n";
                    yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                    yield return "{\n";
                    yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                    yield return "}\n";
                    break;
                case CollectionType.List:
                    yield return $"(bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<{genSerInfo.FieldType}> {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeList<{genSerInfo.FieldType}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldType}));\n";
                    yield return $"if(!{genSerInfo.FieldName}SaintsFieldFilled)\n";
                    yield return "{\n";
                    yield return $"    {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n";
                    yield return "}\n";
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

        private enum CollectionType
        {
            None,
            Array,
            List,
        }

        private readonly struct GenSerInfo
        {
            // public readonly bool IsProperty;
            public readonly CollectionType CollectionType;
            public readonly string FieldType;
            public readonly string FieldName;
            public readonly ICollection<string> Attributes;

            public GenSerInfo(string fieldType, string fieldName, ICollection<string> attributes)
            {
                // IsProperty = isProperty;
                if (fieldType.EndsWith("[]"))
                {
                    CollectionType = CollectionType.Array;
                    FieldType = fieldType.Substring(0, fieldType.Length - 2);
                }
                else if(fieldType.StartsWith("List<") && fieldType.EndsWith(">"))
                {
                    CollectionType = CollectionType.List;
                    FieldType = fieldType.Substring(5, fieldType.Length - 6);
                }
                else
                {
                    CollectionType = CollectionType.None;
                    FieldType = fieldType;
                }
                FieldName = fieldName;
                Attributes = attributes;
            }
        }

        private ICollection<string> FoundGenSerInfo(SyntaxList<AttributeListSyntax> attributes)
        {
            bool foundSaintsSerialized = false;
            List<string> extraAttributes = new List<string>();

            foreach (AttributeListSyntax attributeList in attributes)
            {
                foreach (AttributeSyntax attributeSyntax in attributeList.Attributes)
                {
                    DebugToFile(attributeSyntax.Name.ToString());
                    string attrName = attributeSyntax.Name.ToString();
                    if(attrName == "SaintsField.Playa.SaintsSerialized" || attrName == "Playa.SaintsSerialized" || attrName == "SaintsSerialized")
                    {
                        foundSaintsSerialized = true;
                        // DebugToFile($"Found SaintsSerialized on {variable.Identifier.Text}");
                        //
                        // genSerInfos.Add(new GenSerInfo(varType.ToString(),
                        //     variable.Identifier.Text
                        // ));
                    }
                    else if(attrName == "NonSerialized" || attrName == "System.NonSerialized" || attrName == "global::System.NonSerialized"
                            || attrName == "SerializeField" || attrName == "UnityEngine.SerializeField" || attrName == "global::UnityEngine.SerializeField")
                    {
                        // ignore
                    }
                    else if(attrName == "FormerlySerializedAs" || attrName == "Serialization.FormerlySerializedAs" || attrName == "UnityEngine.Serialization.FormerlySerializedAs" || attrName == "global::UnityEngine.Serialization.FormerlySerializedAs")
                    {
                        AttributeArgumentListSyntax argList = attributeSyntax.ArgumentList;
                        if (argList != null && argList.Arguments.Count == 1)
                        {
                            AttributeArgumentSyntax arg = argList.Arguments[0];
                            extraAttributes.Add($"global::UnityEngine.Serialization.FormerlySerializedAs({arg.ToString()} + \"__SaintsSerialized__\")");
                        }
                    }
                    else
                    {
                        extraAttributes.Add(attrName);
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

#if DEBUG
        private static string _tempFolderPath;
#endif

        // ReSharper disable once UnusedParameter.Local
        private static void DebugToFile(string toWrite)
        {
#if DEBUG
            if(string.IsNullOrEmpty(_tempFolderPath))
            {
                _tempFolderPath = Path.GetTempPath();
            }

            // const string filePath = @"C:\Users\tyler\AppData\Local\Temp\SaintsDebug.txt";
            string tempFilePath = Path.Combine(_tempFolderPath, "SaintsDebug.txt");
            using (StreamWriter writer = new StreamWriter(tempFilePath, true, Encoding.UTF8))
            {
                writer.WriteLine(toWrite);
            }
#endif
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
