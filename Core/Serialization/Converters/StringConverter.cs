using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// StringConverter (internal)
// Handles the special case for strings to be copied properly, since they don't have a default constructor
internal class StringConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context) =>
        context.ValueType == typeof(string);

    public override object Convert(FieldContext context)
    {
        if (context.OriginalValue == null || context.OriginalValue is not string ogStrVal) return null;
        return string.Copy(ogStrVal); // Makes a new copy of the string
    }
}