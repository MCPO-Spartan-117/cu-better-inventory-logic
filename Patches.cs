using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace BalaurBohemianBroken {
    [HarmonyPatch(typeof(Recipe))]
    [HarmonyPatch(nameof(Recipe.GetItemsForRecipe))]
    public class Patch_GetItemsForRecipe {
        public static bool Prefix(Recipe __instance, ref List<Item> __result) {
            __result = CraftingLogic.GetItemsForRecipe(__instance, true);
            return false;
        }
    }

    [HarmonyPatch(typeof(Recipe))]
    [HarmonyPatch(nameof(Recipe.GetItemsForRecipeThorough))]
    public class Patch_GetItemsForRecipeThorough {
        public static bool Prefix(Recipe __instance, ref List<Item> __result) {
            __result = CraftingLogic.GetItemsForRecipe(__instance, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(Locale))]
    [HarmonyPatch(nameof(Locale.LoadLanguage))]
    public class Patch_LoadLanguage {
        public static void Postfix() {
            Dictionary<string, string> lines = new Dictionary<string, string> {
                #region Crafting settings
                {"gamesetprefer_on_floor", "<color=purple>Crafting</color>: Prefer on floor"},
                {"gamesetprefer_on_floordsc", "Use materials on the floor before things in your inventory."},
                {"gamesetprefer_not_container", "<color=purple>Crafting</color>: Prefer not containers"},
                {"gamesetprefer_not_containerdsc", "Use containers, like bags, later."},
                {"gamesetprefer_not_wearable", "<color=purple>Crafting</color>: Prefer not wearables"},
                {"gamesetprefer_not_wearabledsc", "Use wearables, like clothing, later."},
                {"gamesetprefer_on_body", "<color=purple>Crafting</color>: Prefer held"},
                {"gamesetprefer_on_bodydsc", "Use materials in your hands/mouth/back before things in your inventory."},
                {"gamesetprefer_high_quality", "<color=purple>Crafting</color>: Prefer higher quality"},
                {"gamesetprefer_high_qualitydsc", "Use items of lower quality, like lower hammering, later."},
                {"gamesetprefer_low_value", "<color=purple>Crafting</color>: Prefer lower value"},
                {"gamesetprefer_low_valuedsc", "Use items of higher trading value later."},
                {"gamesetprefer_low_condition", "<color=purple>Crafting</color>: Prefer lower condition"},
                {"gamesetprefer_low_conditiondsc", "Use items with higher condition later."},
                #endregion
                #region Autopickup rules
                {"gamesetliquids_can_mix", "<color=purple>Storage (Liquid)</color>: Allow stacking into mixed"},
                {"gamesetliquids_can_mixdsc", "Allow a pouring this liquid into a bottle that has other liquids, only if it already has some of this liquid."},
                {"gamesetliquids_can_fill_new_bottles", "<color=purple>Storage (Liquid)</color>: Allow filling new bottles"},
                {"gamesetliquids_can_fill_new_bottlesdsc", "Allow liquids to fill bottles that are empty."},

                {"gamesetcompare_liquid_unmixed_enabled", "<color=purple>Storage (Liquid)</color>: Prefer liquids unmixed"},
                {"gamesetcompare_liquid_unmixed_enableddsc", "If 'Allow liquids to mix' is enabled, fill containers with the fewest different liquids."},
                {"gamesetcompare_liquid_stacking_enabled", "<color=purple>Storage (Liquid)</color>: Prefer liquid stack"},
                {"gamesetcompare_liquid_stacking_enableddsc", "Fill containers that have the highest amount of this liquid first."},
                {"gamesetcompare_liquid_weight_enabled", "<color=purple>Storage (Liquid)</color>: Prefer low weight liquid containers"},
                {"gamesetcompare_liquid_weight_enableddsc", "Prefer containers that have the lowest weight to highest liquid ratio when full."},
                
                {"gamesetnotify_where_stored", "<color=purple>Storage</color>: Notify where stored"},
                {"gamesetnotify_where_storeddsc", "Create a popup that specifies where the output of crafting recipes was placed."},
                {"gamesetstore_in_containers_first", "<color=purple>Storage</color>: Store in containers first"},
                {"gamesetstore_in_containers_firstdsc", "Put crafted items into containers first, rather than in hands/mouth/back."},

                {"gamesetcompare_storage_type", "<color=purple>Storage</color>: Prefer matched container type"},
                {"gamesetcompare_storage_typedsc", "When storing crafted items, choose containers specific to this item type, such as the material pouch for materials."},
                {"gamesetcompare_storage_reduction", "<color=purple>Storage</color>: Prefer higher container reduction"},
                {"gamesetcompare_storage_reductiondsc", "When storing crafted items, prefer containers that have a better encumbrance reduction."},
                {"gamesetcompare_storage_full", "<color=purple>Storage</color>: Prefer fuller container"},
                {"gamesetcompare_storage_fulldsc", "When storing crafted items, prefer containers that are more full."},
                {"gamesetcompare_storage_capacity", "<color=purple>Storage</color>: Prefer larger containers"},
                {"gamesetcompare_storage_capacitydsc", "When storing crafted items, prefer containers that are larger."},
                {"gamesetcompare_storage_best_condition", "<color=purple>Storage</color>: Prefer best condition container"},
                {"gamesetcompare_storage_best_conditiondsc", "When storing crafted items, prefer containers that have higher condition."},
                #endregion
            };

            foreach (KeyValuePair<string, string> line in lines) {
                Locale.currentLang.other.Add(line.Key, line.Value);
            } 
        }
    }

    [HarmonyPatch(typeof(Settings))]
    [HarmonyPatch(nameof(Settings.DefaultSettings))]
    public class Patch_DefaultSettings {
        public static void Postfix(List<Setting> __result) {
            __result.AddRange(CraftingLogic.settings);
            __result.AddRange(StorageLogic.settings);
            __result.AddRange(LiquidStorageLogic.settings);
        }
    }

    [HarmonyPatch]
    public class Patch_AutoPickUpItem {
//        public static bool AutoPickup(Item item, Body character) {
//                return false;
//            if (!item.Stats.wearable)
//            {
//                StorageLogic.AutoPickup(item);
//            }
//            else
//            {

        //Reverse patch to keep method signature
        [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
        [HarmonyPatch(typeof(Body))]
        [HarmonyPatch(nameof(Body.AutoPickUpItem))]
        //Replace middle part with a function call to StorageLogic.AutoPickup
        public void rev_AutoPickUpItem(Item item) {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                if(instructions == null) { return instructions; };
                var startidx = -1;
                var endidx = -1;
                var methodidx = -1;
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (var i = 0; i < codes.Count; i++) {
                    if (methodidx == -1) {
                        if (codes[i].IsLdarg()) {
                            startidx = i;
                        } else if (codes[i].Calls(AccessTools.Method(typeof(Body), nameof(Body.FirstEmptySlot)))) {
                            methodidx = i;
                        }
                    } else {
                        if (codes[i].opcode == OpCodes.Endfinally) {
                            endidx = i;
                            break;
                        }
                    }
                }

                if (endidx != -1) {
                    //C# version doesn't support array declarations + assignment
                    System.Type[] paramtypes = new System.Type[1];
                    paramtypes[0] = typeof(Item);

                    List<CodeInstruction> callfunct = new List<CodeInstruction>();
                    callfunct.Add(new CodeInstruction(OpCodes.Ldarg_1));
                    callfunct.Add(CodeInstruction.Call(typeof(StorageLogic), nameof(StorageLogic.AutoPickup), paramtypes));
                    callfunct.Add(new CodeInstruction(OpCodes.Ret));

                    codes.RemoveRange(startidx, endidx - startidx + 1);
                    codes.InsertRange(startidx, (IEnumerable<CodeInstruction>)callfunct);
                }

                return (IEnumerable<CodeInstruction>)codes;
            }
            //suppress unused
            _ = Transpiler(null);
        }
    }

    public class ManualPatch_SpawnResult {
        public static IEnumerable<CodeInstruction> Replace_AutoPickfunct(IEnumerable<CodeInstruction> instructions) {
            //IEnumerable<CodeInstruction> MethodReplacer doesn't seem to work for some reason
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            List<int> methodidxs = new List<int>();

            for(var i = 0; i < codes.Count; i++) {
                if(codes[i].Calls(AccessTools.Method(typeof(Body), nameof(Body.AutoPickUpItem)))) {
                    methodidxs.Add(i);
                }
            }

            //C# version doesn't support array declarations + assignment
            System.Type[] functtypes = new System.Type[1];
            functtypes[0] = typeof(Item);
            CodeInstruction callfunct = CodeInstruction.Call(typeof(Patch_AutoPickUpItem), nameof(Patch_AutoPickUpItem.rev_AutoPickUpItem), functtypes);
            for(var i = 0; i < methodidxs.Count; i++) {
                codes.RemoveAt(methodidxs[i]);
                codes.Insert(methodidxs[i], callfunct);
            }
            return (IEnumerable<CodeInstruction>)codes;
        }
    }

    [HarmonyPatch(typeof(RecipeResult))]
    [HarmonyPatch(nameof(RecipeResult.SpawnResult))]
    public class Patch_SpawnResult {
        //Replace flag if statement with LiquidStorageLogic.StoreLiquid function
        //Decompiled code from v1.0.5
//        {
//            string id = __instance.id;
//            float amount = __instance.resultCondition * num2;
//            if (!LiquidStorageLogic.StoreLiquid(__instance.id, __instance.resultCondition * num2))
//            {
//                GameObject gameObject = Utils.Create("craftingbottle", PlayerCamera.main.body.transform.position, 0f);
//                Item component = gameObject.GetComponent<Item>();
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var ifstartidx = -1;
            var startidx = -1;
            var endidx = -1;
            var stage = 0;
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++) {
                switch(stage) {
                    case 0:
                        if(codes[i].opcode == OpCodes.Ldc_I4_0) {
                            ifstartidx = i;
                        //Is Operand matching broken?
                        } else if(ifstartidx != -1 && codes[i].ToString() == "stloc.s 5 (System.Boolean)") {
                            stage = 1;
                        }
                    break;
                    case 1:
                        if (codes[i].LoadsField(AccessTools.Field(typeof(PlayerCamera), nameof(PlayerCamera.main)))) {
                            startidx = i;
                        } else if (codes[i].Calls(AccessTools.Method(typeof(Body), nameof(Body.GetAllItemsThorough)))) {
                            stage = 2;
                        }
                    break;
                    case 2:
                        if (codes[i].opcode == OpCodes.Endfinally) {
                            endidx = i;
                            stage = 3;
                        }
                    break;
                    default:
                        i = codes.Count;
                    break;
                }
            }

            if (endidx != -1) {
                codes.RemoveRange(startidx, endidx - startidx + 1);
                codes.RemoveAt(ifstartidx);

                System.Type[] liqtype = new System.Type[2];
                liqtype[0] = typeof(string);
                liqtype[1] = typeof(float);

                List<CodeInstruction> ifstatement = new List<CodeInstruction>();
                ifstatement.Add(new CodeInstruction(OpCodes.Ldarg_0));
                ifstatement.Add(CodeInstruction.LoadField(typeof(RecipeResult), nameof(RecipeResult.id)));
                ifstatement.Add(new CodeInstruction(OpCodes.Ldarg_0));
                ifstatement.Add(CodeInstruction.LoadField(typeof(RecipeResult), nameof(RecipeResult.resultCondition)));
                ifstatement.Add(new CodeInstruction(OpCodes.Ldloc_1));
                ifstatement.Add(new CodeInstruction(OpCodes.Mul));
                ifstatement.Add(CodeInstruction.Call(typeof(LiquidStorageLogic), nameof(LiquidStorageLogic.StoreLiquid), liqtype));

                codes.InsertRange(ifstartidx, ifstatement);
            }

            return (IEnumerable<CodeInstruction>)codes;
        }

        public static void Postfix(int recipeInt, RecipeResult __instance) {
            if (StorageLogic.notify_where_stored) {
                StorageLogic.InformWhereStored();
            }
            StorageLogic.storing_in =  new HashSet<string>();
        }
    }
}