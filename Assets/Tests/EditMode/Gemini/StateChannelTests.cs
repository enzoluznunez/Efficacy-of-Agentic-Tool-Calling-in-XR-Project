using NUnit.Framework;

namespace Study.Tests.EditMode.Gemini
{
    public class StateChannelTests
    {
        [SetUp]
        public void SetUp()
        {
            StateChannel.Suppressed = false;
            StateChannel.ClearPending();
            StateChannel.TakeAgentBatch();
            StateChannel.TryTakeBatch(out _);
        }

        [TearDown]
        public void TearDown()
        {
            StateChannel.Suppressed = false;
            StateChannel.ClearPending();
        }

        private static string Batch()
        {
            StateChannel.TryTakeBatch(out string text);
            return text;
        }

        [Test]
        public void NothingIsPendingOnAQuietChannel()
        {
            Assert.That(StateChannel.HasPending, Is.False);
            Assert.That(StateChannel.TryTakeBatch(out string text), Is.False);
            Assert.That(text, Is.Null);
        }

        [Test]
        public void RecordingAUserActionMakesABatchPending()
        {
            StateChannel.Record("Sort", "sorted the rows");

            Assert.That(StateChannel.HasPending, Is.True);
            Assert.That(Batch(), Does.Contain("sorted the rows"));
        }

        [Test]
        public void TheUserBatchIsLabelledAsATool()
        {
            StateChannel.Record("Sort", "sorted the rows");

            Assert.That(Batch(), Does.StartWith("[tool] User:"));
        }

        [Test]
        public void TakingABatchDrainsIt()
        {
            StateChannel.Record("Sort", "sorted the rows");
            Batch();

            Assert.That(StateChannel.HasPending, Is.False);
        }

        [Test]
        public void SeveralActionsJoinIntoOneBatch()
        {
            StateChannel.Record("Sort", "first");
            StateChannel.Record("Color", "second");

            string batch = Batch();

            Assert.That(batch, Does.Contain("first"));
            Assert.That(batch, Does.Contain("second"));
            Assert.That(batch, Does.Contain("; "));
        }

        [Test]
        public void EmptyAndNullTextAreIgnored()
        {
            StateChannel.Record("Sort", "");
            StateChannel.Record("Sort", null);

            Assert.That(StateChannel.HasPending, Is.False);
        }

        [Test]
        public void SuppressedDropsUserActions()
        {
            StateChannel.Suppressed = true;
            StateChannel.Record("Sort", "dropped");

            Assert.That(StateChannel.HasPending, Is.False);
        }

        [Test]
        public void SuppressionLiftsCleanly()
        {
            StateChannel.Suppressed = true;
            StateChannel.Record("Sort", "dropped");
            StateChannel.Suppressed = false;
            StateChannel.Record("Sort", "kept");

            string batch = Batch();

            Assert.That(batch, Does.Contain("kept"));
            Assert.That(batch, Does.Not.Contain("dropped"));
        }

        [Test]
        public void RecordingInsideAnAgentCallBypassesTheUserQueue()
        {
            StateChannel.InAgentCall = true;
            StateChannel.Record("Tool", "agent did a thing");
            StateChannel.InAgentCall = false;

            Assert.That(StateChannel.HasPending, Is.False);
            Assert.That(StateChannel.TakeAgentBatch(), Is.EqualTo("agent did a thing"));
        }

        [Test]
        public void AgentRecordsAreNotDroppedBySuppression()
        {
            StateChannel.Suppressed = true;
            StateChannel.InAgentCall = true;
            StateChannel.Record("Tool", "still recorded");
            StateChannel.InAgentCall = false;

            Assert.That(StateChannel.TakeAgentBatch(), Is.EqualTo("still recorded"));
        }

        [Test]
        public void AgentBatchJoinsWithSemicolons()
        {
            StateChannel.InAgentCall = true;
            StateChannel.Record("Tool", "one");
            StateChannel.Record("Tool", "two");
            StateChannel.InAgentCall = false;

            Assert.That(StateChannel.TakeAgentBatch(), Is.EqualTo("one; two"));
        }

        [Test]
        public void TakingTheAgentBatchClearsIt()
        {
            StateChannel.InAgentCall = true;
            StateChannel.Record("Tool", "one");
            StateChannel.InAgentCall = false;

            StateChannel.TakeAgentBatch();

            Assert.That(StateChannel.TakeAgentBatch(), Is.Null);
        }

        [Test]
        public void AnEmptyAgentBatchIsNull()
        {
            Assert.That(StateChannel.TakeAgentBatch(), Is.Null);
        }

        [Test]
        public void InAgentCallIsADepthCounterNotAFlag()
        {
            StateChannel.InAgentCall = true;
            StateChannel.InAgentCall = true;
            StateChannel.InAgentCall = false;

            Assert.That(StateChannel.InAgentCall, Is.True);

            StateChannel.InAgentCall = false;

            Assert.That(StateChannel.InAgentCall, Is.False);
        }

        [Test]
        public void ClosingMoreAgentCallsThanWereOpenedDoesNotGoNegative()
        {
            StateChannel.InAgentCall = false;
            StateChannel.InAgentCall = false;
            StateChannel.InAgentCall = true;

            Assert.That(StateChannel.InAgentCall, Is.True);

            StateChannel.InAgentCall = false;

            Assert.That(StateChannel.InAgentCall, Is.False);
        }

        [Test]
        public void TheUserQueueTrimsToItsCap()
        {
            for (int i = 0; i < 45; i++) StateChannel.Record("Sort", $"action{i}");

            string batch = Batch();

            Assert.That(batch, Does.Contain("action44"));
            Assert.That(batch, Does.Contain("action5"));
            Assert.That(batch, Does.Not.Contain("action0,"));
            Assert.That(batch, Does.Not.Contain("action4;"));
        }

        [Test]
        public void ClearPendingEmptiesBothQueues()
        {
            StateChannel.Record("Sort", "user thing");
            StateChannel.InAgentCall = true;
            StateChannel.Record("Tool", "agent thing");

            StateChannel.ClearPending();

            Assert.That(StateChannel.HasPending, Is.False);
            Assert.That(StateChannel.TakeAgentBatch(), Is.Null);
            Assert.That(StateChannel.InAgentCall, Is.False);
        }

        [Test]
        public void RequestSnapshotAloneMakesABatchPending()
        {
            StateChannel.RequestSnapshot();

            Assert.That(StateChannel.HasPending, Is.True);
        }

        [Test]
        public void ASnapshotCarriesTheRecordedState()
        {
            StateChannel.SetState("testKey", "the test panel is open");
            StateChannel.RequestSnapshot();

            string batch = Batch();

            Assert.That(batch, Does.StartWith("[state]"));
            Assert.That(batch, Does.Contain("the test panel is open"));
        }

        [Test]
        public void TheStateLineComesBeforeTheToolLine()
        {
            StateChannel.SetState("testKey", "state fact");
            StateChannel.RequestSnapshot();
            StateChannel.Record("Sort", "user action");

            string batch = Batch();

            Assert.That(batch.IndexOf("[state]"), Is.LessThan(batch.IndexOf("[tool]")));
        }

        [Test]
        public void SettingTheSameStateKeyTwiceReplacesItRatherThanRepeating()
        {
            StateChannel.SetState("replaceKey", "first value");
            StateChannel.SetState("replaceKey", "second value");
            StateChannel.RequestSnapshot();

            string batch = Batch();

            Assert.That(batch, Does.Contain("second value"));
            Assert.That(batch, Does.Not.Contain("first value"));
        }

        [Test]
        public void EmptyStateTextIsIgnored()
        {
            StateChannel.SetState("ignoredKey", "");
            StateChannel.SetState("ignoredKey", null);
            StateChannel.RequestSnapshot();

            Assert.That(Batch() ?? "", Does.Not.Contain("ignoredKey"));
        }

        [Test]
        public void RecordStateBothStoresAndQueues()
        {
            StateChannel.RecordState("bothKey", "a stored and queued fact");

            string batch = Batch();

            Assert.That(batch, Does.Contain("a stored and queued fact"));

            StateChannel.RequestSnapshot();

            Assert.That(Batch(), Does.Contain("a stored and queued fact"));
        }

        [Test]
        public void StateSurvivesClearPending()
        {
            StateChannel.SetState("survivorKey", "a surviving fact");

            StateChannel.ClearPending();
            StateChannel.RequestSnapshot();

            Assert.That(Batch(), Does.Contain("a surviving fact"));
        }
    }
}
