using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SaintsFieldSourceGenerator
{
    public static class Utils
    {
        public static void DebugToFile(string toWrite, [CallerLineNumber] int lineNumber = 0)
        {
            if (!Debug)
            {
                return;
            }
            // #if DEBUG
            if (string.IsNullOrEmpty(_tempFolderPath))
            {
                _tempFolderPath = Path.GetTempPath();
            }

            // const string filePath = @"C:\Users\tyler\AppData\Local\Temp\SaintsDebug.txt";
            string tempFilePath = Path.Combine(_tempFolderPath, "SaintsDebug.txt");
            //tempFilePath = "/tmp/SaintsDebug.txt";
            using (StreamWriter writer = new StreamWriter(tempFilePath, true, Encoding.UTF8))
            {
                writer.WriteLine($"[{lineNumber}] {toWrite}");
            }
            // #endif
        }

        public static bool Debug = false;
        private static string _tempFolderPath;

        public static bool EqualType(ITypeSymbol type, INamedTypeSymbol targetType, string targetDisplay)
        {
            if (targetType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(type, targetType))
                {
                    return true;
                }
            }
            else
            {
                string attrDisplay = type.ToDisplayString();
                if (attrDisplay == targetDisplay)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
