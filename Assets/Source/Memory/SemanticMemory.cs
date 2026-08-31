using System.Collections.Generic;

public static class SemanticMemory
{
    private static readonly object _factGate = new object();
    private static readonly List<string> _facts = new List<string>();

    public static void SetFacts(IEnumerable<string> facts)
    {
        lock (_factGate)
        {
            _facts.Clear();
            if (facts == null) return;
            foreach (string f in facts)
                if (!string.IsNullOrEmpty(f)) _facts.Add(f);
        }
    }

    public static List<string> FactsSnapshot()
    {
        lock (_factGate) return new List<string>(_facts);
    }

    public static void ClearFacts()
    {
        lock (_factGate) _facts.Clear();
    }
}
