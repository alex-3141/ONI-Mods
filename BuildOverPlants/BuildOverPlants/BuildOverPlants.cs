using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;

/* NOTE: This mod's functionality is now part of the base game as of the
 *       release of The Prehistoric Planet Pack DLC.
 *       No further development of this mod will be neccesary.
 *       This mod will disable itself if it detects the new update is
 *       active to prevent a crash.
 */


namespace BuildOverPlants
{
	public class BuildOverPlants : KMod.UserMod2
	{
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("[Build Over Plants] Loading...");

            if (IsNewVersion())
            {
                Debug.Log("[Build Over Plants]: New game version detected. This mod has been disabled.");
                Debug.Log("[Build Over Plants]: Building over plants has been added to the bast game, and this mod should be uninstalled.");
                return;
            }

            base.OnLoad(harmony);

            Debug.Log("[Build Over Plants] Loaded.");
        }

        static bool IsNewVersion()
        {
			return AccessTools.Method(typeof(RoomProber), "RebuildDirtyCavities", Type.EmptyTypes) != null;
        }

        // Patch BuildingDef.IsAreaClear to allow placement of buildings over plants
        [HarmonyPatch]
		public static class BuildOverPlants_Patch_BuildingDef_IsAreaClear
		{
			// Find target method at runtime due to new overload of IsAreaClear() being introduced in hotfix 597172
			static public MethodBase TargetMethod()
			{
				var targetType = typeof(BuildingDef);
				var parameterTypesOld = new Type[]
				{
					typeof(GameObject), typeof(int), typeof(Orientation),
					typeof(ObjectLayer), typeof(ObjectLayer),
					typeof(bool), typeof(string).MakeByRefType()
				};
				var parameterTypesNew = new Type[]
				{
					typeof(GameObject), typeof(int), typeof(Orientation),
					typeof(ObjectLayer), typeof(ObjectLayer), typeof(bool),
					typeof(bool), typeof(string).MakeByRefType()
				};
				// Maintain backwards compatability with older game versions
				var targetMethod = AccessTools.Method(targetType, "IsAreaClear", parameterTypesNew);
				if (targetMethod == null) {
						targetMethod = AccessTools.Method(targetType, "IsAreaClear", parameterTypesOld);
				}
				return targetMethod;
			}

			public static void Prefix(ref BuildingDef __instance, ref int cell, ref Orientation orientation, ref ObjectLayer layer)
			{
			for (int i = 0; i < __instance.PlacementOffsets.Length; i++) {
				CellOffset offset = __instance.PlacementOffsets [i];
				CellOffset rotatedCellOffset = Rotatable.GetRotatedCellOffset (offset, orientation);
				int num = Grid.OffsetCell (cell, rotatedCellOffset);
				GameObject gameObject = Grid.Objects [num, (int)layer];
				if (gameObject != null) { 
					if (gameObject.GetComponent<Uprootable> () != null) {
						// Here I move the plant object to the Plants object layer (It's on the building layer by default)
						// This allows the building to be placed without overwriting the plant object and without the plant getting in the way
						Grid.Objects [num, (int)ObjectLayer.Plants] = gameObject;
						Grid.Objects [num, (int)ObjectLayer.Building] = null;
					}
				}
			}
		}
	}

        // Patch RoomProber.RebuildDirtyCavities to allow plants on the plant layer to be detected by parks/nature reserves.
        [HarmonyPatch(typeof(RoomProber))]
        [HarmonyPatch("RebuildDirtyCavities")]
        public static class BuildOverPlants_Patch_RoomProber_RebuildDirtyCavities
        {
            public static void Prefix(ICollection<int> visited_cells, KCompactedVector<CavityInfo> ___cavityInfos, HandleVector<int>.Handle[] ___CellCavityID)
            {
                int maxRoomSize = TuningData<RoomProber.Tuning>.Get().maxRoomSize;
                foreach (int current in visited_cells)
                {
                    HandleVector<int>.Handle handle = ___CellCavityID[current];
                    if (handle.IsValid())
                    {
                        CavityInfo data = ___cavityInfos.GetData(handle);
                        if (0 < data.numCells && data.numCells <= maxRoomSize)
                        {
                            GameObject gameObject = Grid.Objects[current, (int)ObjectLayer.Plants];
                            if (gameObject != null)
                            {
                                KPrefabID component = gameObject.GetComponent<KPrefabID>();
                                bool flag2 = false;
                                foreach (KPrefabID current2 in data.buildings)
                                {
                                    if (component.InstanceID == current2.InstanceID)
                                    {
                                        flag2 = true;
                                        break;
                                    }
                                }
                                foreach (KPrefabID current3 in data.plants)
                                {
                                    if (component.InstanceID == current3.InstanceID)
                                    {
                                        flag2 = true;
                                        break;
                                    }
                                }
                                if (!flag2)
                                {
                                    if (component.GetComponent<Deconstructable>())
                                    {
                                        data.AddBuilding(component);
                                    }
                                    else
                                    {
                                        if (component.HasTag(GameTags.Plant) && !component.HasTag("ForestTreeBranch".ToTag()))
                                        {
                                            data.AddPlants(component);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Patch Constructable.PlaceDiggables to mark plants for uprooting when they get in the way
        [HarmonyPatch(typeof(Constructable))]
		[HarmonyPatch("PlaceDiggables")]
		public static class BuildOverPlants_Patch_Constructable_PlaceDiggables
		{
			public static void Postfix(ref Constructable __instance, ref Building ___building)
			{
				// Check if the building is not a background object
				if (___building.Def.ObjectLayer != ObjectLayer.Building) {
					return;
				}
				PrioritySetting masterPriority = __instance.GetComponent<Prioritizable> ().GetMasterPriority ();
				___building.RunOnArea (delegate (int offset_cell) {
					GameObject gameObject = Grid.Objects [offset_cell, (int)ObjectLayer.Plants];
					if (gameObject != null) {
						// This component should always exist but lets check it just in case
						if (gameObject.GetComponent<Uprootable> () != null) {
							gameObject.GetComponent<Uprootable> ().MarkForUproot (true);
							gameObject.GetComponent<Prioritizable> ().SetMasterPriority (masterPriority);

						}
					}
				});
			}
		}
	}
}
