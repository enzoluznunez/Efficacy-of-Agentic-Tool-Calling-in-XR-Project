using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Study.Tests.PlayMode.Support;
using UnityEngine;
using UnityEngine.TestTools;

namespace Study.Tests.PlayMode.Sheets
{
    public class CellColorsSnapshotTests
    {
        private SceneFixture _scene;
        private Parser _data;
        private ManageSheets _sheets;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scene = new SceneFixture();
            _data = _scene.SpawnData(TestGrid.Simple());
            _sheets = _scene.Spawn<ManageSheets>("SheetsUnderTest");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _scene?.Dispose();
            yield return null;
        }

        private List<Dictionary<string, object>> Snapshot()
        {
            var rows = new List<Dictionary<string, object>>();
            foreach (object entry in _sheets.CellColorsSnapshot(_data))
                rows.Add((Dictionary<string, object>)entry);
            return rows;
        }

        private static Dictionary<string, object> Find(List<Dictionary<string, object>> rows,
            string row, string col)
        {
            for (int i = 0; i < rows.Count; i++)
                if ((string)rows[i]["row"] == row && (string)rows[i]["col"] == col) return rows[i];
            return null;
        }

        [UnityTest]
        public IEnumerator AnUncolouredSheetSnapshotsEmpty()
        {
            yield return null;

            Assert.That(Snapshot(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AColouredCellAppearsWithItsTitles()
        {
            _sheets.AddCellColor(1, 2, Color.blue);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["row"], Is.EqualTo("Beta"));
            Assert.That(rows[0]["col"], Is.EqualTo("Mar"));
        }

        [UnityTest]
        public IEnumerator ColoursAreWrittenAsSixDigitHex()
        {
            _sheets.AddCellColor(0, 0, Color.blue);
            yield return null;

            Assert.That(Snapshot()[0]["hex"], Is.EqualTo("#0000FF"));
        }

        [UnityTest]
        public IEnumerator RedAndWhiteRoundTripThroughHex()
        {
            _sheets.AddCellColor(0, 0, Color.red);
            _sheets.AddCellColor(0, 1, Color.white);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(Find(rows, "Alpha", "Jan")["hex"], Is.EqualTo("#FF0000"));
            Assert.That(Find(rows, "Alpha", "Feb")["hex"], Is.EqualTo("#FFFFFF"));
        }

        [UnityTest]
        public IEnumerator EveryColouredCellIsListedExactlyOnce()
        {
            _sheets.AddCellColor(0, 0, Color.red);
            _sheets.AddCellColor(1, 1, Color.blue);
            _sheets.AddCellColor(2, 3, Color.green);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows, Has.Count.EqualTo(3));
            Assert.That(Find(rows, "Alpha", "Jan"), Is.Not.Null);
            Assert.That(Find(rows, "Beta", "Feb"), Is.Not.Null);
            Assert.That(Find(rows, "Gamma", "Apr"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RecolouringACellReplacesRatherThanDuplicates()
        {
            _sheets.AddCellColor(0, 0, Color.red);
            _sheets.AddCellColor(0, 0, Color.blue);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["hex"], Is.EqualTo("#0000FF"));
        }

        [UnityTest]
        public IEnumerator RowAndColumnAreNotTransposed()
        {
            _sheets.AddCellColor(0, 3, Color.red);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows[0]["row"], Is.EqualTo("Alpha"));
            Assert.That(rows[0]["col"], Is.EqualTo("Apr"));
        }

        [UnityTest]
        public IEnumerator TheCellKeyRoundTripsAcrossTheWholeGrid()
        {
            for (int r = 0; r < TestGrid.Rows; r++)
                for (int c = 0; c < TestGrid.Cols; c++)
                    _sheets.AddCellColor(r, c, Color.blue);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows, Has.Count.EqualTo(TestGrid.Rows * TestGrid.Cols));
            for (int r = 0; r < TestGrid.Rows; r++)
                for (int c = 0; c < TestGrid.Cols; c++)
                    Assert.That(Find(rows, TestGrid.RowTitles[r], TestGrid.ColTitles[c]), Is.Not.Null,
                        $"missing cell {r},{c}");
        }

        [UnityTest]
        public IEnumerator LargeIndicesSurviveTheKeyPacking()
        {
            _sheets.AddCellColor(70000, 90000, Color.blue);
            yield return null;

            Assert.That(_sheets.TryGetCellColor(70000, 90000, out Color found), Is.True);
            Assert.That(found, Is.EqualTo(Color.blue));
        }

        [UnityTest]
        public IEnumerator IndicesWithoutATitleFallBackToAOneBasedLabel()
        {
            _sheets.AddCellColor(50, 60, Color.blue);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows[0]["row"], Is.EqualTo("row 51"));
            Assert.That(rows[0]["col"], Is.EqualTo("column 61"));
        }

        [UnityTest]
        public IEnumerator ClearingACellRemovesItFromTheSnapshot()
        {
            _sheets.AddCellColor(0, 0, Color.red);
            _sheets.AddCellColor(1, 1, Color.blue);

            _sheets.ClearCellColor(0, 0);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["row"], Is.EqualTo("Beta"));
        }

        [UnityTest]
        public IEnumerator ResetColorsEmptiesTheSnapshot()
        {
            _sheets.AddCellColor(0, 0, Color.red);
            _sheets.AddCellColor(1, 1, Color.blue);

            _sheets.ResetColors();
            yield return null;

            Assert.That(Snapshot(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator TryGetCellColorAnswersForBothPresentAndAbsentCells()
        {
            _sheets.AddCellColor(2, 2, Color.green);
            yield return null;

            Assert.That(_sheets.TryGetCellColor(2, 2, out Color found), Is.True);
            Assert.That(found, Is.EqualTo(Color.green));
            Assert.That(_sheets.TryGetCellColor(0, 0, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator TopColorOfDefaultsToWhiteForAnUncolouredCell()
        {
            yield return null;

            Assert.That(_sheets.TopColorOf(0, 0), Is.EqualTo(Color.white));
        }

        [UnityTest]
        public IEnumerator SnapshotUsesDataIndicesSoReorderingDoesNotMoveAColour()
        {
            _sheets.AddCellColor(0, 0, Color.red);
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);
            yield return null;

            List<Dictionary<string, object>> rows = Snapshot();

            Assert.That(rows[0]["row"], Is.EqualTo("Alpha"));
            Assert.That(rows[0]["col"], Is.EqualTo("Jan"));
        }

        [UnityTest]
        public IEnumerator SnapshotAgainstNullDataStillNamesTheCells()
        {
            _sheets.AddCellColor(0, 1, Color.blue);
            yield return null;

            var rows = new List<Dictionary<string, object>>();
            foreach (object entry in _sheets.CellColorsSnapshot(null))
                rows.Add((Dictionary<string, object>)entry);

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["row"], Is.EqualTo("row 1"));
            Assert.That(rows[0]["col"], Is.EqualTo("column 2"));
        }
    }
}
