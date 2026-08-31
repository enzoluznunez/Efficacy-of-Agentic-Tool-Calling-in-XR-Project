using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;
using GType = Google.GenAI.Types.Type;

namespace Study.Tests.EditMode.Gemini
{
    public class ToolArgumentsTests
    {
        public enum Axis { Row, Column }

        public class Simple
        {
            public string label;
            public int count;
        }

        public class WithOptional
        {
            public string label;
            [Optional] public int spare;
        }

        public class EnumArgs
        {
            public Axis axis;
        }

        public class ValuesArgs
        {
            [Values("red", "blue")] public string color;
        }

        public class Point
        {
            public int x;
            public int y;
        }

        public class HasNested
        {
            public Point at;
        }

        public class ListArgs
        {
            public List<int> ids;
        }

        public class ArrayArgs
        {
            public int[] ids;
        }

        public class Numbers
        {
            public float ratio;
            public double precise;
            public long big;
            public bool flag;
        }

        public class Documented
        {
            [Doc("the label")] public string label;
            [Limits(0d, 10d)] public int count;
            [Optional] public bool flag;
        }

        public class ListOfPoints
        {
            public List<Point> points;
        }

        private readonly List<JsonDocument> _docs = new List<JsonDocument>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _docs.Count; i++) _docs[i].Dispose();
            _docs.Clear();
        }

        private JsonElement Json(string json)
        {
            JsonDocument doc = JsonDocument.Parse(json);
            _docs.Add(doc);
            return doc.RootElement;
        }

        private static Dictionary<string, object> Map(params object[] pairs)
        {
            var map = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) map[(string)pairs[i]] = pairs[i + 1];
            return map;
        }

        private static string TypeName(object type) => type?.ToString();

        [Test]
        public void BindsStringAndIntegerFields()
        {
            var bound = (Simple)ToolArguments.Bind(typeof(Simple),
                Map("label", "sales", "count", 3), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.label, Is.EqualTo("sales"));
            Assert.That(bound.count, Is.EqualTo(3));
        }

        [Test]
        public void BindsFromJsonElements()
        {
            JsonElement root = Json("{\"label\":\"sales\",\"count\":3}");
            var bound = (Simple)ToolArguments.Bind(typeof(Simple),
                Map("label", root.GetProperty("label"), "count", root.GetProperty("count")),
                out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.label, Is.EqualTo("sales"));
            Assert.That(bound.count, Is.EqualTo(3));
        }

        [Test]
        public void MissingRequiredFieldIsRejected()
        {
            object bound = ToolArguments.Bind(typeof(Simple), Map("label", "sales"), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Is.EqualTo("Provide 'count'."));
        }

        [Test]
        public void NullValueCountsAsMissing()
        {
            object bound = ToolArguments.Bind(typeof(Simple),
                Map("label", "sales", "count", null), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Is.EqualTo("Provide 'count'."));
        }

        [Test]
        public void NullMapIsRejectedOnTheFirstRequiredField()
        {
            object bound = ToolArguments.Bind(typeof(Simple), null, out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Does.StartWith("Provide '"));
        }

        [Test]
        public void MissingOptionalFieldKeepsItsDefault()
        {
            var bound = (WithOptional)ToolArguments.Bind(typeof(WithOptional),
                Map("label", "sales"), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.label, Is.EqualTo("sales"));
            Assert.That(bound.spare, Is.EqualTo(0));
        }

        [Test]
        public void NumericStringCoercesToInteger()
        {
            JsonElement root = Json("{\"count\":\"7\"}");
            var bound = (Simple)ToolArguments.Bind(typeof(Simple),
                Map("label", "sales", "count", root.GetProperty("count")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.count, Is.EqualTo(7));
        }

        [Test]
        public void NonNumericStringIsRejectedWithTheFieldName()
        {
            JsonElement root = Json("{\"count\":\"lots\"}");
            object bound = ToolArguments.Bind(typeof(Simple),
                Map("label", "sales", "count", root.GetProperty("count")), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Is.EqualTo("'count' is not a valid integer."));
        }

        [Test]
        public void CoercesFloatDoubleLongAndBool()
        {
            JsonElement root = Json("{\"ratio\":\"1.5\",\"precise\":2.25,\"big\":9000000000,\"flag\":true}");
            var bound = (Numbers)ToolArguments.Bind(typeof(Numbers),
                Map("ratio", root.GetProperty("ratio"),
                    "precise", root.GetProperty("precise"),
                    "big", root.GetProperty("big"),
                    "flag", root.GetProperty("flag")),
                out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.ratio, Is.EqualTo(1.5f).Within(1e-6f));
            Assert.That(bound.precise, Is.EqualTo(2.25d).Within(1e-9d));
            Assert.That(bound.big, Is.EqualTo(9000000000L));
            Assert.That(bound.flag, Is.True);
        }

        [Test]
        public void NumericOneCoercesToTrue()
        {
            JsonElement root = Json("{\"ratio\":0,\"precise\":0,\"big\":0,\"flag\":1}");
            var bound = (Numbers)ToolArguments.Bind(typeof(Numbers),
                Map("ratio", root.GetProperty("ratio"),
                    "precise", root.GetProperty("precise"),
                    "big", root.GetProperty("big"),
                    "flag", root.GetProperty("flag")),
                out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.flag, Is.True);
        }

        [Test]
        public void EnumParsesCaseInsensitivelyAndTrimmed()
        {
            var bound = (EnumArgs)ToolArguments.Bind(typeof(EnumArgs),
                Map("axis", "  column  "), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.axis, Is.EqualTo(Axis.Column));
        }

        [Test]
        public void UnknownEnumNameListsTheAllowedValues()
        {
            object bound = ToolArguments.Bind(typeof(EnumArgs), Map("axis", "diagonal"), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Is.EqualTo("'axis' must be one of: row, column."));
        }

        [Test]
        public void ValuesAttributeAcceptsAnyCase()
        {
            var bound = (ValuesArgs)ToolArguments.Bind(typeof(ValuesArgs),
                Map("color", "RED"), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.color, Is.EqualTo("red"));
        }

        [Test]
        public void ValuesAttributeRejectsAnythingElse()
        {
            object bound = ToolArguments.Bind(typeof(ValuesArgs), Map("color", "teal"), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Is.EqualTo("'color' must be one of: red, blue."));
        }

        [TestCase("blue")]
        [TestCase("  blue  ")]
        [TestCase("BLUE")]
        [TestCase("\tBlue\n")]
        public void ValuesAttributeStoresTheCanonicalValue(string sent)
        {
            var bound = (ValuesArgs)ToolArguments.Bind(typeof(ValuesArgs),
                Map("color", sent), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.color, Is.EqualTo("blue"));
        }

        [Test]
        public void BindsNestedObjectFromDictionary()
        {
            var bound = (HasNested)ToolArguments.Bind(typeof(HasNested),
                Map("at", Map("x", 1, "y", 2)), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.at.x, Is.EqualTo(1));
            Assert.That(bound.at.y, Is.EqualTo(2));
        }

        [Test]
        public void BindsNestedObjectFromJsonElement()
        {
            JsonElement root = Json("{\"at\":{\"x\":4,\"y\":5}}");
            var bound = (HasNested)ToolArguments.Bind(typeof(HasNested),
                Map("at", root.GetProperty("at")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.at.x, Is.EqualTo(4));
            Assert.That(bound.at.y, Is.EqualTo(5));
        }

        [Test]
        public void StringWhereAnObjectIsExpectedExplainsTheShape()
        {
            object bound = ToolArguments.Bind(typeof(HasNested), Map("at", "1,2"), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Does.Contain("'at' must be an object with fields: x, y."));
            Assert.That(error, Does.Contain("send it as a nested object"));
        }

        [Test]
        public void NestedObjectMissingAFieldIsRejected()
        {
            object bound = ToolArguments.Bind(typeof(HasNested), Map("at", Map("x", 1)), out string error);

            Assert.That(bound, Is.Null);
            Assert.That(error, Does.Contain("'at' must be an object with fields: x, y."));
        }

        [Test]
        public void BindsListFromJsonArray()
        {
            JsonElement root = Json("{\"ids\":[1,2,3]}");
            var bound = (ListArgs)ToolArguments.Bind(typeof(ListArgs),
                Map("ids", root.GetProperty("ids")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.ids, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void BindsArrayFromJsonArray()
        {
            JsonElement root = Json("{\"ids\":[4,5]}");
            var bound = (ArrayArgs)ToolArguments.Bind(typeof(ArrayArgs),
                Map("ids", root.GetProperty("ids")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.ids, Is.EqualTo(new[] { 4, 5 }));
        }

        [Test]
        public void SingleScalarBecomesAOneItemList()
        {
            JsonElement root = Json("{\"ids\":7}");
            var bound = (ListArgs)ToolArguments.Bind(typeof(ListArgs),
                Map("ids", root.GetProperty("ids")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.ids, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void EmptyJsonArrayBindsToAnEmptyList()
        {
            JsonElement root = Json("{\"ids\":[]}");
            var bound = (ListArgs)ToolArguments.Bind(typeof(ListArgs),
                Map("ids", root.GetProperty("ids")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.ids, Is.Empty);
        }

        [Test]
        public void BindsListOfNestedObjects()
        {
            JsonElement root = Json("{\"points\":[{\"x\":1,\"y\":2},{\"x\":3,\"y\":4}]}");
            var bound = (ListOfPoints)ToolArguments.Bind(typeof(ListOfPoints),
                Map("points", root.GetProperty("points")), out string error);

            Assert.That(error, Is.Null);
            Assert.That(bound.points.Count, Is.EqualTo(2));
            Assert.That(bound.points[1].y, Is.EqualTo(4));
        }

        [Test]
        public void SchemaMarksOnlyNonOptionalFieldsRequired()
        {
            var schema = ToolArguments.Schema(typeof(Documented));

            Assert.That(schema.Required, Is.EquivalentTo(new[] { "label", "count" }));
            Assert.That(schema.Properties.Keys, Is.EquivalentTo(new[] { "label", "count", "flag" }));
        }

        [Test]
        public void SchemaCarriesDocTextAsDescription()
        {
            var schema = ToolArguments.Schema(typeof(Documented));

            Assert.That(schema.Properties["label"].Description, Is.EqualTo("the label"));
        }

        [Test]
        public void SchemaCarriesLimitsAsMinimumAndMaximum()
        {
            var schema = ToolArguments.Schema(typeof(Documented));

            Assert.That(schema.Properties["count"].Minimum, Is.EqualTo(0d));
            Assert.That(schema.Properties["count"].Maximum, Is.EqualTo(10d));
        }

        [Test]
        public void SchemaLowercasesEnumNames()
        {
            var schema = ToolArguments.Schema(typeof(EnumArgs));

            Assert.That(schema.Properties["axis"].Enum, Is.EqualTo(new[] { "row", "column" }));
            Assert.That(TypeName(schema.Properties["axis"].Type), Is.EqualTo(TypeName(GType.String)));
        }

        [Test]
        public void SchemaUsesValuesAttributeOverEnumNames()
        {
            var schema = ToolArguments.Schema(typeof(ValuesArgs));

            Assert.That(schema.Properties["color"].Enum, Is.EqualTo(new[] { "red", "blue" }));
        }

        [Test]
        public void SchemaMapsPrimitiveTypes()
        {
            var schema = ToolArguments.Schema(typeof(Numbers));

            Assert.That(TypeName(schema.Properties["ratio"].Type), Is.EqualTo(TypeName(GType.Number)));
            Assert.That(TypeName(schema.Properties["precise"].Type), Is.EqualTo(TypeName(GType.Number)));
            Assert.That(TypeName(schema.Properties["big"].Type), Is.EqualTo(TypeName(GType.Integer)));
            Assert.That(TypeName(schema.Properties["flag"].Type), Is.EqualTo(TypeName(GType.Boolean)));
        }

        [Test]
        public void SchemaNestsObjectProperties()
        {
            var schema = ToolArguments.Schema(typeof(HasNested));
            var nested = schema.Properties["at"];

            Assert.That(TypeName(nested.Type), Is.EqualTo(TypeName(GType.Object)));
            Assert.That(nested.Properties.Keys, Is.EquivalentTo(new[] { "x", "y" }));
            Assert.That(nested.Required, Is.EquivalentTo(new[] { "x", "y" }));
        }

        [Test]
        public void SchemaDescribesListItemType()
        {
            var schema = ToolArguments.Schema(typeof(ListArgs));
            var ids = schema.Properties["ids"];

            Assert.That(TypeName(ids.Type), Is.EqualTo(TypeName(GType.Array)));
            Assert.That(TypeName(ids.Items.Type), Is.EqualTo(TypeName(GType.Integer)));
        }

        [Test]
        public void SchemaWithNoRequiredFieldsLeavesRequiredNull()
        {
            var schema = ToolArguments.Schema(typeof(AllOptional));

            Assert.That(schema.Required, Is.Null);
        }

        public class AllOptional
        {
            [Optional] public string a;
            [Optional] public int b;
        }

        [Test]
        public void SchemaIsCachedPerType()
        {
            var first = ToolArguments.Schema(typeof(Simple));
            var second = ToolArguments.Schema(typeof(Simple));

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void SchemaOfNullTypeIsNull()
        {
            Assert.That(ToolArguments.Schema(null), Is.Null);
        }
    }
}
