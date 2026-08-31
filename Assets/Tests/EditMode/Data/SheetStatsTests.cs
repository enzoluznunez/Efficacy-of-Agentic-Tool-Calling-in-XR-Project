using NUnit.Framework;
using Study.Tests.EditMode.Support;

namespace Study.Tests.EditMode.Data
{
    public class SheetStatsTests
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

        private SheetStats.Summary Whole() =>
            SheetStats.Over(_data, 0, TestGrid.Rows - 1, 0, TestGrid.Cols - 1);

        [Test]
        public void CountsEveryCellInTheRange()
        {
            SheetStats.Summary s = Whole();

            Assert.That(s.valid, Is.True);
            Assert.That(s.count, Is.EqualTo(12));
        }

        [Test]
        public void SumsAndAveragesTheRange()
        {
            SheetStats.Summary s = Whole();

            Assert.That(s.sum, Is.EqualTo(78d).Within(1e-9d));
            Assert.That(s.mean, Is.EqualTo(6.5d).Within(1e-9d));
        }

        [Test]
        public void TracksMinAndMax()
        {
            SheetStats.Summary s = Whole();

            Assert.That(s.min, Is.EqualTo(1d));
            Assert.That(s.max, Is.EqualTo(12d));
        }

        [Test]
        public void ReportsThePopulationStandardDeviation()
        {
            SheetStats.Summary s = Whole();

            Assert.That(s.stdDevPopulation, Is.EqualTo(3.452052529d).Within(1e-6d));
        }

        [Test]
        public void ReportsTheSampleStandardDeviation()
        {
            SheetStats.Summary s = Whole();

            Assert.That(s.stdDevSample, Is.EqualTo(3.605551276d).Within(1e-6d));
        }

        [Test]
        public void SampleDeviationIsTheLargerOfTheTwo()
        {
            SheetStats.Summary s = Whole();

            Assert.That(s.stdDevSample, Is.GreaterThan(s.stdDevPopulation));
        }

        [Test]
        public void TheTwoDeviationsDifferByBesselsCorrection()
        {
            SheetStats.Summary s = Whole();

            double expected = s.stdDevPopulation * System.Math.Sqrt(s.count / (double)(s.count - 1));

            Assert.That(s.stdDevSample, Is.EqualTo(expected).Within(1e-9d));
        }

        [Test]
        public void ASingleValueHasNoPopulationDeviation()
        {
            SheetStats.Summary s = SheetStats.Over(_data, 0, 0, 0, 0);

            Assert.That(s.count, Is.EqualTo(1));
            Assert.That(s.stdDevPopulation, Is.EqualTo(0d).Within(1e-9d));
            Assert.That(s.mean, Is.EqualTo(1d));
        }

        [Test]
        public void ASingleValueHasNoDefinedSampleDeviation()
        {
            SheetStats.Summary s = SheetStats.Over(_data, 0, 0, 0, 0);

            Assert.That(s.valid, Is.True);
            Assert.That(double.IsNaN(s.stdDevSample), Is.True);
        }

        [Test]
        public void TwoValuesHaveADefinedSampleDeviation()
        {
            using var pair = TestGrid.Load("Item\\Month,Jan,Feb\nAlpha,4,10\n");

            SheetStats.Summary s = SheetStats.Over(pair.Source, 0, 0, 0, 1);

            Assert.That(s.count, Is.EqualTo(2));
            Assert.That(s.stdDevPopulation, Is.EqualTo(3d).Within(1e-9d));
            Assert.That(s.stdDevSample, Is.EqualTo(4.242640687d).Within(1e-6d));
        }

        [Test]
        public void AnEmptyRangeLeavesBothDeviationsAtTheirDefault()
        {
            SheetStats.Summary s = SheetStats.Over(_data, 50, 60, 50, 60);

            Assert.That(s.valid, Is.False);
            Assert.That(s.stdDevPopulation, Is.EqualTo(0d));
            Assert.That(s.stdDevSample, Is.EqualTo(0d));
        }

        [Test]
        public void ASubRangeOnlyCoversItsOwnCells()
        {
            SheetStats.Summary s = SheetStats.Over(_data, 0, 0, 0, TestGrid.Cols - 1);

            Assert.That(s.count, Is.EqualTo(4));
            Assert.That(s.sum, Is.EqualTo(1d + 2d + 3d + 4d));
        }

        [Test]
        public void RangesAreReadThroughTheCurrentRowOrder()
        {
            SheetStats.Summary before = SheetStats.Over(_data, 0, 0, 0, TestGrid.Cols - 1);
            Assert.That(before.sum, Is.EqualTo(10d));

            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);

            SheetStats.Summary after = SheetStats.Over(_data, 0, 0, 0, TestGrid.Cols - 1);

            Assert.That(after.sum, Is.EqualTo(9d + 10d + 11d + 12d));
        }

        [Test]
        public void RangesAreReadThroughTheCurrentColumnOrder()
        {
            _data.SetColumnOrder(new[] { 3, 2, 1, 0 }, DataSource.SortMode.Manual);

            SheetStats.Summary s = SheetStats.Over(_data, 0, 0, 0, 0);

            Assert.That(s.sum, Is.EqualTo(4d));
        }

        [Test]
        public void ReorderingDoesNotChangeTheWholeGridTotal()
        {
            double before = Whole().sum;

            _data.SetRowOrder(new[] { 2, 0, 1 }, DataSource.SortMode.Manual);
            _data.SetColumnOrder(new[] { 1, 3, 0, 2 }, DataSource.SortMode.Manual);

            Assert.That(Whole().sum, Is.EqualTo(before));
        }

        [Test]
        public void CellsWithoutAValueAreSkipped()
        {
            using var holes = TestGrid.Load("Item\\Month,Jan,Feb\nAlpha,10,\nBeta,,20\n");

            SheetStats.Summary s = SheetStats.Over(holes.Source, 0, 1, 0, 1);

            Assert.That(s.count, Is.EqualTo(2));
            Assert.That(s.sum, Is.EqualTo(30d));
            Assert.That(s.min, Is.EqualTo(10d));
            Assert.That(s.max, Is.EqualTo(20d));
        }

        [Test]
        public void NullDataIsNotValid()
        {
            SheetStats.Summary s = SheetStats.Over(null, 0, 1, 0, 1);

            Assert.That(s.valid, Is.False);
            Assert.That(s.count, Is.EqualTo(0));
        }

        [Test]
        public void AnInvertedRangeYieldsNothing()
        {
            SheetStats.Summary s = SheetStats.Over(_data, 2, 0, 2, 0);

            Assert.That(s.valid, Is.False);
            Assert.That(s.count, Is.EqualTo(0));
        }

        [Test]
        public void OutOfRangeIndicesAreClampedNotThrown()
        {
            SheetStats.Summary s = SheetStats.Over(_data, -5, 99, -5, 99);

            Assert.That(s.count, Is.EqualTo(12));
            Assert.That(s.sum, Is.EqualTo(78d));
        }

        [Test]
        public void ARangeEntirelyOutsideTheGridIsNotValid()
        {
            SheetStats.Summary s = SheetStats.Over(_data, 50, 60, 50, 60);

            Assert.That(s.valid, Is.False);
        }

        [Test]
        public void NegativeValuesAreHandled()
        {
            using var negative = TestGrid.Load("Item\\Month,Jan,Feb\nAlpha,-10,10\nBeta,-20,20\n");

            SheetStats.Summary s = SheetStats.Over(negative.Source, 0, 1, 0, 1);

            Assert.That(s.sum, Is.EqualTo(0d).Within(1e-9d));
            Assert.That(s.min, Is.EqualTo(-20d));
            Assert.That(s.max, Is.EqualTo(20d));
            Assert.That(s.mean, Is.EqualTo(0d).Within(1e-9d));
        }

        [Test]
        public void IdenticalValuesHaveZeroDeviation()
        {
            using var flat = TestGrid.Load("Item\\Month,Jan,Feb\nAlpha,5,5\nBeta,5,5\n");

            SheetStats.Summary s = SheetStats.Over(flat.Source, 0, 1, 0, 1);

            Assert.That(s.stdDevPopulation, Is.EqualTo(0d).Within(1e-9d));
            Assert.That(s.stdDevSample, Is.EqualTo(0d).Within(1e-9d));
            Assert.That(s.mean, Is.EqualTo(5d));
        }

        [Test]
        public void AGridWithNoNumbersAtAllIsNotValid()
        {
            using var empty = TestGrid.Load("Item\\Month,Jan\nAlpha,x\n");

            SheetStats.Summary s = SheetStats.Over(empty.Source, 0, 0, 0, 0);

            Assert.That(s.valid, Is.False);
        }
    }
}
