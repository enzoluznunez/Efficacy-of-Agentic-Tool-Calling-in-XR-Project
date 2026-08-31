using System.Collections.Generic;
using NUnit.Framework;

namespace Study.Tests.EditMode.Study
{
    public class FrameBudgetTests
    {
        [TearDown]
        public void TearDown() => FrameBudget.Reset();

        private static List<float> Samples(params float[] ms) => new List<float>(ms);

        [Test]
        public void DefaultsToSeventyTwoHertz()
        {
            Assert.That(FrameBudget.DisplayHz, Is.EqualTo(72f));
            Assert.That(FrameBudget.BudgetMs, Is.EqualTo(1000f / 72f).Within(1e-4f));
        }

        [TestCase(90f, 11.111f)]
        [TestCase(72f, 13.888f)]
        [TestCase(120f, 8.333f)]
        public void BudgetFollowsDisplayRate(float hz, float expectedMs)
        {
            FrameBudget.SetDisplayHz(hz);

            Assert.That(FrameBudget.BudgetMs, Is.EqualTo(expectedMs).Within(0.01f));
        }

        [TestCase(0f)]
        [TestCase(-90f)]
        [TestCase(5000f)]
        public void ImplausibleDisplayRateIsIgnored(float hz)
        {
            FrameBudget.SetDisplayHz(hz);

            Assert.That(FrameBudget.DisplayHz, Is.EqualTo(FrameBudget.DefaultHz));
        }

        [Test]
        public void EmptySampleSetAnalysesToZero()
        {
            FrameStats s = FrameBudget.Analyse(Samples(), 10f);

            Assert.That(s.frames, Is.EqualTo(0));
            Assert.That(s.p50, Is.EqualTo(0f));
            Assert.That(s.max, Is.EqualTo(0f));
        }

        [Test]
        public void CountsMeanAndMax()
        {
            FrameStats s = FrameBudget.Analyse(Samples(10f, 20f, 30f), 100f);

            Assert.That(s.frames, Is.EqualTo(3));
            Assert.That(s.mean, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(s.max, Is.EqualTo(30f));
        }

        [Test]
        public void BucketsAreExclusiveAndOrdered()
        {
            FrameStats s = FrameBudget.Analyse(Samples(5f, 12f, 25f, 45f), 10f);

            Assert.That(s.over1x, Is.EqualTo(1));
            Assert.That(s.over2x, Is.EqualTo(1));
            Assert.That(s.over4x, Is.EqualTo(1));
        }

        [Test]
        public void FrameExactlyAtBudgetIsNotOver()
        {
            FrameStats s = FrameBudget.Analyse(Samples(10f, 10f), 10f);

            Assert.That(s.over1x, Is.EqualTo(0));
            Assert.That(s.over2x, Is.EqualTo(0));
            Assert.That(s.over4x, Is.EqualTo(0));
        }

        [Test]
        public void PercentilesTrackTheSortedOrder()
        {
            var samples = new List<float>();
            for (int i = 1; i <= 100; i++) samples.Add(i);

            FrameStats s = FrameBudget.Analyse(samples, 1000f);

            Assert.That(s.p50, Is.EqualTo(50f).Within(1.5f));
            Assert.That(s.p95, Is.EqualTo(95f).Within(1.5f));
            Assert.That(s.p99, Is.EqualTo(99f).Within(1.5f));
        }

        [Test]
        public void AnalyseDoesNotReorderTheCallersList()
        {
            var samples = Samples(30f, 10f, 20f);

            FrameBudget.Analyse(samples, 10f);

            Assert.That(samples, Is.EqualTo(new[] { 30f, 10f, 20f }));
        }

        [Test]
        public void SingleSamplePercentileIsThatSample()
        {
            FrameStats s = FrameBudget.Analyse(Samples(42f), 10f);

            Assert.That(s.p50, Is.EqualTo(42f));
            Assert.That(s.p99, Is.EqualTo(42f));
        }

        [TestCase(10f, 10f, 0)]
        [TestCase(19f, 10f, 0)]
        [TestCase(21f, 10f, 1)]
        [TestCase(45f, 10f, 3)]
        public void FramesLostCountsWholeMissedFrames(float ms, float budget, int expected)
        {
            Assert.That(FrameBudget.FramesLost(ms, budget), Is.EqualTo(expected));
        }

        [Test]
        public void WindowCollectsAndThenDrains()
        {
            FrameBudget.Record(5f);
            FrameBudget.Record(50f);
            Assert.That(FrameBudget.WindowCount, Is.EqualTo(2));

            var fields = new Dictionary<string, object>();
            Assert.That(FrameBudget.TryTakeWindow(fields), Is.True);
            Assert.That(fields["frameFrames"], Is.EqualTo(2));
            Assert.That(FrameBudget.WindowCount, Is.EqualTo(0));
        }

        [Test]
        public void EmptyWindowReportsNothing()
        {
            var fields = new Dictionary<string, object>();

            Assert.That(FrameBudget.TryTakeWindow(fields), Is.False);
            Assert.That(fields, Is.Empty);
        }

        [Test]
        public void BeginPhaseClearsPhaseSamplesButNotTheWindow()
        {
            FrameBudget.Record(5f);
            FrameBudget.BeginPhase("task:1");
            FrameBudget.Record(6f);

            var phase = new Dictionary<string, object>();
            FrameBudget.AppendPhaseStats(phase);

            Assert.That(phase["phase"], Is.EqualTo("task:1"));
            Assert.That(phase["frameFrames"], Is.EqualTo(1));
            Assert.That(FrameBudget.WindowCount, Is.EqualTo(2));
        }

        [Test]
        public void PhaseStatsCarryTheBudget()
        {
            FrameBudget.SetDisplayHz(90f);
            FrameBudget.BeginPhase("task:2");
            FrameBudget.Record(11f);

            var phase = new Dictionary<string, object>();
            FrameBudget.AppendPhaseStats(phase);

            Assert.That((double)phase["budgetMs"], Is.EqualTo(11.11d).Within(0.01d));
        }
    }
}
