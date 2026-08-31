using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace Study.Tests.EditMode.Study
{
    public class StudyLogTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "studylog-tests-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            StudyLog.End(_token);
            _token = 0;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private long _token;

        private long Begin(string participant = "P01", string arm = "Assistant") =>
            _token = StudyLog.Begin(_dir, participant, arm);

        private static List<Dictionary<string, JsonElement>> Read(string path)
        {
            var rows = new List<Dictionary<string, JsonElement>>();
            foreach (string line in File.ReadAllLines(path))
                if (!string.IsNullOrWhiteSpace(line))
                    rows.Add(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line));
            return rows;
        }

        private string OnlyFile() => Directory.GetFiles(_dir, "*.jsonl")[0];

        [Test]
        public void BeginCreatesOneFileAndActivatesTheLog()
        {
            Begin();

            Assert.That(StudyLog.Active, Is.True);
            Assert.That(Directory.GetFiles(_dir, "*.jsonl").Length, Is.EqualTo(1));
        }

        [Test]
        public void FileNameCarriesParticipantAndArm()
        {
            Begin("P07", "NoAssistant");

            string file = Path.GetFileName(OnlyFile());

            Assert.That(file, Does.Contain("P07"));
            Assert.That(file, Does.Contain("NoAssistant"));
        }

        [Test]
        public void SpacesInParticipantBecomeUnderscores()
        {
            Begin("P 07", "Sample");

            Assert.That(Path.GetFileName(OnlyFile()), Does.Contain("P_07"));
        }

        [Test]
        public void BeginWritesASessionBeginRow()
        {
            Begin("P02", "Sample");

            var rows = Read(OnlyFile());

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["type"].GetString(), Is.EqualTo("session_begin"));
            Assert.That(rows[0]["participant"].GetString(), Is.EqualTo("P02"));
            Assert.That(rows[0]["arm"].GetString(), Is.EqualTo("Sample"));
        }

        [Test]
        public void EveryRowCarriesTheSharedTimelineFields()
        {
            StudyLog.Frame = 4242;
            StudyLog.RealtimeMs = 1234.5f;
            Begin();
            StudyLog.Event("marker", new Dictionary<string, object> { { "label", "x" } });

            var rows = Read(OnlyFile());

            foreach (var row in rows)
            {
                Assert.That(row.ContainsKey("t_ms"), Is.True);
                Assert.That(row.ContainsKey("rt_ms"), Is.True);
                Assert.That(row.ContainsKey("frame"), Is.True);
                Assert.That(row.ContainsKey("utc"), Is.True);
            }
            Assert.That(rows[1]["frame"].GetInt32(), Is.EqualTo(4242));
            Assert.That(rows[1]["rt_ms"].GetDouble(), Is.EqualTo(1234.5d).Within(0.1d));
        }

        [Test]
        public void EventsAppendInOrder()
        {
            Begin();
            StudyLog.Event("first");
            StudyLog.Event("second");
            StudyLog.Event("third");

            var rows = Read(OnlyFile());

            Assert.That(rows.ConvertAll(r => r["type"].GetString()),
                Is.EqualTo(new[] { "session_begin", "first", "second", "third" }));
        }

        [Test]
        public void CustomFieldsSurviveTheRoundTrip()
        {
            Begin();
            StudyLog.Event("ui_span", new Dictionary<string, object> {
                { "name", "grid_rebuild" },
                { "ms", 62.5 },
                { "cells", 60 },
                { "forced", true }
            });

            var row = Read(OnlyFile())[1];

            Assert.That(row["name"].GetString(), Is.EqualTo("grid_rebuild"));
            Assert.That(row["ms"].GetDouble(), Is.EqualTo(62.5d).Within(1e-6d));
            Assert.That(row["cells"].GetInt32(), Is.EqualTo(60));
            Assert.That(row["forced"].GetBoolean(), Is.True);
        }

        [Test]
        public void EventsBeforeBeginAreDropped()
        {
            StudyLog.Event("orphan");

            Assert.That(StudyLog.Active, Is.False);
            Assert.That(Directory.Exists(_dir), Is.False);
        }

        [Test]
        public void EventsAfterEndAreDropped()
        {
            long token = Begin();
            StudyLog.Event("kept");
            StudyLog.End(token);
            _token = 0;
            StudyLog.Event("dropped");

            var types = Read(OnlyFile()).ConvertAll(r => r["type"].GetString());

            Assert.That(StudyLog.Active, Is.False);
            Assert.That(types, Does.Contain("kept"));
            Assert.That(types, Does.Not.Contain("dropped"));
        }

        [Test]
        public void EndWithAStaleTokenDoesNotCloseTheLog()
        {
            long first = Begin();
            StudyLog.End(first);
            long second = Begin();

            StudyLog.End(first);

            Assert.That(StudyLog.Active, Is.True);
            _token = second;
        }

        [Test]
        public void BeginTwiceClosesTheFirstLog()
        {
            Begin("P01", "Sample");
            Begin("P02", "Assistant");

            Assert.That(Directory.GetFiles(_dir, "*.jsonl").Length, Is.EqualTo(2));
            Assert.That(StudyLog.Active, Is.True);
        }

        [Test]
        public void EndIsIdempotent()
        {
            long token = Begin();
            StudyLog.End(token);
            StudyLog.End(token);
            _token = 0;

            Assert.That(StudyLog.Active, Is.False);
        }
    }
}
