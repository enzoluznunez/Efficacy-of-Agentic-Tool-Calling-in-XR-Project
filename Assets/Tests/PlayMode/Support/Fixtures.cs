using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Study.Tests.PlayMode.Support
{
    public static class TestGrid
    {
        public const int Rows = 3;
        public const int Cols = 4;

        public static readonly string[] RowTitles = { "Alpha", "Beta", "Gamma" };
        public static readonly string[] ColTitles = { "Jan", "Feb", "Mar", "Apr" };

        public static float ValueAt(int row, int col) => row * Cols + col + 1;

        public static string Simple() => Csv(RowTitles, ColTitles, ValueAt);

        public static string Csv(string[] rows, string[] cols, Func<int, int, float> value)
        {
            var sb = new StringBuilder();
            sb.Append("Item\\Month");
            for (int c = 0; c < cols.Length; c++) sb.Append(',').Append(cols[c]);
            sb.Append('\n');

            for (int r = 0; r < rows.Length; r++)
            {
                sb.Append(rows[r]);
                for (int c = 0; c < cols.Length; c++)
                    sb.Append(',').Append(value(r, c).ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }

    public sealed class SceneFixture : IDisposable
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public Parser Data { get; private set; }

        public Parser SpawnData(string csv)
        {
            var host = New("DataUnderTest");
            Data = host.AddComponent<Parser>();
            Data.LoadFromCsvText(csv);
            return Data;
        }

        public T Spawn<T>(string name) where T : Component => New(name).AddComponent<T>();

        public GameObject New(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        public void Dispose()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
            Data = null;
        }
    }
}
