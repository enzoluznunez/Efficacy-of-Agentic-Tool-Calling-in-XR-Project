using System.Collections.Generic;
using Google.GenAI.Types;

namespace Study.Tests.PlayMode.Support
{
    public sealed class ToolAccess : AgenticTool
    {
        public override FunctionDeclaration Declaration => new FunctionDeclaration { Name = "PlayModeToolAccess" };

        protected override void Run(Dictionary<string, object> args, Dictionary<string, object> result) { }

        public static bool ResolveLine(object arg, bool columns, int min, int max,
            Dictionary<string, object> result, out int visIndex) =>
            TryResolveLine(arg, columns, min, max, result, out visIndex);

        public static bool ResolveLines(IReadOnlyList<string> tokens, bool columns, int min, int max,
            Dictionary<string, object> result, out List<int> visIndexes) =>
            TryResolveLines(tokens, columns, min, max, result, out visIndexes);

        public static bool InferAxis(string token, out bool columns) => TryInferAxis(token, out columns);

        public static bool TitleOnAxis(string token, bool columns) => TitleExistsOnAxis(token, columns);
    }
}
