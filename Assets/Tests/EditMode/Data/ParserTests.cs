using NUnit.Framework;
using UnityEngine;

namespace Study.Tests.EditMode.Data
{
    public class ParserTests
    {
        private Parser _parser;
        private GameObject _host;
        private bool? _ok;
        private string _reason;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ParserUnderTest");
            _parser = _host.AddComponent<Parser>();
            _ok = null;
            _reason = null;
            _parser.onLoadResult = (ok, reason) => { _ok = ok; _reason = reason; };
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }

        private void Load(string csv) => _parser.LoadFromCsvText(csv);

        [Test]
        public void ReadsHeaderRowsAndValues()
        {
            Load("Item\\Month,Jan,Feb\nBao,10,20\nLamb,30,40");

            Assert.That(_ok, Is.True);
            Assert.That(_parser.RowCount, Is.EqualTo(2));
            Assert.That(_parser.ColumnCount, Is.EqualTo(2));
            Assert.That(_parser.RowTitles, Is.EqualTo(new[] { "Bao", "Lamb" }));
            Assert.That(_parser.ColumnTitles, Is.EqualTo(new[] { "Jan", "Feb" }));
            Assert.That(_parser.GetValue(0, 0), Is.EqualTo(10f));
            Assert.That(_parser.GetValue(1, 1), Is.EqualTo(40f));
        }

        [Test]
        public void SplitsAxisTitlesOnBackslash()
        {
            Load("Item\\Month,Jan\nBao,10");

            Assert.That(_parser.RowAxisTitle, Is.EqualTo("Item"));
            Assert.That(_parser.ColumnAxisTitle, Is.EqualTo("Month"));
        }

        [Test]
        public void SplitsAxisTitlesOnForwardSlash()
        {
            Load("Item/Month,Jan\nBao,10");

            Assert.That(_parser.RowAxisTitle, Is.EqualTo("Item"));
            Assert.That(_parser.ColumnAxisTitle, Is.EqualTo("Month"));
        }

        [Test]
        public void CornerWithoutSeparatorSetsRowAxisOnly()
        {
            Load("Item,Jan\nBao,10");

            Assert.That(_parser.RowAxisTitle, Is.EqualTo("Item"));
            Assert.That(_parser.ColumnAxisTitle, Is.Null);
        }

        [Test]
        public void EmptyCornerLeavesBothAxisTitlesNull()
        {
            Load(",Jan\nBao,10");

            Assert.That(_parser.RowAxisTitle, Is.Null);
            Assert.That(_parser.ColumnAxisTitle, Is.Null);
        }

        [Test]
        public void TrimsWhitespaceFromTitles()
        {
            Load(",  Jan  ,Feb\n  Bao  ,10,20");

            Assert.That(_parser.ColumnTitles[0], Is.EqualTo("Jan"));
            Assert.That(_parser.RowTitles[0], Is.EqualTo("Bao"));
        }

        [Test]
        public void QuotedFieldKeepsItsComma()
        {
            Load(",Jan\n\"Bao, Steamed\",10");

            Assert.That(_parser.RowCount, Is.EqualTo(1));
            Assert.That(_parser.RowTitles[0], Is.EqualTo("Bao, Steamed"));
            Assert.That(_parser.GetValue(0, 0), Is.EqualTo(10f));
        }

        [Test]
        public void DoubledQuoteInsideQuotedFieldUnescapes()
        {
            Load(",Jan\n\"He said \"\"hi\"\"\",10");

            Assert.That(_parser.RowTitles[0], Is.EqualTo("He said \"hi\""));
        }

        [Test]
        public void SkipsBlankAndWhitespaceOnlyLines()
        {
            Load(",Jan\n\nBao,10\n   \nLamb,20\n");

            Assert.That(_parser.RowCount, Is.EqualTo(2));
            Assert.That(_parser.RowTitles, Is.EqualTo(new[] { "Bao", "Lamb" }));
        }

        [Test]
        public void HandlesCarriageReturnLineEndings()
        {
            Load(",Jan,Feb\r\nBao,10,20\r\nLamb,30,40");

            Assert.That(_parser.RowCount, Is.EqualTo(2));
            Assert.That(_parser.ColumnCount, Is.EqualTo(2));
            Assert.That(_parser.GetValue(1, 1), Is.EqualTo(40f));
        }

        [Test]
        public void ParsesDecimalsWithInvariantCulture()
        {
            Load(",Jan\nBao,1.5");

            Assert.That(_parser.GetValue(0, 0), Is.EqualTo(1.5f).Within(1e-6f));
        }

        [Test]
        public void ParsesNegativeValues()
        {
            Load(",Jan,Feb\nBao,-5,10");

            Assert.That(_parser.GetValue(0, 0), Is.EqualTo(-5f));
        }

        [Test]
        public void MissingTrailingFieldHasNoValue()
        {
            Load(",Jan,Feb\nBao,10");

            Assert.That(_parser.HasValue(0, 0), Is.True);
            Assert.That(_parser.HasValue(0, 1), Is.False);
        }

        [Test]
        public void NonNumericCellHasNoValue()
        {
            Load(",Jan,Feb\nBao,n/a,20");

            Assert.That(_parser.HasValue(0, 0), Is.False);
            Assert.That(_parser.HasValue(0, 1), Is.True);
        }

        [Test]
        public void EmptyCellHasNoValue()
        {
            Load(",Jan,Feb\nBao,,20");

            Assert.That(_parser.HasValue(0, 0), Is.False);
        }

        [Test]
        public void OrdersStartAsIdentity()
        {
            Load(",Jan,Feb,Mar\nBao,1,2,3\nLamb,4,5,6");

            Assert.That(_parser.RowOrder, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(_parser.ColumnOrder, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void RejectsHtmlPayload()
        {
            Load("<!DOCTYPE html><html><body>nope</body></html>");

            Assert.That(_ok, Is.False);
            Assert.That(_reason, Does.Contain("web page"));
            Assert.That(_parser.RowCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectsHtmlWithLeadingWhitespace()
        {
            Load("\n   <html></html>");

            Assert.That(_ok, Is.False);
            Assert.That(_reason, Does.Contain("web page"));
        }

        [Test]
        public void RejectsHeaderWithoutDataRows()
        {
            Load(",Jan,Feb");

            Assert.That(_ok, Is.False);
            Assert.That(_reason, Does.Contain("no data rows"));
            Assert.That(_parser.RowCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectsGridWithNoNumericValues()
        {
            Load(",Jan,Feb\nBao,x,y\nLamb,z,w");

            Assert.That(_ok, Is.False);
            Assert.That(_reason, Does.Contain("numeric"));
            Assert.That(_parser.RowCount, Is.EqualTo(0));
            Assert.That(_parser.ColumnCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectsHeaderWithNoColumns()
        {
            Load("Item\nBao");

            Assert.That(_ok, Is.False);
            Assert.That(_parser.ColumnCount, Is.EqualTo(0));
        }

        [Test]
        public void FailedReloadClearsThePreviousGrid()
        {
            Load(",Jan,Feb\nBao,10,20");
            Assert.That(_parser.RowCount, Is.EqualTo(1));

            Load(",Jan,Feb");

            Assert.That(_ok, Is.False);
            Assert.That(_parser.RowCount, Is.EqualTo(0));
            Assert.That(_parser.ColumnCount, Is.EqualTo(0));
        }

        [TestCase("<html>error page</html>")]
        [TestCase(",Jan,Feb")]
        [TestCase("Item\nBao")]
        [TestCase(",Jan\nBao,x")]
        public void EveryRejectionClearsRawText(string payload)
        {
            Load(payload);

            Assert.That(_ok, Is.False);
            Assert.That(_parser.RawText, Is.Null);
        }

        [TestCase("<html>error page</html>")]
        [TestCase(",Jan,Feb")]
        [TestCase("Item\nBao")]
        [TestCase(",Jan\nBao,x")]
        public void EveryRejectionLeavesTheSourceUnloaded(string payload)
        {
            Load(payload);

            Assert.That(_ok, Is.False);
            Assert.That(_parser.IsLoaded, Is.False);
            Assert.That(_parser.RowCount, Is.EqualTo(0));
            Assert.That(_parser.ColumnCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectionAfterASuccessfulLoadAlsoUnloads()
        {
            Load(",Jan,Feb\nBao,10,20");
            Assert.That(_parser.IsLoaded, Is.True);

            Load("<html>error page</html>");

            Assert.That(_parser.IsLoaded, Is.False);
            Assert.That(_parser.RawText, Is.Null);
        }

        [Test]
        public void SuccessfulLoadReportsItselfAsLoaded()
        {
            Load(",Jan\nBao,10");

            Assert.That(_ok, Is.True);
            Assert.That(_parser.IsLoaded, Is.True);
        }

        [Test]
        public void SuccessfulReloadReplacesThePreviousGrid()
        {
            Load(",Jan,Feb\nBao,10,20");
            Load(",Mar\nLamb,99");

            Assert.That(_ok, Is.True);
            Assert.That(_parser.RowCount, Is.EqualTo(1));
            Assert.That(_parser.ColumnCount, Is.EqualTo(1));
            Assert.That(_parser.RowTitles, Is.EqualTo(new[] { "Lamb" }));
            Assert.That(_parser.ColumnTitles, Is.EqualTo(new[] { "Mar" }));
            Assert.That(_parser.GetValue(0, 0), Is.EqualTo(99f));
        }

        [Test]
        public void KeepsRawTextOnSuccess()
        {
            const string csv = ",Jan\nBao,10";
            Load(csv);

            Assert.That(_parser.RawText, Is.EqualTo(csv));
        }

        [Test]
        public void RaggedRowsPadToTheHeaderWidth()
        {
            Load(",Jan,Feb,Mar\nBao,1\nLamb,4,5,6");

            Assert.That(_parser.ColumnCount, Is.EqualTo(3));
            Assert.That(_parser.HasValue(0, 2), Is.False);
            Assert.That(_parser.HasValue(1, 2), Is.True);
            Assert.That(_parser.GetValue(1, 2), Is.EqualTo(6f));
        }

        [Test]
        public void ExtraFieldsBeyondTheHeaderAreIgnored()
        {
            Load(",Jan\nBao,1,999");

            Assert.That(_parser.ColumnCount, Is.EqualTo(1));
            Assert.That(_parser.GetValue(0, 0), Is.EqualTo(1f));
        }
    }
}
