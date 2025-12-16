using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SaintsFieldSourceGenerator
{
    public readonly struct GenSerInfo
    {
        // public readonly bool IsProperty;
        public readonly SerializationType SerializationType;
        public readonly CollectionType CollectionType;
        public readonly IReadOnlyList<string> FieldTypes;
        public readonly string FieldName;
        public readonly ICollection<(AttributeData attributeData, string rawName)> Attributes;
        public readonly bool IsSerializeReference;

        public GenSerInfo(Compilation compilation, ITypeSymbol fieldType, string fieldName, ICollection<(AttributeData, string)> attributes)
        {
            FieldName = fieldName;

            // IsProperty = isProperty;
            if (fieldType is IArrayTypeSymbol arrayType)
            {
                CollectionType = CollectionType.Array;
                ITypeSymbol subType = arrayType.ElementType;
                // string rawTypeString = fieldType.Substring(0, fieldType.Length - 2);
                (SerializationType, FieldTypes) = ParseType(compilation, subType);
                // Utils.DebugToFile($"Parse GenSerInfo SerializationType={SerializationType} CollectionType={CollectionType} for {FieldName} with {rawTypeString}");
            }
            // else if (fieldType.StartsWith("List<") && fieldType.EndsWith(">"))
            else if (fieldType is INamedTypeSymbol generic1Type
                     && generic1Type.IsGenericType
                     && generic1Type.TypeArguments.Length == 1)
            {
                INamedTypeSymbol listType = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
                if (listType != null &&
                    SymbolEqualityComparer.Default.Equals(generic1Type.OriginalDefinition, listType))
                {
                    CollectionType = CollectionType.List;
                    ITypeSymbol subType = generic1Type.TypeArguments[0];
                    (SerializationType, FieldTypes) = ParseType(compilation, subType);
                }
                else
                {
                    CollectionType = CollectionType.None;
                    (SerializationType, FieldTypes) = ParseType(compilation, fieldType);
                }
            }
            else
            {
                CollectionType = CollectionType.None;
                (SerializationType, FieldTypes) = ParseType(compilation, fieldType);
            }

            (IsSerializeReference, Attributes) = CheckIsSerializeReference(compilation, attributes);
            // Attributes = attributes;



            Utils.DebugToFile($"New GenSerInfo SerializationType={SerializationType} CollectionType={CollectionType} for {FieldName}");
        }

        private static (bool, ICollection<(AttributeData syntax, string rawName)>) CheckIsSerializeReference(Compilation compilation, ICollection<(AttributeData, string)> attributes)
        {
            bool found = false;
            List<(AttributeData syntax, string rawName)> resultAttributes =
                new List<(AttributeData syntax, string rawName)>();

            foreach ((AttributeData originAttribute, string _) r in attributes)
            {
                INamedTypeSymbol valueAttribute = compilation.GetTypeByMetadataName("SaintsField.ValueAttributeAttribute");
                if (Utils.EqualType(r.originAttribute.AttributeClass, valueAttribute, "SaintsField.ValueAttributeAttribute"))
                {
                    if (r.originAttribute.ConstructorArguments.Length == 0)
                    {
                        resultAttributes.Add(r);
                        continue;
                    }

                    TypedConstant arg = r.originAttribute.ConstructorArguments[0];
                    if (arg.Kind != TypedConstantKind.Type)
                    {
                        resultAttributes.Add(r);
                        continue;
                    }

                    if (!(arg.Value is ITypeSymbol valueType))
                    {
                        resultAttributes.Add(r);
                        continue;
                    }

                    INamedTypeSymbol serializeRefType = compilation.GetTypeByMetadataName("UnityEngine.SerializeReference");
                    if (!Utils.EqualType(valueType, serializeRefType, "UnityEngine.SerializeReference"))
                    {
                        resultAttributes.Add(r);
                        continue;
                    }

                    found = true;  // don't add for this!
                    continue;
                }

                resultAttributes.Add(r);
            }

            return (found, resultAttributes);
        }

        // private static readonly string[] DictionaryTypePref =
        // {
        //     "Dictionary<",
        //     "Generic.Dictionary<",
        //     "Collections.Generic.Dictionary<",
        //     "System.Collections.Generic.Dictionary<",
        //     "global::System.Collections.Generic.Dictionary<",
        // };
        // private static readonly string[] HashSetTypePref =
        // {
        //     "HashSet<",
        //     "Generic.HashSet<",
        //     "Collections.Generic.HashSet<",
        //     "System.Collections.Generic.HashSet<",
        //     "global::System.Collections.Generic.HashSet<",
        // };

        private static (SerializationType serializationType, IReadOnlyList<string> fieldTypes) ParseType(Compilation compilation, ITypeSymbol type)
        {
            if (!(type is INamedTypeSymbol named) || !named.IsGenericType)
            {
                return (SerializationType.Default, new[] { type.ToDisplayString() });
            }

            INamedTypeSymbol dictType =
                compilation.GetTypeByMetadataName(
                    "System.Collections.Generic.Dictionary`2");
            if (Utils.EqualType(named.OriginalDefinition, dictType, "System.Collections.Generic.Dictionary`2"))
            {
                ITypeSymbol keyType = named.TypeArguments[0];
                ITypeSymbol valueType = named.TypeArguments[1];
                return (
                    SerializationType.Dictionary,
                    new[]
                    {
                        keyType.ToDisplayString(),
                        valueType.ToDisplayString(),
                    });
            }

            // HashSet<T>
            INamedTypeSymbol hashSetType =
                compilation.GetTypeByMetadataName(
                    "System.Collections.Generic.HashSet`1");

            if (Utils.EqualType(named.OriginalDefinition, hashSetType, "System.Collections.Generic.HashSet`1"))
            {
                ITypeSymbol valueType = named.TypeArguments[0];
                return (SerializationType.HashSet, new[] { valueType.ToDisplayString() });
            }

            return (SerializationType.Default, new[] { type.ToDisplayString() });
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
}
