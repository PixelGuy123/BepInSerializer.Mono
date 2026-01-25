using System;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// ArrayConverter (internal)
internal class ArrayConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        return type.IsArray;
    }

    public override object Convert(FieldContext context)
    {
        var sourceArray = (Array)context.OriginalValue;
        if (sourceArray == null) return null;
        var elementType = context.ValueType.GetElementType();
        int rank = sourceArray.Rank;

        // Get the lengths of all dimensions
        int[] lengths = new int[rank];
        for (int i = 0; i < rank; i++)
            lengths[i] = sourceArray.GetLength(i);


        // Create the new Multi-Dimensional Array
        if (!TryConstructNewArray(context, lengths, out var newArray))
            return null;

        // Populate using a recursive helper to handle N-dimensions
        int[] currentIndices = new int[rank];
        PopulateArray(sourceArray, newArray, context, elementType, currentIndices, 0);

        return newArray;
    }

    // Little complex to explain, but thinking of the indices array as a key pair to indicate the position inside this multi-dimensional array helps
    private void PopulateArray(Array source, Array target, FieldContext context, Type elementType, int[] indices, int currentDimension)
    {
        // Get the length of the dimension we are currently looping over
        int length = source.GetLength(currentDimension);

        for (int i = 0; i < length; i++)
        {
            // Update the index for the current dimension
            indices[currentDimension] = i;

            if (currentDimension == source.Rank - 1)
            {
                // We now have a full set of indices (e.g., [0, 1, 4]) to identify a single item.
                var originalValue = source.GetValue(indices);

                // Perform your conversion
                var convertedValue = ReConvert(FieldContext.CreateRemoteContext(context, originalValue, elementType));

                target.SetValue(convertedValue, indices);
            }
            else
            {
                // RECURSIVE STEP: Dive into the next dimension
                PopulateArray(source, target, context, elementType, indices, currentDimension + 1);
            }
        }
    }
}