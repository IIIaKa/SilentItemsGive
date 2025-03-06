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
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Silent Items Give", "IIIaKa", "0.1.0")]
    [Description("Toggling broadcast messages to all players when issuing in-game items via Harmony patching.")]
    class SilentItemsGive : RustPlugin
    {
        #region ~Variables~
        private Harmony _harmony;
        private static bool _debug = false, _silentGive = true;
        private const string PERMISSION_ADMIN = "silentitemsgive.admin", IdForHarmony = "iiiaka.silentitemsgive";
        #endregion
        
        #region ~Oxide Hooks~
        void Init()
        {
            _harmony = new Harmony(IdForHarmony);
            _harmony.PatchAll();
            Puts($"Patch '{IdForHarmony}' by '{Author}' has been successfully applied!");
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            AddCovalenceCommand("silentgive.toggle", nameof(Command_Toggle));
            if (Interface.Oxide.DataFileSystem.ReadObject<object>(Name) is bool savedVal)
                _silentGive = savedVal;
            else
                _silentGive = true;
        }
        
        void Unload()
        {
            _harmony.UnpatchAll(IdForHarmony);
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
        [HarmonyPatch(typeof(ConVar.Inventory), "give", new Type[] { typeof(ConsoleSystem.Arg) })]
        public static class GivePatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (_debug)
                    Interface.Oxide.LogError("**********START - give**********");
                
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
                    if (_debug && i >= 198 && i < 208)
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    
                    if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                        targetIndex = i;
                }
                if (_debug)
                {
                    Interface.Oxide.LogInfo("...");
                    code = result[^1];
                    Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
                }
                
                if (targetIndex >= 0)
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Log' method at {targetIndex}, starting to patch 'give' method...");
                    
                    var jumpCode = result[targetIndex + 4];
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex + 1, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'give' method.");
                }

                if (_debug)
                {
                    Interface.Oxide.LogWarning("**********After**********");
                    Interface.Oxide.LogInfo("...");
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (i >= 198 && i < 208)
                        {
                            code = result[i];
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
        }
        
        [HarmonyPatch(typeof(ConVar.Inventory), "giveid", new Type[] { typeof(ConsoleSystem.Arg) })]
        public static class GiveIdPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (_debug)
                    Interface.Oxide.LogError("**********START - giveid**********");
                
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
                    if (_debug && i >= 94 && i < 104)
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    
                    if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                        targetIndex = i;
                }
                if (_debug)
                {
                    Interface.Oxide.LogInfo("...");
                    code = result[^1];
                    Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
                }
                
                if (targetIndex >= 0)
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Log' method at {targetIndex}, starting to patch 'giveid' method...");
                    
                    var jumpCode = result[targetIndex + 4];
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex + 1, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'giveid' method.");
                }
                
                if (_debug)
                {
                    Interface.Oxide.LogWarning("**********After**********");
                    Interface.Oxide.LogInfo("...");
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (i >= 94 && i < 104)
                        {
                            code = result[i];
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
        }
        
        [HarmonyPatch(typeof(ConVar.Inventory), "givearm", new Type[] { typeof(ConsoleSystem.Arg) })]
        public static class GiveArmPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (_debug)
                    Interface.Oxide.LogError("**********START - givearm**********");

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
                    if (_debug && i >= 97 && i < 107)
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    
                    if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                        targetIndex = i;
                }
                if (_debug)
                {
                    Interface.Oxide.LogInfo("...");
                    code = result[^1];
                    Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
                }
                
                if (targetIndex >= 0)
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Log' method at {targetIndex}, starting to patch 'givearm' method...");
                    
                    var jumpCode = result[targetIndex + 4];
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex + 1, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'givearm' method.");
                }

                if (_debug)
                {
                    Interface.Oxide.LogWarning("**********After**********");
                    Interface.Oxide.LogInfo("...");
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (i >= 97 && i < 107)
                        {
                            code = result[i];
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
        }
        
        [HarmonyPatch(typeof(ConVar.Inventory), "giveto", new Type[] { typeof(ConsoleSystem.Arg) })]
        public static class GiveToPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (_debug)
                    Interface.Oxide.LogError("**********START - giveto**********");
                
                int targetIndex = -1;
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
                    if (_debug && i >= 115 && i < 125)
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    
                    if (code.opcode == OpCodes.Ret)
                        jumpCode = code;
                    else if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                        targetIndex = i;
                }
                if (_debug)
                {
                    Interface.Oxide.LogInfo("...");
                    code = result[^1];
                    Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
                }
                
                if (targetIndex >= 0 && jumpCode != null)
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Log' method at {targetIndex}, starting to patch 'giveto' method...");
                    
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex + 1, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'giveto' method.");
                }
                
                if (_debug)
                {
                    Interface.Oxide.LogWarning("**********After**********");
                    Interface.Oxide.LogInfo("...");
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (i >= 115 && i < 125)
                        {
                            code = result[i];
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
        }
        
        [HarmonyPatch(typeof(ConVar.Inventory), "giveall", new Type[] { typeof(ConsoleSystem.Arg) })]
        public static class GiveAllPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (_debug)
                    Interface.Oxide.LogError("**********START - giveall**********");
                
                int targetIndex = -1;
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
                    if (_debug && i >= 116 && i < 126)
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    
                    if (code.opcode == OpCodes.Ret)
                        jumpCode = code;
                    else if (code.opcode == OpCodes.Endfinally)
                        targetIndex = i;
                }
                if (_debug)
                {
                    Interface.Oxide.LogInfo("...");
                    code = result[^1];
                    Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
                }
                
                if (targetIndex >= 0 && jumpCode != null)
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Endfinally' at {targetIndex}, starting to patch the 'giveall' method...");
                    
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 1, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'giveall' method.");
                }
                
                if (_debug)
                {
                    Interface.Oxide.LogWarning("**********After**********");
                    Interface.Oxide.LogInfo("...");
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (i >= 116 && i < 126)
                        {
                            code = result[i];
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
        
        }
        
        [HarmonyPatch(typeof(ConVar.Inventory), "giveBp", new Type[] { typeof(ConsoleSystem.Arg) })]
        public static class GiveBpPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (_debug)
                    Interface.Oxide.LogError("**********START - giveBp**********");

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
                    if (_debug && i >= 107 && i < 117)
                        Interface.Oxide.LogInfo($"Code[{i}]: {code.opcode} {code.operand}");
                    
                    if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                        targetIndex = i;
                }
                if (_debug)
                {
                    Interface.Oxide.LogInfo("...");
                    code = result[^1];
                    Interface.Oxide.LogInfo($"Code[{result.Count - 1}]: {code.opcode} {code.operand}");
                }
                
                if (targetIndex >= 0)
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Log' method at {targetIndex}, starting to patch 'giveBp' method...");
                    
                    var jumpCode = result[targetIndex + 4];
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex + 1, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'giveBp' method.");
                }

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
        }
        
        [HarmonyPatch(typeof(ConVar.Inventory), "copyTo", new Type[] { typeof(BasePlayer), typeof(BasePlayer) })]
        public static class GiveCopyToPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
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
                {
                    if (_debug)
                        Interface.Oxide.LogWarning($"Found the index of the last 'Endfinally' at {targetIndex}, starting to patch the 'copyTo' method...");
                    
                    var jumpCode = result[targetIndex + 4];
                    if (jumpCode.labels == null)
                        jumpCode.labels = new List<Label>();
                    var jumpLabel = generator.DefineLabel();
                    jumpCode.labels.Add(jumpLabel);
                    
                    result.Insert(targetIndex, GetSilentGiveInstruction());
                    result.Insert(targetIndex + 1, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
                    
                    if (_debug)
                        Interface.Oxide.LogWarning("Successfully finished patching 'copyTo' method.");
                }

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
        }
        
        private static CodeInstruction GetSilentGiveInstruction() => new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(SilentItemsGive), nameof(_silentGive)));
        #endregion
    }
}