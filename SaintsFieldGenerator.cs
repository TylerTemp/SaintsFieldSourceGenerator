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
// #if UNITY_EDITOR
            // Debug.Log(System.DateTime.Now.ToString());
// #endif
            string filePath = @"C:\Users\tyler\AppData\Local\Temp\SaintsDebug.txt";
            using (var writer = new StreamWriter(filePath, true, Encoding.UTF8))
            {
                foreach (SyntaxTree tree in context.Compilation.SyntaxTrees)
                {
                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    foreach (MemberDeclarationSyntax memberDeclarationSyntax in root.Members)
                    {
                        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                        switch (memberDeclarationSyntax.Kind())
                        {
                            case SyntaxKind.NamespaceDeclaration:
                            {
                                NamespaceDeclarationSyntax namespaceDeclarationSyntax =
                                    (NamespaceDeclarationSyntax)memberDeclarationSyntax;
                                string nameSpace = namespaceDeclarationSyntax.Name.ToString();

                                foreach (MemberDeclarationSyntax namespaceMemberSyntax in namespaceDeclarationSyntax.Members)
                                {
                                    if (namespaceMemberSyntax.Kind() != SyntaxKind.ClassDeclaration)
                                    {
                                        continue;
                                    }

                                    ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)namespaceMemberSyntax;

                                    if (classDeclaration.Identifier.Text != "SerEnumULong")
                                    {
                                        continue;
                                    }

                                    writer.WriteLine($"GEN: {nameSpace}: {classDeclaration.Identifier.Text}");

                                    var isPartial = false;
                                    foreach (SyntaxToken modifier in classDeclaration.Modifiers)
                                    {
                                        writer.WriteLine(modifier);

                                        if (modifier.IsKind(SyntaxKind.PartialKeyword))
                                        {
                                            isPartial = true;
                                            break;
                                        }
                                    }

                                    // if (!isPartial)
                                    // {
                                    //     continue;
                                    // }

                                    // StringBuilder sourceBuilder = new StringBuilder($"GEN: {nameSpace}: {classDeclaration.Identifier.Text} \n");
                                    // context.AddSource($"{classDeclaration.Identifier.Text}_SaintsFieldGenerated.txt",
                                    //     SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                                    StringBuilder sourceBuilder = new StringBuilder();
                                    sourceBuilder.Append($"namespace {nameSpace}\n{{\n");
                                    // sourceBuilder.Append($@"    {classDeclaration.Keyword} partial class {classDeclaration.Identifier.Text}\n    {{\n");
                                    sourceBuilder.Append($"    public partial class {classDeclaration.Identifier.Text}\n    {{\n");
                                    sourceBuilder.Append($"        public static string GeneratedStringSaintsField() => \"This is generated in {System.DateTime.Now:O}\";\n");
                                    sourceBuilder.Append("    }\n}");
                                    context.AddSource($"{classDeclaration.Identifier.Text}_SaintsFieldGenerated.cs",
                                        SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                                }
                            }
                                break;
                                // foreach (ClassContainer classContainer in ParseNamespace()
                                // {
                                //     yield return classContainer;
                                // }
                                // break;
                            // case SyntaxKind.ClassDeclaration:
                            //     yield return ParseClass((ClassDeclarationSyntax)memberDeclarationSyntax, null);
                            //     break;
                        }
                    }
                }
            }

//             StringBuilder sourceBuilder = new StringBuilder(
//             @"\
// using UnityEngine;
// namespace ExampleSourceGenerated
// {
//     public static class ExampleSourceGenerated
//     {
//         public static string GetTestText()
//         {
//             return ""This is from source generator ");
//
//             sourceBuilder.Append(System.DateTime.Now.ToString());
//
//             sourceBuilder.Append(
//                 @""";
//         }
//     }
// }
// ");
//
//             context.AddSource("exampleSourceGenerator", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }


        public void Initialize(GeneratorInitializationContext context)
        {
        }

    }
}
