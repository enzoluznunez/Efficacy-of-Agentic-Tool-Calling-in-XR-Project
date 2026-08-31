using System;
using System.Text;
using UnityEngine;

namespace Study.Tests.EditMode.Support
{
    public sealed class GridFixture : IDisposable
    {
        private readonly GameObject _host;

        public Parser Source { get; }

        public GridFixture(string csv)
        {
            _host = new GameObject("GridUnderTest");
            Source = _host.AddComponent<Parser>();
            Source.LoadFromCsvText(csv);
        }

        public void Dispose()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }
    }

    public static class TestGrid
    {
        public const int Rows = 3;
        public const int Cols = 4;

        public static readonly string[] RowTitles = { "Alpha", "Beta", "Gamma" };
        public static readonly string[] ColTitles = { "Jan", "Feb", "Mar", "Apr" };

        public static float ValueAt(int row, int col) => row * Cols + col + 1;

        public static string Simple() => Csv(RowTitles, ColTitles, ValueAt);

        public static GridFixture Load() => new GridFixture(Simple());

        public static GridFixture Load(string csv) => new GridFixture(csv);

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

        public static int[] Reversed(int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = count - 1 - i;
            return order;
        }
    }
}
