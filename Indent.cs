using System;

namespace SaintsFieldSourceGenerator
{
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
