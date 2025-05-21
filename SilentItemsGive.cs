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

using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Silent Items Give", "IIIaKa", "0.1.1")]
    [Description("Allows toggling silent item giving(chat notifications and ownership) via Harmony patching.")]
    class SilentItemsGive : RustPlugin
    {
        #region ~Variables~
        private Harmony _harmony;
        private static bool _silentGive = true;
        private const string PERMISSION_ADMIN = "silentitemsgive.admin", IdForHarmony = "iiiaka.silentitemsgive";
        #endregion
        
        #region ~Oxide Hooks~
        void OnServerInitialized(bool initial)
        {
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            AddCovalenceCommand("silentgive.toggle", nameof(Toggle_Command));
            _silentGive = Interface.Oxide.DataFileSystem.ReadObject<object>(Name) is bool savedVal ? savedVal : true;
            
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
            
            void PatchMethod(Type targetType, string methodName, HarmonyMethod transpiler, Type[] parameters = null)
            {
                var targetMethod = parameters == null ? AccessTools.Method(targetType, methodName) : AccessTools.Method(targetType, methodName, parameters);
                if (targetMethod == null)
                    PrintError($"Failed to find method {targetType.Name}.{methodName}!");
                else
                    _harmony.Patch(targetMethod, transpiler: transpiler);
            }
        }

        void Unload()
        {
            _harmony?.UnpatchAll(IdForHarmony);
            Interface.Oxide.DataFileSystem.WriteObject(Name, _silentGive);
        }
        #endregion
        
        #region ~Commands~
        private void Toggle_Command(IPlayer player, string command, string[] args)
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
            CodeInstruction code;
            int chatIndex = -1, ownerIndex = -1;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (code.operand is not MethodInfo method) continue;
                if (code.opcode == OpCodes.Call && method.Name == "Log")
                    chatIndex = i;
                else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                    ownerIndex = i;
            }
            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex);
            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveId(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            CodeInstruction code;
            int chatIndex = -1, ownerIndex = -1;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (code.operand is not MethodInfo method) continue;
                if (code.opcode == OpCodes.Call && method.Name == "Log")
                    chatIndex = i;
                else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                    ownerIndex = i;
            }
            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex);
            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveArm(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            CodeInstruction code;
            int chatIndex = -1, ownerIndex = -1;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (code.operand is not MethodInfo method) continue;
                if (code.opcode == OpCodes.Call && method.Name == "Log")
                    chatIndex = i;
                else if (code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                    ownerIndex = i;
            }
            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex);
            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveTo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            int chatIndex = -1, ownerIndex = -1;
            CodeInstruction code, jumpCode = null;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
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
            if (chatIndex >= 0 && jumpCode != null)
                Patch_ChatMessage(result, generator, chatIndex, jumpCode);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex);
            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveAll(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            int targetIndex = -1, ownerIndex = -1;
            CodeInstruction code, jumpCode = null;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (code.opcode == OpCodes.Ret)
                    jumpCode = code;
                else if (code.opcode == OpCodes.Endfinally)
                    targetIndex = i;
                else if (code.operand is MethodInfo method && code.opcode == OpCodes.Callvirt && method.Name == "SetItemOwnership")
                    ownerIndex = i;
            }
            if (targetIndex >= 0 && jumpCode != null)
                Patch_AfterForeach(result, generator, targetIndex, jumpCode);
            if (ownerIndex >= 0)
                Patch_SetItemOwnership(result, generator, ownerIndex);
            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_GiveBp(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            int chatIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "Log")
                    chatIndex = i;
            }
            if (chatIndex >= 0)
                Patch_ChatMessage(result, generator, chatIndex);
            return result;
        }
        
        public static IEnumerable<CodeInstruction> Transpiler_CopyTo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            int targetIndex = -1;
            CodeInstruction code;
            var result = new List<CodeInstruction>(instructions);
            for (int i = 0; i < result.Count; i++)
            {
                code = result[i];
                if (code.opcode == OpCodes.Endfinally)
                    targetIndex = i;
            }
            if (targetIndex >= 0)
                Patch_AfterForeach(result, generator, targetIndex);
            return result;
        }
        
        private static void Patch_ChatMessage(List<CodeInstruction> result, ILGenerator generator, int index, CodeInstruction jumpCode = null)
        {
            if (jumpCode == null)
                jumpCode = result[index + 4];
            if (jumpCode.labels == null)
                jumpCode.labels = new List<Label>();
            var jumpLabel = generator.DefineLabel();
            jumpCode.labels.Add(jumpLabel);
            
            result.Insert(index + 1, GetSilentGiveInstruction());
            result.Insert(index + 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
        }
        
        private static void Patch_AfterForeach(List<CodeInstruction> result, ILGenerator generator, int index, CodeInstruction jumpCode = null)
        {
            if (jumpCode == null)
                jumpCode = result[index + 4];
            if (jumpCode.labels == null)
                jumpCode.labels = new List<Label>();
            var jumpLabel = generator.DefineLabel();
            jumpCode.labels.Add(jumpLabel);
            
            result.Insert(index, GetSilentGiveInstruction());
            result.Insert(index + 1, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
        }
        
        private static void Patch_SetItemOwnership(List<CodeInstruction> result, ILGenerator generator, int index)
        {
            var jumpCode = result[index + 2];
            if (jumpCode.labels == null)
                jumpCode.labels = new List<Label>();
            var jumpLabel = generator.DefineLabel();
            jumpCode.labels.Add(jumpLabel);
            
            result.Insert(index - 3, GetSilentGiveInstruction());
            result.Insert(index - 2, new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));
        }
        
        private static CodeInstruction GetSilentGiveInstruction() => new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(SilentItemsGive), nameof(_silentGive)));
        #endregion
    }
}