using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Study.Tests.PlayMode.Support;
using UnityEngine.TestTools;

namespace Study.Tests.PlayMode.Gemini
{
    public class LineResolutionTests
    {
        private SceneFixture _scene;
        private Parser _data;
        private Dictionary<string, object> _result;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scene = new SceneFixture();
            _data = _scene.SpawnData(TestGrid.Simple());
            _result = new Dictionary<string, object>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _scene?.Dispose();
            yield return null;
        }

        private string Error() => _result.TryGetValue("error", out object e) ? e as string : null;

        [UnityTest]
        public IEnumerator TheFixtureIsVisibleThroughScene()
        {
            yield return null;

            Assert.That(Scene.Data, Is.SameAs(_data));
            Assert.That(_data.RowCount, Is.EqualTo(TestGrid.Rows));
        }

        [UnityTest]
        public IEnumerator ResolvesARowByOneBasedNumber()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("1", false, 0, TestGrid.Rows - 1, _result, out int vis), Is.True);
            Assert.That(vis, Is.EqualTo(0));

            Assert.That(ToolAccess.ResolveLine("3", false, 0, TestGrid.Rows - 1, _result, out vis), Is.True);
            Assert.That(vis, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ResolvesAColumnByName()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("Mar", true, 0, TestGrid.Cols - 1, _result, out int vis), Is.True);
            Assert.That(vis, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NameMatchingIgnoresCaseAndSurroundingSpace()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("  alpha  ", false, 0, TestGrid.Rows - 1, _result, out int vis), Is.True);
            Assert.That(vis, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ResolutionFollowsTheCurrentOrderNotTheUnderlyingData()
        {
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);
            yield return null;

            Assert.That(ToolAccess.ResolveLine("Alpha", false, 0, TestGrid.Rows - 1, _result, out int byName), Is.True);
            Assert.That(byName, Is.EqualTo(2));

            Assert.That(ToolAccess.ResolveLine("1", false, 0, TestGrid.Rows - 1, _result, out int byNumber), Is.True);
            Assert.That(byNumber, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator APrefixOfThreeOrMoreCharactersResolves()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("Gam", false, 0, TestGrid.Rows - 1, _result, out int vis), Is.True);
            Assert.That(vis, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator APrefixShorterThanThreeCharactersDoesNotResolve()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("Ga", false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator NumberZeroIsRefusedWithTheLineCount()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("0", false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("no row 0"));
            Assert.That(Error(), Does.Contain("3"));
        }

        [UnityTest]
        public IEnumerator NumberPastTheEndIsRefused()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("9", true, 0, TestGrid.Cols - 1, _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("no column 9"));
        }

        [UnityTest]
        public IEnumerator AnEmptyTokenAsksForOne()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("", false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("Provide a row"));
        }

        [UnityTest]
        public IEnumerator AnUnknownNameIsRefusedAndTheNamesAreOffered()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("Zebra", false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("No row called 'Zebra'"));
            Assert.That(_result.ContainsKey("rows"), Is.True);
        }

        [UnityTest]
        public IEnumerator ANameOnTheOtherAxisSaysSo()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("Jan", false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("there is a column called 'Jan'"));
            Assert.That(Error(), Does.Contain("pass it as 'column'"));
        }

        [UnityTest]
        public IEnumerator TitleOnAxisFindsTitlesOnTheRightAxisOnly()
        {
            yield return null;

            Assert.That(ToolAccess.TitleOnAxis("Jan", true), Is.True);
            Assert.That(ToolAccess.TitleOnAxis("Jan", false), Is.False);
            Assert.That(ToolAccess.TitleOnAxis("Alpha", false), Is.True);
            Assert.That(ToolAccess.TitleOnAxis("Nothing", true), Is.False);
        }

        [UnityTest]
        public IEnumerator InferAxisPicksTheAxisThatHoldsTheName()
        {
            yield return null;

            Assert.That(ToolAccess.InferAxis("Jan", out bool columns), Is.True);
            Assert.That(columns, Is.True);

            Assert.That(ToolAccess.InferAxis("Alpha", out columns), Is.True);
            Assert.That(columns, Is.False);
        }

        [UnityTest]
        public IEnumerator InferAxisRefusesANumber()
        {
            yield return null;

            Assert.That(ToolAccess.InferAxis("2", out _), Is.False);
        }

        [UnityTest]
        public IEnumerator InferAxisRefusesEmptyAndUnknownNames()
        {
            yield return null;

            Assert.That(ToolAccess.InferAxis("", out _), Is.False);
            Assert.That(ToolAccess.InferAxis(null, out _), Is.False);
            Assert.That(ToolAccess.InferAxis("Nothing", out _), Is.False);
        }

        [UnityTest]
        public IEnumerator InferAxisRefusesANameThatSitsOnBothAxes()
        {
            _scene.Dispose();
            _scene = new SceneFixture();
            _data = _scene.SpawnData("Item\\Month,Same,Feb\nSame,1,2\nBeta,3,4\n");
            yield return null;

            Assert.That(ToolAccess.TitleOnAxis("Same", true), Is.True);
            Assert.That(ToolAccess.TitleOnAxis("Same", false), Is.True);
            Assert.That(ToolAccess.InferAxis("Same", out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ResolveLinesTakesSeveralTokens()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLines(new[] { "Alpha", "3" }, false, 0, TestGrid.Rows - 1,
                _result, out List<int> vis), Is.True);
            Assert.That(vis, Is.EqualTo(new[] { 0, 2 }));
        }

        [UnityTest]
        public IEnumerator ResolveLinesRefusesADuplicate()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLines(new[] { "Alpha", "1" }, false, 0, TestGrid.Rows - 1,
                _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("more than once"));
        }

        [UnityTest]
        public IEnumerator ResolveLinesRefusesMoreTokensThanLines()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLines(new[] { "1", "2", "3", "4" }, false, 0, TestGrid.Rows - 1,
                _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("only 3 rows"));
        }

        [UnityTest]
        public IEnumerator ResolveLinesRefusesAnEmptyList()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLines(new string[0], false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
            Assert.That(ToolAccess.ResolveLines(null, false, 0, TestGrid.Rows - 1, _result, out _), Is.False);
            Assert.That(Error(), Does.Contain("at least one row"));
        }

        [UnityTest]
        public IEnumerator ResolveLinesStopsAtTheFirstBadToken()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLines(new[] { "Alpha", "Zebra" }, false, 0, TestGrid.Rows - 1,
                _result, out List<int> vis), Is.False);
            Assert.That(vis, Is.Null);
            Assert.That(Error(), Does.Contain("Zebra"));
        }

        [UnityTest]
        public IEnumerator ResolutionIsBoundedByTheGivenWindow()
        {
            yield return null;

            Assert.That(ToolAccess.ResolveLine("1", false, 1, 2, _result, out int vis), Is.True);
            Assert.That(vis, Is.EqualTo(1));

            Assert.That(ToolAccess.ResolveLine("3", false, 1, 2, _result, out _), Is.False);
        }
    }
}
