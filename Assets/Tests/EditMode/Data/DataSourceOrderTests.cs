using System.Collections.Generic;
using NUnit.Framework;
using Study.Tests.EditMode.Support;

namespace Study.Tests.EditMode.Data
{
    public class DataSourceOrderTests
    {
        private GridFixture _fixture;
        private Parser _data;
        private int _orderChanged;

        [SetUp]
        public void SetUp()
        {
            _fixture = TestGrid.Load();
            _data = _fixture.Source;
            _orderChanged = 0;
            _data.OnOrderChanged += Count;
        }

        [TearDown]
        public void TearDown()
        {
            if (_data != null) _data.OnOrderChanged -= Count;
            _fixture?.Dispose();
        }

        private void Count() => _orderChanged++;

        [Test]
        public void OrdersStartAsIdentity()
        {
            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_data.ColumnOrder, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Original));
        }

        [Test]
        public void SetRowOrderAcceptsAPermutation()
        {
            Assert.That(_data.SetRowOrder(new[] { 2, 0, 1 }, DataSource.SortMode.Manual), Is.True);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 2, 0, 1 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Manual));
            Assert.That(_orderChanged, Is.EqualTo(1));
        }

        [Test]
        public void SetColumnOrderAcceptsAPermutation()
        {
            Assert.That(_data.SetColumnOrder(new[] { 3, 2, 1, 0 }, DataSource.SortMode.Manual), Is.True);

            Assert.That(_data.ColumnOrder, Is.EqualTo(new[] { 3, 2, 1, 0 }));
        }

        [TestCase(new[] { 0, 1 })]
        [TestCase(new[] { 0, 1, 2, 3 })]
        [TestCase(new[] { 0, 0, 1 })]
        [TestCase(new[] { 0, 1, 3 })]
        [TestCase(new[] { -1, 0, 1 })]
        public void SetRowOrderRejectsAnythingThatIsNotAPermutation(int[] order)
        {
            Assert.That(_data.SetRowOrder(order, DataSource.SortMode.Manual), Is.False);
        }

        [Test]
        public void SetRowOrderRejectsNull()
        {
            Assert.That(_data.SetRowOrder(null, DataSource.SortMode.Manual), Is.False);
        }

        [Test]
        public void ARejectedOrderLeavesThePreviousOneUntouched()
        {
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);
            _orderChanged = 0;

            _data.SetRowOrder(new[] { 0, 0, 0 }, DataSource.SortMode.Original);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 2, 1, 0 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Manual));
            Assert.That(_orderChanged, Is.EqualTo(0));
        }

        [Test]
        public void MoveRowShiftsForward()
        {
            _data.MoveRow(0, 2);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 1, 2, 0 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Manual));
        }

        [Test]
        public void MoveRowShiftsBackward()
        {
            _data.MoveRow(2, 0);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void MoveColumnShiftsWithoutLosingEntries()
        {
            _data.MoveColumn(1, 3);

            Assert.That(_data.ColumnOrder, Is.EqualTo(new[] { 0, 2, 3, 1 }));
            Assert.That(_data.ColumnOrder, Has.Count.EqualTo(TestGrid.Cols));
        }

        [Test]
        public void MoveToTheSamePositionDoesNothing()
        {
            _data.MoveRow(1, 1);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Original));
            Assert.That(_orderChanged, Is.EqualTo(0));
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(99)]
        public void MoveFromOutsideTheRangeDoesNothing(int from)
        {
            _data.MoveRow(from, 0);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_orderChanged, Is.EqualTo(0));
        }

        [Test]
        public void MoveTargetIsClampedIntoRange()
        {
            _data.MoveRow(0, 99);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 1, 2, 0 }));
        }

        [Test]
        public void MoveTargetIsClampedAtZero()
        {
            _data.MoveRow(2, -99);

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void MoveDoesNotSelfClearWhenTheOrderIsPassedToItself()
        {
            _data.MoveColumn(0, 3);
            _data.MoveColumn(0, 3);

            Assert.That(_data.ColumnOrder, Has.Count.EqualTo(TestGrid.Cols));
            Assert.That(_data.ColumnOrder, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void ResetOrderRestoresIdentityAndOriginalMode()
        {
            _data.MoveRow(0, 2);
            _data.MoveColumn(0, 3);

            _data.ResetOrder();

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_data.ColumnOrder, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Original));
            Assert.That(_data.ColumnSortMode, Is.EqualTo(DataSource.SortMode.Original));
        }

        [Test]
        public void TitleAtFollowsTheCurrentOrder()
        {
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);

            Assert.That(_data.TitleAt(false, 0), Is.EqualTo("Gamma"));
            Assert.That(_data.TitleAt(false, 2), Is.EqualTo("Alpha"));
        }

        [Test]
        public void ColumnTitleAtFollowsTheCurrentOrder()
        {
            _data.SetColumnOrder(new[] { 3, 0, 1, 2 }, DataSource.SortMode.Manual);

            Assert.That(_data.TitleAt(true, 0), Is.EqualTo("Apr"));
            Assert.That(_data.TitleAt(true, 1), Is.EqualTo("Jan"));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void TitleAtOutsideTheRangeIsNull(int visIndex)
        {
            Assert.That(_data.TitleAt(false, visIndex), Is.Null);
        }

        [Test]
        public void VisIndexOfInvertsTheOrder()
        {
            _data.SetRowOrder(new[] { 2, 0, 1 }, DataSource.SortMode.Manual);

            Assert.That(_data.VisIndexOf(false, 2), Is.EqualTo(0));
            Assert.That(_data.VisIndexOf(false, 0), Is.EqualTo(1));
            Assert.That(_data.VisIndexOf(false, 1), Is.EqualTo(2));
        }

        [Test]
        public void VisIndexOfAnUnknownDataIndexIsMinusOne()
        {
            Assert.That(_data.VisIndexOf(false, 99), Is.EqualTo(-1));
            Assert.That(_data.VisIndexOf(false, -1), Is.EqualTo(-1));
        }

        [Test]
        public void TitleAtAndVisIndexOfAgreeForEveryRow()
        {
            _data.SetRowOrder(new[] { 1, 2, 0 }, DataSource.SortMode.Manual);

            for (int dataIndex = 0; dataIndex < TestGrid.Rows; dataIndex++)
            {
                int vis = _data.VisIndexOf(false, dataIndex);
                Assert.That(_data.TitleAt(false, vis), Is.EqualTo(TestGrid.RowTitles[dataIndex]));
            }
        }

        [Test]
        public void ReorderingDoesNotDisturbTheUnderlyingValues()
        {
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);
            _data.SetColumnOrder(new[] { 3, 2, 1, 0 }, DataSource.SortMode.Manual);

            for (int r = 0; r < TestGrid.Rows; r++)
                for (int c = 0; c < TestGrid.Cols; c++)
                    Assert.That(_data.GetValue(r, c), Is.EqualTo(TestGrid.ValueAt(r, c)));
        }

        [Test]
        public void OrderChangedFiresOncePerAcceptedChange()
        {
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);
            _data.MoveRow(0, 1);

            Assert.That(_orderChanged, Is.EqualTo(2));
        }

        [Test]
        public void ReloadingResetsTheOrderToIdentity()
        {
            _data.SetRowOrder(new[] { 2, 1, 0 }, DataSource.SortMode.Manual);

            _data.LoadFromCsvText(TestGrid.Simple());

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_data.RowSortMode, Is.EqualTo(DataSource.SortMode.Original));
        }

        [Test]
        public void AnOrderSurvivesBeingSetToItself()
        {
            var order = new List<int>(_data.ColumnOrder);

            Assert.That(_data.SetColumnOrder(order, DataSource.SortMode.Manual), Is.True);
            Assert.That(_data.ColumnOrder, Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void SettingAnOrderCopiesItSoLaterEditsDoNotLeak()
        {
            var order = new List<int> { 2, 1, 0 };
            _data.SetRowOrder(order, DataSource.SortMode.Manual);

            order[0] = 0;

            Assert.That(_data.RowOrder, Is.EqualTo(new[] { 2, 1, 0 }));
        }
    }
}
