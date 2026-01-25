using MonoMod.Cil;

namespace BepInSerializer.Utils;

internal static class MonoCecilDebuggingUtils
{
    public static void LogMethodBody(this ILCursor cursor)
    {
        BridgeManager.logger.LogWarning($"=========== READING METHOD {cursor.Method.FullName} ==========");
        cursor.Goto(0);
        while (cursor.Index < cursor.Instrs.Count)
        {
            var inst = cursor.Instrs[cursor.Index];
            if (inst.Operand != null)
                BridgeManager.logger.LogInfo($"({inst.OpCode}) -- ({inst.Operand})");
            else
                BridgeManager.logger.LogInfo($"({inst.OpCode})");
            cursor.Index++;
        }
        BridgeManager.logger.LogWarning($"=========== END OF METHOD {cursor.Method.FullName} ==========");
    }
}