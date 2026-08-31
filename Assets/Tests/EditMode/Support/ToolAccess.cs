using System.Collections.Generic;
using System.Threading.Tasks;
using Google.GenAI.Types;

namespace Study.Tests.EditMode.Support
{
    public sealed class ToolAccess : AgenticTool
    {
        public override FunctionDeclaration Declaration => new FunctionDeclaration { Name = "ToolAccess" };

        protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) { }

        public static bool CellValue(DataSource data, int visRow, int visCol, out double value) =>
            TryCellValue(data, visRow, visCol, out value);

        public static bool LineMeasure(DataSource data, bool isColumn, int line,
            int crossMin, int crossMax, string measure, out double score) =>
            TryLineMeasure(data, isColumn, line, crossMin, crossMax, measure, out score);

        public static bool ParseTool(string s, out ToolType tool) => TryParseTool(s, out tool);

        public static string Text(object v) => AsString(v);

        public static object Rounded(double v) => Round(v);

        public static bool Get(Dictionary<string, object> args, string key, out object value) =>
            TryGet(args, key, out value);
    }
}
