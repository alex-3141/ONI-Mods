using Harmony;
using UnityEngine;

/*
 * TODO: Improve compatibility with sandbox mode (low priority)
 * TODO: Find a way to differentiate decorative buildings such as paintings from normal buildings.
 *       Only option I know of at the moment is to hardcode them, which could lead to compatibility issues.
 */


namespace BuildOverPlants
{
	public static class BuildOverPlants
	{
		public static class BuildOverPlants_OnLoad
		{
			public static void OnLoad()
			{
				Debug.Log("Build Over Plants Mod Loaded");
			}
		}

		// Patch BuildingDef.IsAreaClear to allow placement of buildings over plants
		[HarmonyPatch(typeof(BuildingDef))]
		[HarmonyPatch("IsAreaClear")]
		public static class BuildOverPlants_Patch_BuildingDef_IsAreaClear
		{
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
