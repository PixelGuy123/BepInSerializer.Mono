using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnitySerializationBridge.Utils;

public static class JsonUtils
{
    public static string ToMultiJson(this IEnumerable collection)
    {
        StringBuilder sb = new();
        foreach (object item in collection)
        {
            // Serialize individual items as jsons
            sb.Append(JsonUtility.ToJson(item));
        }
        return sb.ToString();
    }

    public static List<string> UnpackMultiJson(string json)
    {
        List<string> result = [];
        if (string.IsNullOrEmpty(json)) return result;

        int braceDepth = 0;
        int startIndex = 0;
        bool insideString = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            // Handle strings to avoid counting braces inside them
            if (c == '"' && (i <= 1 || json[i - 1] != '\\' || json[i - 2] == '\\')) // Accounting to '\\"' edge case
            {
                insideString = !insideString;
            }

            if (!insideString)
            {
                if (c == '{')
                {
                    if (braceDepth == 0) startIndex = i;
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0)
                    {
                        // Found a complete JSON object
                        result.Add(json.Substring(startIndex, i - startIndex + 1));
                    }
                }
            }
        }
        return result;
    }
}