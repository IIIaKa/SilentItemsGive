/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  GitHub repository page: https://github.com/IIIaKa/SilentItemsGive
*  
*  uMod plugin page: https://umod.org/plugins/silent-items-give
*  uMod license: https://umod.org/plugins/silent-items-give#license
*  
*  Codefling plugin page: https://codefling.com/plugins/silent-items-give
*  Codefling license: https://codefling.com/plugins/silent-items-give?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/silent-items-give/
*
*  Copyright © 2025 IIIaKa
*/

// Reference: 0Harmony
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Silent Items Give", "IIIaKa", "0.1.0")]
    [Description("Allows toggling silent item giving(chat notifications and ownership) via Harmony patching.")]
    class SilentItemsGive : RustPlugin
    {
        #region ~Variables~
        private Harmony _harmony;
        private static bool _debug = false, _silentGive = true;
        private const string PERMISSION_ADMIN = "silentitemsgive.admin", IdForHarmony = "iiiaka.silentitemsgive";
        #endregion
        
        #region ~Oxide Hooks~
        void OnServerInitialized(bool initial)
        {
#if CARBON
            if (!Carbon.Community.Runtime.Config.IsModded)
#else
            if (!Interface.Oxide.Config.Options.Modded)
#endif
            {
                PrintError("ATTENTION! Your server is a Community server(not Modded). It is not recommended to use plugins like this on such servers! Use it at your own risk!");
            }
            
            _harmony = new Harmony(IdForHarmony);
            
            PatchMethod(typeof(ConVar.Inventory), "give", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_Give)), new Type[] { typeof(ConsoleSystem.Arg) });
            PatchMethod(typeof(ConVar.Inventory), "giveid", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_GiveId)), new Type[] { typeof(ConsoleSystem.Arg) });
            PatchMethod(typeof(ConVar.Inventory), "givearm", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_GiveArm)), new Type[] { typeof(ConsoleSystem.Arg) });
            PatchMethod(typeof(ConVar.Inventory), "giveto", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_GiveTo)), new Type[] { typeof(ConsoleSystem.Arg) });
            PatchMethod(typeof(ConVar.Inventory), "giveall", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_GiveAll)), new Type[] { typeof(ConsoleSystem.Arg) });
            PatchMethod(typeof(ConVar.Inventory), "giveBp", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_GiveBp)), new Type[] { typeof(ConsoleSystem.Arg) });
            PatchMethod(typeof(ConVar.Inventory), "copyTo", new HarmonyMethod(typeof(SilentItemsGive), nameof(Transpiler_CopyTo)), new Type[] { typeof(BasePlayer), typeof(BasePlayer) });
            
            Puts($"Patch '{IdForHarmony}' by '{Author}' has been successfully applied!");
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            AddCovalenceCommand("silentgive.toggle", nameof(Command_Toggle));
            _silentGive = Interface.Oxide.DataFileSystem.ReadObject<object>(Name) is bool savedVal ? savedVal : true;
            
            void PatchMethod(Type targetType, string methodName, HarmonyMethod transpiler, Type[] parameters = null)
            {
                var targetMethod = parameters == null ? AccessTools.Method(targetType, methodName) : AccessTools.Method(targetType, methodName, parameters);
                if (targetMethod == null)
                    PrintError($"Failed to find method {targetType.Name}.{methodName}!");
                else
                {
                    _harmony.Patch(targetMethod, transpiler: transpiler);
                    if (_debug)
                        PrintWarning($"Patched {targetType.Name}.{methodName} successfully!");
                }
            }
        }

        void Unload()
        {
            _harmony?.UnpatchAll(IdForHarmony);
            Puts($"Patch '{IdForHarmony}' by '{Author}' has been successfully removed!");
            Interface.Oxide.DataFileSystem.WriteObject(Name, _silentGive);
        }
        #endregion
        
        #region ~Commands~
        private void Command_Toggle(IPlayer player, string command, string[] args)
        {
            if (player == null || (!player.IsAdmin && !permission.UserHasPermission(player.Id, PERMISSION_ADMIN))) return;
            
            if (args == null || args.Length < 1)
                _silentGive = !_silentGive;
            else
            {
                if (!bool.TryParse(args[0], out var newVal))
                {
                    if (!int.TryParse(args[0], out var intVal))
                    {
                        player.Reply("You have provided an incorrect argument! The argument must be a boolean value.");
                        return;
                    }
                    newVal = Math.Clamp(intVal, 0, 1) == 1;
                }
                _silentGive = newVal;
            }
            player.Reply(_silentGive ? "You have successfully DISABLED broadcasting messages to all players when issuing items!" :
                "You have successfully ENABLED broadcasting messages to all players when issuing items!");
        }
        #endregion

        #region ~Harmony Patch~
        public static IEnumerable<CodeInstruction> Transpiler_Give(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - give**********");

            int chatIndex = -1, ownerIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];

                if (_debug && ((i >= 48 && i < 58) || (i >= 198 && i < 208)))
                {
                    if (i == 198)
                        Interface.Oxide.LogInfo("...");
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                }

                if (code.operand is MethodInfo method)
                {
                    if (code.opcode == OpCodes.Call && method.Name == "Log")
                        chatIndex = i;
                    else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                        ownerIndex = i;
                }
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex, "give");
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex, "give");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if ((i >= 48 && i < 58) || (i >= 200 && i < 210))
                    {
                        code = result[i];
                        if (i == 200)
                            Interface.Oxide.LogInfo("...");
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - give**********");
            }

            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveId(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - giveid**********");

            int chatIndex = -1, ownerIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (_debug && ((i >= 32 && i < 42) || (i >= 94 && i < 104)))
                {
                    if (i == 94)
                        Interface.Oxide.LogInfo("...");
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                }

                if (code.operand is MethodInfo method)
                {
                    if (code.opcode == OpCodes.Call && method.Name == "Log")
                        chatIndex = i;
                    else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                        ownerIndex = i;
                }
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex, "giveid");
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex, "giveid");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if ((i >= 32 && i < 42) || (i >= 96 && i < 106))
                    {
                        code = result[i];
                        if (i == 96)
                            Interface.Oxide.LogInfo("...");
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - giveid**********");
            }

            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveArm(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - givearm**********");

            int chatIndex = -1, ownerIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (_debug && ((i >= 32 && i < 42) || (i >= 97 && i < 107)))
                {
                    if (i == 97)
                        Interface.Oxide.LogInfo("...");
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                }

                if (code.operand is MethodInfo method)
                {
                    if (code.opcode == OpCodes.Call && method.Name == "Log")
                        chatIndex = i;
                    else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                        ownerIndex = i;
                }
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex, "givearm");
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex, "givearm");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if ((i >= 32 && i < 42) || (i >= 99 && i < 109))
                    {
                        code = result[i];
                        if (i == 99)
                            Interface.Oxide.LogInfo("...");
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - givearm**********");
            }

            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveTo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - giveto**********");

            int chatIndex = -1, ownerIndex = -1;
            CodeInstruction code, jumpCode = null;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (_debug && ((i >= 53 && i < 63) || (i >= 115 && i < 125)))
                {
                    if (i == 115)
                        Interface.Oxide.LogInfo("...");
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                }

                if (code.opcode == OpCodes.Ret)
                    jumpCode = code;
                else if (code.operand is MethodInfo method)
                {
                    if (code.opcode == OpCodes.Call && method.Name == "Log")
                        chatIndex = i;
                    else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                        ownerIndex = i;
                }
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (chatIndex >= 0 && jumpCode != null)
                Patch_ChatMessage(result, generator, chatIndex, "giveto", jumpCode);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex, "giveto");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if ((i >= 53 && i < 63) || (i >= 117 && i < 119))
                    {
                        code = result[i];
                        if (i == 117)
                            Interface.Oxide.LogInfo("...");
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - giveto**********");
            }

            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveAll(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - giveall**********");

            int targetIndex = -1, ownerIndex = -1;
            CodeInstruction code, jumpCode = null;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (_debug && ((i >= 46 && i < 56) || (i >= 116 && i < 126)))
                {
                    if (i == 116)
                        Interface.Oxide.LogInfo("...");
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                }

                if (code.opcode == OpCodes.Ret)
                    jumpCode = code;
                else if (code.opcode == OpCodes.Endfinally)
                    targetIndex = i;
                else if (code.operand is MethodInfo method && code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                    ownerIndex = i;
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (targetIndex >= 0 && jumpCode != null)
                Patch_AfterForeach(result, generator, targetIndex, "giveall", jumpCode);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex, "giveall");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if ((i >= 46 && i < 56) || (i >= 118 && i < 128))
                    {
                        code = result[i];
                        if (i == 118)
                            Interface.Oxide.LogInfo("...");
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - giveall**********");
            }

            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveBp(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - giveBp**********");

            int chatIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (_debug && i >= 107 && i < 117)
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");

                if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                    chatIndex = i;
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex, "giveBp");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if (i >= 107 && i < 117)
                    {
                        code = result[i];
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - giveBp**********");
            }

            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_CopyTo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (_debug)
                Interface.Oxide.LogError("**********START - copyTo**********");

            int targetIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********Before**********");
                Interface.Oxide.LogInfo("...");
            }
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (_debug && i >= 192 && i < 202)
                    Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");

                if (code.opcode == OpCodes.Endfinally)
                    targetIndex = i;
            }
            if (_debug)
            {
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
            }

            if (targetIndex >= 0)
                Patch_AfterForeach(result, generator, targetIndex, "copyTo");

            if (_debug)
            {
                Interface.Oxide.LogWarning("**********After**********");
                Interface.Oxide.LogInfo("...");
                for (int i = 0; i < result.Count; i++)
                {
                    if (i >= 192 && i < 202)
                    {
                        code = result[i];
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    }
                }
                Interface.Oxide.LogInfo("...");
                code = result[^1];
                Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");

                Interface.Oxide.LogError("**********END - copyTo**********");
            }

            return result;
        }
        
        private static void Patch_ChatMessage(List<CodeInstruction> result, ILGenerator generator, int index, string methodName, CodeInstruction jumpCode = null)
        {
            if (_debug)
                Interface.Oxide.LogWarning($"Found the index of the last 'Log' method at {index}, starting to patch '{methodName}' method...");
            
            if (jumpCode == null)
                jumpCode = result[index + 4];
            if (jumpCode.labels == null)
                jumpCode.labels = new List<Label>();
            var jumpLabel = generator.DefineLabel();
            jumpCode.labels.Add(jumpLabel);

            result.Insert(index + 1, GetSilentGiveInstruction());
            result.Insert(index + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
            
            if (_debug)
                Interface.Oxide.LogWarning($"Successfully finished patching '{methodName}' method.");
        }
        
        private static void Patch_AfterForeach(List<CodeInstruction> result, ILGenerator generator, int index, string methodName, CodeInstruction jumpCode = null)
        {
            if (_debug)
                Interface.Oxide.LogWarning($"Found the index of the last 'Endfinally' method at {index}, starting to patch '{methodName}' method...");
            
            if (jumpCode == null)
                jumpCode = result[index + 4];
            if (jumpCode.labels == null)
                jumpCode.labels = new List<Label>();
            var jumpLabel = generator.DefineLabel();
            jumpCode.labels.Add(jumpLabel);

            result.Insert(index, GetSilentGiveInstruction());
            result.Insert(index + 1, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));

            if (_debug)
                Interface.Oxide.LogWarning($"Successfully finished patching '{methodName}' method.");
        }
        
        private static void Patch_SetItemOwnership(List<CodeInstruction> result, ILGenerator generator, int index, string methodName)
        {
            if (_debug)
                Interface.Oxide.LogWarning($"Found the index of the last 'SetItemOwnership' method at {index}, starting to patch '{methodName}' method...");

            var jumpCode = result[index + 2];
            if (jumpCode.labels == null)
                jumpCode.labels = new List<Label>();
            var jumpLabel = generator.DefineLabel();
            jumpCode.labels.Add(jumpLabel);

            result.Insert(index - 3, GetSilentGiveInstruction());
            result.Insert(index - 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));

            if (_debug)
                Interface.Oxide.LogWarning($"Successfully finished patching '{methodName}' method.");
        }
        
        private static CodeInstruction GetSilentGiveInstruction() => new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(SilentItemsGive), nameof(_silentGive)));
        #endregion
    }
}