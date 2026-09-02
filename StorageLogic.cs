using System;
using System.Linq;
using System.Collections.Generic;

namespace BalaurBohemianBroken {
    public class StorageLogic {
        public static bool store_in_containers_first;
        private static List<StorageComparison> storage_comparison_stack = new List<StorageComparison>();
        public static bool compare_storage_type;
        public static bool compare_storage_reduction;
        public static bool compare_storage_full;
        public static bool compare_storage_capacity;
        public static bool compare_storage_best_condition;

        // This is terrible form, having a side effect like this. But that's modding.
        public static bool notify_where_stored;
        public static HashSet<string> storing_in = new HashSet<string>();

        public static List<Setting> settings = new List<Setting>() {
            new SettingBool {
                name = "notify_where_stored",
                value = true,
                apply = delegate {
                    notify_where_stored =
                        Settings.Get<SettingBool>("notify_where_stored").value;
                },
                category = Setting.SettingCategory.Game
            },
            new SettingBool {
                name = "store_in_containers_first",
                value = true,
                apply = delegate {
                    store_in_containers_first =
                        Settings.Get<SettingBool>("store_in_containers_first").value;
                },
                category = Setting.SettingCategory.Game
            },
            new SettingBool {
                name = "compare_storage_type",
                value = true,
                apply = delegate {
                    compare_storage_type =
                        Settings.Get<SettingBool>("compare_storage_type").value;
                    CreateStorageComparisonStack();
                },
                category = Setting.SettingCategory.Game
            },
            new SettingBool {
                name = "compare_storage_reduction",
                value = true,
                apply = delegate {
                    compare_storage_reduction =
                        Settings.Get<SettingBool>("compare_storage_reduction").value;
                    CreateStorageComparisonStack();
                },
                category = Setting.SettingCategory.Game
            },
            new SettingBool {
                name = "compare_storage_full",
                value = true,
                apply = delegate {
                    compare_storage_full =
                        Settings.Get<SettingBool>("compare_storage_full").value;
                    CreateStorageComparisonStack();
                },
                category = Setting.SettingCategory.Game
            },
            new SettingBool {
                name = "compare_storage_capacity",
                value = true,
                apply = delegate {
                    compare_storage_capacity =
                        Settings.Get<SettingBool>("compare_storage_capacity").value;
                    CreateStorageComparisonStack();
                },
                category = Setting.SettingCategory.Game
            },
            new SettingBool {
                name = "compare_storage_best_condition",
                value = true,
                apply = delegate {
                    compare_storage_best_condition =
                        Settings.Get<SettingBool>("compare_storage_best_condition").value;
                    CreateStorageComparisonStack();
                },
                category = Setting.SettingCategory.Game
            },
        };

        public static void AutoPickup(Item item) {
            string stored_in = AddItemToInventory(item);
            if (stored_in == null)
                stored_in = "the floor";
            storing_in.Add(stored_in);
        }
        
        public static string AddItemToInventory(Item item) {
            // Return value of this is a bit gross. Because inventory slots don't share an interface with containers,
            // I can't return a unified object. I hope this won't bite me in the future.
            // Based on Body.AutoPickUpItem
            
            // Try hold item.
            if (!store_in_containers_first && TryHoldItem(item) != -1) {
                return "inventory";
            }

            // Try store item.
            Container stored_in = TryStoreItem(item); 
            if (stored_in != null)
                return BetterInventoryLogic.NameOrUnrecognized(stored_in.mItem);
            
            if (TryHoldItem(item) != -1) {
                return "inventory";
            }

            return null;
        }

        public static int TryHoldItem(Item item) {
            // Returns slot ID it went into.
            Body p = PlayerCamera.main.body;
            var slot = p.FirstEmptySlot();
            if (slot != null) {
                p.PickUpItem(item, slot.Value, true);
                return slot.Value;
            }

            return -1;
        }

        public static Container TryStoreItem(Item item) {
            Body p = PlayerCamera.main.body;
            List<Container> candidates = new List<Container>();
            foreach (Item surfaceInventoryItem in p.GetSurfaceInventoryItems()) {
                Container c = surfaceInventoryItem.container; 
                if (surfaceInventoryItem.container == null)
                    continue;
                if (!c.CanHoldItem(item))
                    continue;
                candidates.Add(c);
            }
            
            if (candidates.Any()) {
                candidates.Sort((x, y) => CompareContainerDesirability(x, y, item));
                Container container = candidates.Last();
                container.LoadItem(item);
                return container;
            }

            return null;
        }

        public static void InformWhereStored() {
            string stored_in = String.Join(", ", storing_in);
            PlayerCamera.main.DoAlert($"Stored: {stored_in}");
        }
        
        public static bool IsItemOnBody(Item item) {
            // I don't know for sure that this fully checks that.
            // This will almost certainly cause an obscure bug, like with floating items.
            // Based on looking at Body.PickUpItem
            // This explicitly exludes wearables. I'm not checking if it's worn.
            return !item.Stats.wearable && !item.rb.simulated && item.ParentContainer() == null;
        }

        public static bool IsItemOnFloor(Item item) {
            // This isn't a great check, but I think it should work well enough.
            return item.ParentContainer() == null && !IsItemOnBody(item);
        }
        
        public static void CreateStorageComparisonStack() {
            storage_comparison_stack = new List<StorageComparison>();

            if (compare_storage_type) {
                storage_comparison_stack.Add(new StorageCompareType());
            }
            if (compare_storage_reduction) {
                storage_comparison_stack.Add(new StorageCompareWeightReduction());
            }
            if (compare_storage_full) {
                storage_comparison_stack.Add(new StorageCompareMostFull());
            }
            if (compare_storage_capacity) {
                storage_comparison_stack.Add(new StorageCompareLargest());
            }
            if (compare_storage_best_condition) {
                storage_comparison_stack.Add(new StorageCompareBestCondition());
            }
        }
        
        public static int CompareContainerDesirability(Container x, Container y, Item item) {
            foreach (StorageComparison comparison in storage_comparison_stack) {
                int comp_result = comparison.Compare(x, y, item);
                if (comp_result != 0)
                    return comp_result;
            }
            return 0;
        }
    }
    
    #region Storage comparison classes
    public abstract class StorageComparison {
        public abstract int Compare(Container x, Container y, Item item);
    }

    public class StorageCompareType : StorageComparison {
        public override int Compare(Container x, Container y, Item item) {
            bool x_has_same_restriction = false, y_has_same_restriction = false;
            if (x.tagRestriction.Length > 0)
                x_has_same_restriction = x.tagRestriction.Intersect(item.Stats.GetTags()).Any();
            if (y.tagRestriction.Length > 0)
                y_has_same_restriction = y.tagRestriction.Intersect(item.Stats.GetTags()).Any();

            if (x_has_same_restriction == y_has_same_restriction)
                return 0;
            if (x_has_same_restriction)
                return 1;
            return -1;
        }
    }

    public class StorageCompareWeightReduction : StorageComparison {
        public override int Compare(Container x, Container y, Item item) {
            return x.encumberanceMult.CompareTo(y.encumberanceMult) * -1;
        }
    }

    public class StorageCompareMostFull : StorageComparison {
        public override int Compare(Container x, Container y, Item item) {
            float x_space_left = x.maxWeight - x.GetHoldingWeight();
            float y_space_left = y.maxWeight - y.GetHoldingWeight();
            return x_space_left.CompareTo(y_space_left);
        }
    }

    public class StorageCompareLargest : StorageComparison {
        public override int Compare(Container x, Container y, Item item) {
            return x.maxWeight.CompareTo(y.maxWeight);
        }
    }
    
    // TODO: Maybe maybe this a generic item comparison.
    public class StorageCompareBestCondition : StorageComparison {
        public override int Compare(Container x, Container y, Item item) {
            return x.mItem.condition.CompareTo(y.mItem.condition);
        }
    }
    #endregion
}