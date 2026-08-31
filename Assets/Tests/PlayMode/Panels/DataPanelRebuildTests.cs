using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Study.Tests.PlayMode.Panels
{
    public class DataPanelRebuildTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
            FrameBudget.Reset();
        }

        private Parser NewParser(string csv)
        {
            var host = new GameObject("Parser");
            _spawned.Add(host);
            var parser = host.AddComponent<Parser>();
            parser.LoadFromCsvText(csv);
            return parser;
        }

        private static string Grid(int rows, int cols)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Item\\Month");
            for (int c = 0; c < cols; c++) sb.Append(",C").Append(c);
            sb.Append('\n');
            for (int r = 0; r < rows; r++)
            {
                sb.Append("R").Append(r);
                for (int c = 0; c < cols; c++) sb.Append(',').Append((r + 1) * (c + 1));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator SyntheticGridLoadsAtSize()
        {
            Parser parser = NewParser(Grid(30, 20));
            yield return null;

            Assert.That(parser.IsLoaded, Is.True);
            Assert.That(parser.RowCount, Is.EqualTo(30));
            Assert.That(parser.ColumnCount, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator FrameBudgetRecordsAcrossRealFrames()
        {
            FrameBudget.BeginPhase("test");
            int before = FrameBudget.WindowCount;

            for (int i = 0; i < 5; i++) yield return null;

            Assert.That(FrameBudget.WindowCount, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator SpanReportsElapsedFramesAcrossAYield()
        {
            StudySpan span = StudySpan.Begin("test_span");
            yield return null;
            yield return null;
            span.Dispose();

            Assert.That(StudySpan.VerboseConsole, Is.True);
        }

        [UnityTest]
        public IEnumerator LargeGridDoesNotThrow()
        {
            Parser parser = NewParser(Grid(200, 60));
            yield return null;

            Assert.That(parser.IsLoaded, Is.True);
            Assert.That(parser.RowCount * parser.ColumnCount, Is.EqualTo(12000));
        }

        [UnityTest]
        public IEnumerator RejectedLoadLeavesTheSourceUnloadedAcrossFrames()
        {
            Parser parser = NewParser("<html>nope</html>");
            yield return null;

            Assert.That(parser.IsLoaded, Is.False);
            Assert.That(parser.RawText, Is.Null);
        }
    }
}
