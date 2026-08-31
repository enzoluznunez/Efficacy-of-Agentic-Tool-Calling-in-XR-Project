using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;
using Study.Tests.EditMode.Support;

namespace Study.Tests.EditMode.Gemini
{
    public class ToolMeasureTests
    {
        private GridFixture _fixture;
        private Parser _data;

        [SetUp]
        public void SetUp()
        {
            _fixture = TestGrid.Load();
            _data = _fixture.Source;
        }

        [TearDown]
        public void TearDown() => _fixture?.Dispose();

        [Test]
        public void CellValueReadsThroughTheOrder()
        {
            Assert.That(ToolAccess.CellValue(_data, 0, 0, out double first), Is.True);
            Assert.That(first, Is.EqualTo(1d));

            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);

            Assert.That(ToolAccess.CellValue(_data, 0, 0, out double moved), Is.True);
            Assert.That(moved, Is.EqualTo(9d));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(99, 0)]
        [TestCase(0, 99)]
        public void CellValueOutsideTheGridFails(int visRow, int visCol)
        {
            Assert.That(ToolAccess.CellValue(_data, visRow, visCol, out _), Is.False);
        }

        [Test]
        public void CellValueOnNullDataFails()
        {
            Assert.That(ToolAccess.CellValue(null, 0, 0, out _), Is.False);
        }

        [Test]
        public void CellValueOnAMissingCellFails()
        {
            using var holes = TestGrid.Load("Item\\Month,Jan,Feb\nAlpha,10,\n");

            Assert.That(ToolAccess.CellValue(holes.Source, 0, 0, out _), Is.True);
            Assert.That(ToolAccess.CellValue(holes.Source, 0, 1, out _), Is.False);
        }

        [TestCase("sum", 10d)]
        [TestCase("average", 2.5d)]
        [TestCase("max", 4d)]
        [TestCase("min", 1d)]
        public void LineMeasureAcrossARow(string measure, double expected)
        {
            Assert.That(ToolAccess.LineMeasure(_data, false, 0, 0, TestGrid.Cols - 1, measure, out double score), Is.True);
            Assert.That(score, Is.EqualTo(expected).Within(1e-9d));
        }

        [TestCase("sum", 15d)]
        [TestCase("average", 5d)]
        [TestCase("max", 9d)]
        [TestCase("min", 1d)]
        public void LineMeasureDownAColumn(string measure, double expected)
        {
            Assert.That(ToolAccess.LineMeasure(_data, true, 0, 0, TestGrid.Rows - 1, measure, out double score), Is.True);
            Assert.That(score, Is.EqualTo(expected).Within(1e-9d));
        }

        [TestCase("median")]
        [TestCase("SUM")]
        [TestCase("")]
        [TestCase(null)]
        public void LineMeasureRejectsAnUnknownMeasure(string measure)
        {
            Assert.That(ToolAccess.LineMeasure(_data, false, 0, 0, TestGrid.Cols - 1, measure, out _), Is.False);
        }

        [Test]
        public void LineMeasureSkipsMissingCells()
        {
            using var holes = TestGrid.Load("Item\\Month,Jan,Feb,Mar\nAlpha,10,,20\n");

            Assert.That(ToolAccess.LineMeasure(holes.Source, false, 0, 0, 2, "sum", out double sum), Is.True);
            Assert.That(sum, Is.EqualTo(30d));

            Assert.That(ToolAccess.LineMeasure(holes.Source, false, 0, 0, 2, "average", out double mean), Is.True);
            Assert.That(mean, Is.EqualTo(15d));
        }

        [Test]
        public void LineMeasureWithNoUsableCellsFails()
        {
            Assert.That(ToolAccess.LineMeasure(_data, false, 99, 0, TestGrid.Cols - 1, "sum", out _), Is.False);
        }

        [Test]
        public void LineMeasureFollowsAReorderedAxis()
        {
            Assert.That(ToolAccess.LineMeasure(_data, false, 0, 0, TestGrid.Cols - 1, "sum", out double before), Is.True);
            Assert.That(before, Is.EqualTo(10d));

            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);

            Assert.That(ToolAccess.LineMeasure(_data, false, 0, 0, TestGrid.Cols - 1, "sum", out double after), Is.True);
            Assert.That(after, Is.EqualTo(9d + 10d + 11d + 12d));
        }

        [Test]
        public void LineMeasureHandlesNegatives()
        {
            using var negative = TestGrid.Load("Item\\Month,Jan,Feb\nAlpha,-5,5\n");

            Assert.That(ToolAccess.LineMeasure(negative.Source, false, 0, 0, 1, "min", out double min), Is.True);
            Assert.That(min, Is.EqualTo(-5d));
            Assert.That(ToolAccess.LineMeasure(negative.Source, false, 0, 0, 1, "sum", out double sum), Is.True);
            Assert.That(sum, Is.EqualTo(0d).Within(1e-9d));
        }

        [TestCase("none", ToolType.None)]
        [TestCase("detail", ToolType.Detail)]
        [TestCase("slice", ToolType.Slice)]
        [TestCase("color", ToolType.Color)]
        [TestCase("colour", ToolType.Color)]
        [TestCase("move", ToolType.Move)]
        [TestCase("grab", ToolType.Move)]
        [TestCase("rotate", ToolType.Rotate)]
        [TestCase("scale", ToolType.Scale)]
        [TestCase("sort", ToolType.Sort)]
        [TestCase("profile", ToolType.Profile)]
        public void ParseToolAcceptsEveryName(string name, ToolType expected)
        {
            Assert.That(ToolAccess.ParseTool(name, out ToolType tool), Is.True);
            Assert.That(tool, Is.EqualTo(expected));
        }

        [TestCase("  Color  ")]
        [TestCase("COLOUR")]
        [TestCase("\tsort\n")]
        public void ParseToolIgnoresCaseAndSurroundingSpace(string name)
        {
            Assert.That(ToolAccess.ParseTool(name, out _), Is.True);
        }

        [TestCase("paint")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("7")]
        public void ParseToolRejectsJunk(string name)
        {
            Assert.That(ToolAccess.ParseTool(name, out ToolType tool), Is.False);
            Assert.That(tool, Is.EqualTo(ToolType.None));
        }

        [Test]
        public void TextReadsAJsonStringWithoutItsQuotes()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"a\":\"hello\",\"b\":7}");

            Assert.That(ToolAccess.Text(doc.RootElement.GetProperty("a")), Is.EqualTo("hello"));
            Assert.That(ToolAccess.Text(doc.RootElement.GetProperty("b")), Is.EqualTo("7"));
        }

        [Test]
        public void TextOfNullIsNull()
        {
            Assert.That(ToolAccess.Text(null), Is.Null);
        }

        [Test]
        public void RoundKeepsFourDecimals()
        {
            Assert.That(ToolAccess.Rounded(1.234567d), Is.EqualTo(1.2346m));
            Assert.That(ToolAccess.Rounded(10d), Is.EqualTo(10m));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void RoundRefusesValuesJsonCannotCarry(double value)
        {
            Assert.That(ToolAccess.Rounded(value), Is.Null);
        }

        [Test]
        public void RoundRefusesValuesBeyondDecimalRange()
        {
            Assert.That(ToolAccess.Rounded(1e30d), Is.Null);
            Assert.That(ToolAccess.Rounded(-1e30d), Is.Null);
        }

        [Test]
        public void GetFindsAPresentKey()
        {
            var args = new Dictionary<string, object> { { "a", 1 } };

            Assert.That(ToolAccess.Get(args, "a", out object value), Is.True);
            Assert.That(value, Is.EqualTo(1));
        }

        [Test]
        public void GetTreatsMissingNullAndNullMapAlike()
        {
            var args = new Dictionary<string, object> { { "a", null } };

            Assert.That(ToolAccess.Get(args, "a", out _), Is.False);
            Assert.That(ToolAccess.Get(args, "b", out _), Is.False);
            Assert.That(ToolAccess.Get(null, "a", out _), Is.False);
        }
    }
}
