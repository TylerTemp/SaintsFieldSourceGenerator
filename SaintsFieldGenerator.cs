using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
// #if UNITY_EDITOR
// using UnityEngine;
// #endif

namespace SaintsFieldSourceGenerator
{
    [Generator]
    public class SaintsFieldGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                foreach (SyntaxTree tree in context.Compilation.SyntaxTrees)
                {
                    string fileName = Path.GetFileName(tree.FilePath);
                    // DebugToFile(fileName);
                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    List<string> usingNames = new List<string>();

                    foreach (UsingDirectiveSyntax usingDirectiveSyntax in root.Usings)
                    {
                        // DebugToFile(usingDirectiveSyntax.Name);
                        usingNames.Add(usingDirectiveSyntax.Name.ToString());
                    }

                    // return;

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
                                string nameSpace = namespaceDeclarationSyntax.Name.ToString();

                                foreach (MemberDeclarationSyntax namespaceMemberSyntax in namespaceDeclarationSyntax
                                             .Members)
                                {
                                    if (namespaceMemberSyntax.Kind() != SyntaxKind.ClassDeclaration)
                                    {
                                        continue;
                                    }

                                    ClassDeclarationSyntax classDeclaration =
                                        (ClassDeclarationSyntax)namespaceMemberSyntax;

                                    // MonoBehavior requires class name == file name
                                    if (classDeclaration.Identifier.Text !=
                                        fileName.Substring(0, fileName.Length - 3))
                                    {
                                        continue;
                                    }

                                    bool isPartial = false;
                                    List<string> modifiersList = new List<string>();
                                    foreach (SyntaxToken modifier in classDeclaration.Modifiers)
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
                                        continue;
                                    }


                                    if (classDeclaration.BaseList == null ||
                                        classDeclaration.BaseList.Types.Count == 0)
                                    {
                                        continue;
                                    }

                                    if (classDeclaration.Identifier.Text != "SerEnumULong")
                                    {
                                        continue;
                                    }

                                    DebugToFile($"GEN: {nameSpace}: {classDeclaration.Identifier.Text}");

                                    List<GenSerInfo> genSerInfos = new List<GenSerInfo>();

                                    foreach (MemberDeclarationSyntax classDeclarationMember in classDeclaration.Members)
                                    {
                                        switch (classDeclarationMember.Kind())
                                        {
                                            case SyntaxKind.FieldDeclaration:
                                            {
                                                FieldDeclarationSyntax fieldDeclarationSyntax =
                                                    (FieldDeclarationSyntax)classDeclarationMember;
                                                TypeSyntax varType = fieldDeclarationSyntax.Declaration.Type;
                                                DebugToFile(varType.ToString());
                                                VariableDeclaratorSyntax variable = fieldDeclarationSyntax.Declaration.Variables[0];
                                                DebugToFile(variable.Identifier.Text);

                                                ICollection<string> attributeStrings = FoundGenSerInfo(fieldDeclarationSyntax.AttributeLists);

                                                if (attributeStrings != null)
                                                {
                                                    DebugToFile($"Found SaintsSerialized on {variable.Identifier.Text}");

                                                    genSerInfos.Add(new GenSerInfo(
                                                        varType.ToString(),
                                                        variable.Identifier.Text,
                                                        attributeStrings
                                                    ));

                                                }
                                            }
                                                break;
                                            case SyntaxKind.PropertyDeclaration:
                                            {
                                                PropertyDeclarationSyntax propertyDeclarationSyntax =
                                                    (PropertyDeclarationSyntax)classDeclarationMember;
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
                                        }
                                    }

                                    if (genSerInfos.Count > 0)
                                    {
                                        // StringBuilder sourceBuilder = new StringBuilder($"GEN: {nameSpace}: {classDeclaration.Identifier.Text} \n");
                                        // context.AddSource($"{classDeclaration.Identifier.Text}_SaintsFieldGenerated.txt",
                                        //     SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                                        StringBuilder sourceBuilder = new StringBuilder();
                                        foreach (string usingName in usingNames)
                                        {
                                            sourceBuilder.Append($"using {usingName};\n");
                                        }
                                        sourceBuilder.Append($"namespace {nameSpace}\n{{\n");
                                        // sourceBuilder.Append($@"    {classDeclaration.Keyword} partial class {classDeclaration.Identifier.Text}\n    {{\n");
                                        sourceBuilder.Append(
                                            $"    {string.Join(" ", modifiersList)} class {classDeclaration.Identifier.Text}: global::UnityEngine.ISerializationCallbackReceiver\n    {{\n");

                                        // sourceBuilder.Append(
                                        //     $"        public static string GeneratedStringSaintsField() => \"This is generated in {System.DateTime.Now:O}\";\n");

                                        foreach (GenSerInfo genSerInfo in genSerInfos)
                                        {
                                            sourceBuilder.Append("\n");

                                            // sourceBuilder.Append("        [global::SaintsField.Playa.ShowIf(\n");
                                            // sourceBuilder.Append("#if SAINTSFIELD_SERIALIZED_DEBUG\n");
                                            // sourceBuilder.Append("         true\n");
                                            // sourceBuilder.Append("#else\n");
                                            // sourceBuilder.Append("         false\n");
                                            // sourceBuilder.Append("#endif\n");
                                            // sourceBuilder.Append("        )]\n");

                                            sourceBuilder.Append($"        [global::SaintsField.Utils.SaintsSerializedActual(nameof({genSerInfo.FieldName}), typeof({genSerInfo.FieldType}))]\n");
                                            sourceBuilder.Append("        [global::SaintsField.SaintsRow(inline: true)]\n");
                                            sourceBuilder.Append("        [global::UnityEngine.SerializeField]\n");
                                            if(genSerInfo.Attributes.Count > 0)
                                            {
                                                sourceBuilder.Append(
                                                    $"        [{string.Join(", ", genSerInfo.Attributes)}]\n");
                                            }

                                            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                            if (genSerInfo.CollectionType == CollectionType.None)
                                            {
                                                sourceBuilder.Append(
                                                    $"        private global::SaintsField.SaintsSerialization.SaintsSerializedProperty {genSerInfo.FieldName}__SaintsSerialized__;\n");
                                            }
                                            else
                                            {
                                                sourceBuilder.Append(
                                                    $"        private global::SaintsField.SaintsSerialization.SaintsSerializedProperty[] {genSerInfo.FieldName}__SaintsSerialized__ = global::System.Array.Empty<global::SaintsField.SaintsSerialization.SaintsSerializedProperty>();\n");

                                            }
                                        }

                                        sourceBuilder.Append("\n");
                                        sourceBuilder.Append("        public void OnBeforeSerialize()\n");
                                        sourceBuilder.Append("        {\n");
                                        foreach (GenSerInfo genSerInfo in genSerInfos)
                                        {
                                            if(genSerInfo.CollectionType != CollectionType.None)
                                            {
                                                sourceBuilder.Append("\n");
                                                sourceBuilder.Append($"            if ({genSerInfo.FieldName} == null)\n");
                                                sourceBuilder.Append("            {\n");
                                                sourceBuilder.Append($"                {genSerInfo.FieldName} = ");
                                                switch (genSerInfo.CollectionType)
                                                {
                                                    case CollectionType.Array:
                                                        sourceBuilder.Append("global::System.Array.Empty<");
                                                        sourceBuilder.Append(genSerInfo.FieldType);
                                                        sourceBuilder.Append(">();\n");
                                                        break;
                                                    case CollectionType.List:
                                                        sourceBuilder.Append("new global::System.Collections.Generic.List<");
                                                        sourceBuilder.Append(genSerInfo.FieldType);
                                                        sourceBuilder.Append(">();\n");
                                                        break;
                                                }
                                                sourceBuilder.Append("            }\n");
                                            }

                                            switch (genSerInfo.CollectionType)
                                            {
                                                case CollectionType.None:
                                                    sourceBuilder.Append(
                                                        $"            {genSerInfo.FieldName}__SaintsSerialized__ = global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerialize({genSerInfo.FieldName}, typeof({genSerInfo.FieldType}));\n");
                                                    break;
                                                case CollectionType.Array:
                                                case CollectionType.List:
                                                    sourceBuilder.Append(
                                                        $"            global::SaintsField.Utils.SaintsSerializedUtil.OnBeforeSerializeCollection<{genSerInfo.FieldType}>(ref {genSerInfo.FieldName}__SaintsSerialized__, {genSerInfo.FieldName}, typeof({genSerInfo.FieldType}));\n");
                                                    break;
                                                default:
                                                    throw new ArgumentOutOfRangeException();
                                            }
                                        }
                                        sourceBuilder.Append("        }\n");

                                        sourceBuilder.Append("        public void OnAfterDeserialize()\n");
                                        sourceBuilder.Append("        {\n");
                                        foreach (GenSerInfo genSerInfo in genSerInfos)
                                        {
                                            switch (genSerInfo.CollectionType)
                                            {
                                                case CollectionType.None:
                                                    sourceBuilder.Append(
                                                        $"            {genSerInfo.FieldName} = ({genSerInfo.FieldType})global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserialize({genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldType}));\n");
                                                    break;
                                                case CollectionType.Array:
                                                    sourceBuilder.Append("\n");
                                                    sourceBuilder.Append($"            (bool {genSerInfo.FieldName}SaintsFieldFilled, {genSerInfo.FieldType}[] {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeArray<{genSerInfo.FieldType}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldType}));\n");
                                                    sourceBuilder.Append($"            if(!{genSerInfo.FieldName}SaintsFieldFilled)\n");
                                                    sourceBuilder.Append("            {\n");
                                                    sourceBuilder.Append($"                {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n");
                                                    sourceBuilder.Append("            }\n");
                                                    break;
                                                case CollectionType.List:
                                                    sourceBuilder.Append("\n");
                                                    sourceBuilder.Append($"            (bool {genSerInfo.FieldName}SaintsFieldFilled, global::System.Collections.Generic.List<{genSerInfo.FieldType}> {genSerInfo.FieldName}SaintsFieldResult) = global::SaintsField.Utils.SaintsSerializedUtil.OnAfterDeserializeList<{genSerInfo.FieldType}>({genSerInfo.FieldName}, {genSerInfo.FieldName}__SaintsSerialized__, typeof({genSerInfo.FieldType}));\n");
                                                    sourceBuilder.Append($"            if(!{genSerInfo.FieldName}SaintsFieldFilled)\n");
                                                    sourceBuilder.Append("            {\n");
                                                    sourceBuilder.Append($"                {genSerInfo.FieldName} = {genSerInfo.FieldName}SaintsFieldResult;\n");
                                                    sourceBuilder.Append("            }\n");
                                                    break;
                                                default:
                                                    throw new ArgumentOutOfRangeException();
                                            }

                                        }
                                        sourceBuilder.Append("        }\n");

                                        sourceBuilder.Append("    }\n}");
                                        context.AddSource($"{classDeclaration.Identifier.Text}.SaintsSerialized.cs",
                                            SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                                    }

                                }
                            }
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugToFile(e.Message);
                DebugToFile(e.StackTrace);
            }
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

        private static void DebugToFile(string toWrite)
        {
            // const string filePath = @"C:\Users\tyler\AppData\Local\Temp\SaintsDebug.txt";
            // using (StreamWriter writer = new StreamWriter(filePath, true, Encoding.UTF8))
            // {
            //     writer.WriteLine(toWrite);
            // }
        }

    }
}
