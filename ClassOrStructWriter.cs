using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaintsFieldSourceGenerator
{
    public class ClassOrStructWriter : IWriter
    {
        public bool IsClass;

        public IReadOnlyList<ClassOrStructWriter> SubClassOrStructWriters = new List<ClassOrStructWriter>();
        public string Declare;
        public IReadOnlyList<GenSerInfo> SerializedInfos;

        public string Write()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Indent.GetIndentString()}{Declare}");
            if (SerializedInfos.Count > 0)
            {
                sb.Append(": global::UnityEngine.ISerializationCallbackReceiver");
            }
            sb.Append("\n");
            sb.Append($"{Indent.GetIndentString()}{{\n");

            using (new Indent())
            {
                if (SerializedInfos.Count > 0)
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

        private static IEnumerable<string> WriteGenSerInfoFields(bool isClass, GenSerInfo genSerInfo)
        {
            yield return "[global::UnityEngine.SerializeField]\n";
            // yield return $"[global::SaintsField.Utils.SaintsSerializedActual(nameof({genSerInfo.FieldName}), typeof({genSerInfo.FieldType}))]\n";
            yield return $"[global::SaintsField.Utils.SaintsSerializedActual(nameof({genSerInfo.FieldName}))]\n";
            if (genSerInfo.SerializationType != SerializationType.Dictionary && genSerInfo.SerializationType != SerializationType.HashSet)
            {
                // yield return "[global::SaintsField.SaintsRow(inline: true)]\n";
                yield return $"[global::SaintsField.Utils.SaintsSerializedActualDrawer]\n";
            }
            if (genSerInfo.Attributes.Count > 0)
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
                        if (genSerInfo.IsSerializeReference)
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
                Utils.DebugToFile($"genSerInfo.SerializationType={genSerInfo.SerializationType} for {genSerInfo.FieldName}");
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
                            if (genSerInfo.IsSerializeReference)
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

        private static IEnumerable<string> WriteOnBeforeSerialize(GenSerInfo genSerInfo)
        {
            if (genSerInfo.CollectionType != CollectionType.None)
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
                        if (genSerInfo.IsSerializeReference)
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
    }
}
