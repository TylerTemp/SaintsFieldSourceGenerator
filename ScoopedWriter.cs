using System.Collections.Generic;
using System.Text;

namespace SaintsFieldSourceGenerator
{
    public class ScoopedWriter : IWriter
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

            using (new Indent(NamespaceName == null ? 0 : 1))
            {
                Utils.DebugToFile($"write SubClassOrStructWriters {SubClassOrStructWriters.Count}");
                foreach (ClassOrStructWriter classOrStructWriter in SubClassOrStructWriters)
                {
                    Utils.DebugToFile($"write sub {classOrStructWriter}");
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
}
