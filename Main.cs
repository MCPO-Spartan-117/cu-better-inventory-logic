using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Inventory ideas:
// Show container total weight, including contents.
// Better pickup rules.
// Autosort
// Extended item info, e.g. type tags, how long item has existed for

// TODO: Make crafting status update after properly crafting stuff.
// TODO: Click on an item in the crafting window to tell recipe to try to use something else.
// TODO: For liquids, I should choose to craft with mixed liquids first.
// TODO: Allow users to organize the stack themselves.
// TODO: Message that states where the crafted item was placed?
// TODO: Fill bottle, and move on to next bottle if there is any left over.
// TODO: Allow recipes to use filled containers.
// TODO: Show item condition in preview menu. Hover text for item.
// TODO: Craft multiple.
namespace BalaurBohemianBroken {
    [BepInPlugin("com.balaur.BetterLogic", "BetterInventoryLogic", "1.0.4")]
    public class BetterInventoryLogic : BaseUnityPlugin {
        public static BetterInventoryLogic instance;
        Harmony harmony;

        public void Awake() {
            instance = this;
            harmony = new Harmony("com.balaur.BetterLogic");
            harmony.PatchAll();
        }

        //Wait for all plugins to be loaded
        public void Start() {
            var original_reciperes = AccessTools.Method(typeof(RecipeResult), nameof(RecipeResult.SpawnResult));
            harmony.Patch(original_reciperes, transpiler: new HarmonyMethod(AccessTools.Method(typeof(ManualPatch_SpawnResult), nameof(ManualPatch_SpawnResult.Replace_AutoPickfunct))));

            //Replace funct in all prefixes
            var patches = Harmony.GetPatchInfo(original_reciperes);
            foreach(Patch p in patches.Prefixes) {
                harmony.Patch(p.PatchMethod, transpiler: new HarmonyMethod(AccessTools.Method(typeof(ManualPatch_SpawnResult), nameof(ManualPatch_SpawnResult.Replace_AutoPickfunct))));
            }

            //Remake original function with all patches
            MethodInfo original_autopickup = AccessTools.Method(typeof(Body), nameof(Body.AutoPickUpItem));
            MethodInfo rev_autopickup = AccessTools.Method(typeof(Patch_AutoPickUpItem), nameof(Patch_AutoPickUpItem.rev_AutoPickUpItem));
            var autopatches = Harmony.GetPatchInfo(original_autopickup);
            //Unfortunely there doesn't seem to be a multi-patch system so this spams the previous patches
            foreach(Patch p in autopatches.Prefixes) {
                harmony.Patch(rev_autopickup, prefix: new HarmonyMethod(p.GetMethod(original_autopickup)));
            }

            foreach(Patch p in autopatches.Postfixes) {
                harmony.Patch(rev_autopickup, postfix: new HarmonyMethod(p.GetMethod(original_autopickup)));
            }

            foreach(Patch p in autopatches.Finalizers) {
                harmony.Patch(rev_autopickup, finalizer: new HarmonyMethod(p.GetMethod(original_autopickup)));
            }
        }

        public static List<Item> GetAvailableItems(bool include_pickups) {
            List<Item> all_items = PlayerCamera.main.body.GetAllItemsThorough();

            if (!include_pickups)
                return all_items;
            // Find items. Taken and cleaned from decomp.
            Vector2 position = PlayerCamera.main.body.transform.position;
            int mask = LayerMask.GetMask("Item");
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(position, 10f, mask)) {
                if (collider.TryGetComponent<Item>(out Item component) && PlayerCamera.main.body.DoPickupCheck(component, true))
                    all_items.Add(component);
            }

            return all_items;
        }

        public static string NameOrUnrecognized(Item item) {
            return item.Stats.rec.recognizable ? item.fullName : Locale.GetOther("unknownobject");
        }
    }
}