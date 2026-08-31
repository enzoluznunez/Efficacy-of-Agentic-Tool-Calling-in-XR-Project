using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Study.Tests.EditMode.Sheet
{
    public class EditListTests
    {
        private EditList _edits;

        [SetUp]
        public void SetUp() => _edits = new EditList();

        private void PushMove(int sheetId) =>
            _edits.PushMove(new MoveRecord { sheetId = sheetId }, EditKind.Move);

        private void PushColor(string name) =>
            _edits.PushColorStroke(name, "#0000FF", new List<ColorCell>());

        [Test]
        public void StampGroupCollapsesAnInterruptedTurnIntoOneStep()
        {
            PushMove(1);
            int mark = _edits.Count;
            PushMove(2);
            PushMove(3);
            PushMove(4);

            Assert.That(_edits.UndoStepCount(), Is.EqualTo(4));

            int stamped = _edits.StampGroup(mark);

            Assert.That(stamped, Is.EqualTo(3));
            Assert.That(_edits.TopGroupSize(), Is.EqualTo(3));
            Assert.That(_edits.UndoStepCount(), Is.EqualTo(2));
        }

        [Test]
        public void StampGroupLeavesEarlierEditsAlone()
        {
            PushMove(1);
            PushMove(2);
            int mark = _edits.Count;
            PushMove(3);

            _edits.StampGroup(mark);

            Assert.That(_edits[0].group, Is.EqualTo(0));
            Assert.That(_edits[1].group, Is.EqualTo(0));
            Assert.That(_edits[2].group, Is.Not.EqualTo(0));
        }

        [Test]
        public void StampGroupOnAnEmptySpanDoesNothing()
        {
            PushMove(1);

            Assert.That(_edits.StampGroup(_edits.Count), Is.EqualTo(0));
            Assert.That(_edits.TopGroupSize(), Is.EqualTo(1));
        }

        [Test]
        public void DropAtRemovesOneEdit()
        {
            PushMove(1);
            PushMove(2);
            PushMove(3);

            _edits.DropAt(1);

            Assert.That(_edits.Count, Is.EqualTo(2));
            Assert.That(_edits[0].move.sheetId, Is.EqualTo(1));
            Assert.That(_edits[1].move.sheetId, Is.EqualTo(3));
        }

        [Test]
        public void DropAtIgnoresAnOutOfRangeIndex()
        {
            PushMove(1);

            _edits.DropAt(-1);
            _edits.DropAt(5);

            Assert.That(_edits.Count, Is.EqualTo(1));
        }

        [Test]
        public void EmptyListHasNoUndoSteps()
        {
            Assert.That(_edits.UndoStepCount(), Is.EqualTo(0));
            Assert.That(_edits.TopGroupSize(), Is.EqualTo(0));
            Assert.That(_edits.Peek(), Is.Null);
            Assert.That(_edits.Pop(), Is.Null);
        }

        [Test]
        public void UngroupedEditsCountOneStepEach()
        {
            PushMove(1);
            PushMove(2);
            PushMove(3);

            Assert.That(_edits.Count, Is.EqualTo(3));
            Assert.That(_edits.UndoStepCount(), Is.EqualTo(3));
        }

        [Test]
        public void OneOpenGroupCollapsesToASingleStep()
        {
            _edits.OpenGroup();
            PushMove(1);
            PushMove(2);
            PushMove(3);
            _edits.CloseGroup();

            Assert.That(_edits.Count, Is.EqualTo(3));
            Assert.That(_edits.UndoStepCount(), Is.EqualTo(1));
        }

        [Test]
        public void GroupedAndUngroupedEditsMix()
        {
            PushMove(1);

            _edits.OpenGroup();
            PushMove(2);
            PushMove(3);
            _edits.CloseGroup();

            PushMove(4);

            Assert.That(_edits.Count, Is.EqualTo(4));
            Assert.That(_edits.UndoStepCount(), Is.EqualTo(3));
        }

        [Test]
        public void TwoAdjacentGroupsStayTwoSteps()
        {
            _edits.OpenGroup();
            PushMove(1);
            PushMove(2);
            _edits.CloseGroup();

            _edits.OpenGroup();
            PushMove(3);
            PushMove(4);
            _edits.CloseGroup();

            Assert.That(_edits.UndoStepCount(), Is.EqualTo(2));
        }

        [Test]
        public void GroupIdsIncrementSoGroupsNeverMerge()
        {
            int first = _edits.OpenGroup();
            _edits.CloseGroup();
            int second = _edits.OpenGroup();
            _edits.CloseGroup();

            Assert.That(second, Is.GreaterThan(first));
        }

        [Test]
        public void EditsPushedAfterCloseGroupAreUngrouped()
        {
            _edits.OpenGroup();
            PushMove(1);
            _edits.CloseGroup();
            PushMove(2);

            Assert.That(_edits[0].group, Is.Not.EqualTo(0));
            Assert.That(_edits[1].group, Is.EqualTo(0));
        }

        [Test]
        public void AnUnclosedGroupKeepsSwallowingEdits()
        {
            _edits.OpenGroup();
            PushMove(1);
            PushMove(2);
            PushMove(3);

            Assert.That(_edits.UndoStepCount(), Is.EqualTo(1));
        }

        [Test]
        public void TopGroupSizeIsOneForAnUngroupedTop()
        {
            _edits.OpenGroup();
            PushMove(1);
            PushMove(2);
            _edits.CloseGroup();
            PushMove(3);

            Assert.That(_edits.TopGroupSize(), Is.EqualTo(1));
        }

        [Test]
        public void TopGroupSizeCountsTheWholeTopGroup()
        {
            PushMove(1);
            _edits.OpenGroup();
            PushMove(2);
            PushMove(3);
            PushMove(4);
            _edits.CloseGroup();

            Assert.That(_edits.TopGroupSize(), Is.EqualTo(3));
        }

        [Test]
        public void PopReturnsAndRemovesTheNewestEdit()
        {
            PushMove(1);
            PushMove(2);

            Edit popped = _edits.Pop();

            Assert.That(popped.move.sheetId, Is.EqualTo(2));
            Assert.That(_edits.Count, Is.EqualTo(1));
        }

        [Test]
        public void PeekDoesNotRemove()
        {
            PushMove(1);

            Assert.That(_edits.Peek().move.sheetId, Is.EqualTo(1));
            Assert.That(_edits.Count, Is.EqualTo(1));
        }

        [Test]
        public void PushSortCopiesTheOrderItWasGiven()
        {
            var order = new List<int> { 0, 1, 2 };
            _edits.PushSort(true, order, DataSource.SortMode.Original, 0, 2);

            order[0] = 99;
            order.Add(5);

            Assert.That(_edits.Peek().reorderPreOrder, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void PushReorderCopiesTheOrderItWasGiven()
        {
            var order = new List<int> { 2, 1, 0 };
            _edits.PushReorder(false, order, DataSource.SortMode.Manual, 3);

            order.Clear();

            Assert.That(_edits.Peek().reorderPreOrder, Is.EqualTo(new[] { 2, 1, 0 }));
        }

        [Test]
        public void PushSortWithNullOrderStoresAnEmptyListNotNull()
        {
            _edits.PushSort(true, null, DataSource.SortMode.Original, 0, 1);

            Assert.That(_edits.Peek().reorderPreOrder, Is.Not.Null);
            Assert.That(_edits.Peek().reorderPreOrder, Is.Empty);
        }

        [Test]
        public void PushReorderWithNullOrderStoresAnEmptyListNotNull()
        {
            _edits.PushReorder(true, null, DataSource.SortMode.Original, 2);

            Assert.That(_edits.Peek().reorderPreOrder, Is.Not.Null);
            Assert.That(_edits.Peek().reorderPreOrder, Is.Empty);
        }

        [Test]
        public void PushSortRecordsASingleLine()
        {
            _edits.PushSort(true, new List<int> { 0, 1 }, DataSource.SortMode.Original, 0, 1);

            Edit e = _edits.Peek();

            Assert.That(e.kind, Is.EqualTo(EditKind.Sort));
            Assert.That(e.reorderLines, Is.EqualTo(1));
            Assert.That(e.reorderFrom, Is.EqualTo(0));
            Assert.That(e.reorderTarget, Is.EqualTo(1));
        }

        [Test]
        public void PushReorderMarksItselfAsBulkWithNoFromOrTarget()
        {
            _edits.PushReorder(true, new List<int> { 0, 1 }, DataSource.SortMode.Original, 4);

            Edit e = _edits.Peek();

            Assert.That(e.reorderLines, Is.EqualTo(4));
            Assert.That(e.reorderFrom, Is.EqualTo(-1));
            Assert.That(e.reorderTarget, Is.EqualTo(-1));
        }

        [Test]
        public void PushSliceTakesItsSheetIdFromTheRecord()
        {
            _edits.PushSlice(new SliceRecord { aId = 7, bId = 8 });

            Assert.That(_edits.Peek().kind, Is.EqualTo(EditKind.Slice));
            Assert.That(_edits.Peek().sheetId, Is.EqualTo(7));
        }

        [Test]
        public void PushColorStrokeKeepsNameAndHex()
        {
            PushColor("blue");

            Edit e = _edits.Peek();

            Assert.That(e.kind, Is.EqualTo(EditKind.Color));
            Assert.That(e.colorName, Is.EqualTo("blue"));
            Assert.That(e.colorHex, Is.EqualTo("#0000FF"));
        }

        [Test]
        public void PushProjectionKeepsTheKindItWasGiven()
        {
            _edits.PushProjection(new ProjectionRecord { isColumn = true, lift = 0.5f }, EditKind.Profile);

            Assert.That(_edits.Peek().kind, Is.EqualTo(EditKind.Profile));
            Assert.That(_edits.Peek().projection.isColumn, Is.True);
        }

        [Test]
        public void DropKindRemovesOnlyThatKind()
        {
            PushMove(1);
            PushColor("blue");
            PushMove(2);
            PushColor("red");

            _edits.DropKind(EditKind.Color);

            Assert.That(_edits.Count, Is.EqualTo(2));
            foreach (Edit e in _edits) Assert.That(e.kind, Is.EqualTo(EditKind.Move));
        }

        [Test]
        public void DropKindLeavesUndoStepsCoherent()
        {
            _edits.OpenGroup();
            PushMove(1);
            PushMove(2);
            _edits.CloseGroup();
            PushColor("blue");

            Assert.That(_edits.UndoStepCount(), Is.EqualTo(2));

            _edits.DropKind(EditKind.Color);

            Assert.That(_edits.UndoStepCount(), Is.EqualTo(1));
        }

        [Test]
        public void DropKindOnAnAbsentKindChangesNothing()
        {
            PushMove(1);
            PushMove(2);

            _edits.DropKind(EditKind.Slice);

            Assert.That(_edits.Count, Is.EqualTo(2));
        }

        [Test]
        public void PoppingAGroupMemberShrinksTheTopGroup()
        {
            _edits.OpenGroup();
            PushMove(1);
            PushMove(2);
            PushMove(3);
            _edits.CloseGroup();

            _edits.Pop();

            Assert.That(_edits.TopGroupSize(), Is.EqualTo(2));
            Assert.That(_edits.UndoStepCount(), Is.EqualTo(1));
        }

        [TestCase(EditKind.Slice, "slice")]
        [TestCase(EditKind.Move, "move")]
        [TestCase(EditKind.Rotate, "rotate")]
        [TestCase(EditKind.Scale, "scale")]
        [TestCase(EditKind.Color, "color")]
        [TestCase(EditKind.Sort, "sort")]
        [TestCase(EditKind.Detail, "detail")]
        [TestCase(EditKind.Profile, "profile")]
        public void KindNameCoversEveryKind(EditKind kind, string expected)
        {
            Assert.That(Edit.KindName(kind), Is.EqualTo(expected));
        }

        [Test]
        public void MoveRecordSurvivesTheRoundTrip()
        {
            var record = new MoveRecord {
                sheetId = 3,
                prePos = new Vector3(1f, 2f, 3f),
                postPos = new Vector3(4f, 5f, 6f),
                distance = 2.5f
            };
            _edits.PushMove(record, EditKind.Scale);

            Edit e = _edits.Peek();

            Assert.That(e.kind, Is.EqualTo(EditKind.Scale));
            Assert.That(e.move.prePos, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(e.move.distance, Is.EqualTo(2.5f));
        }
    }
}
