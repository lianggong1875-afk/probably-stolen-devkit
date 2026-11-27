using System;
using System.Collections.Generic;

public class ShipHelper
{
	public const string SYSTEM_TAG = "SYSTEM_TAG";

	public const string SYSTEM_TAG_SPECIAL = "SYSTEM_TAG_SPECIAL";

	public const string SYSTEM_TAG_NAVIGATION = "SYSTEM_TAG_NAVIGATION";

	public const string SYSTEM_TAG_POWER = "SYSTEM_TAG_POWER";

	public const string SYSTEM_TAG_PROPULSION = "SYSTEM_TAG_PROPULSION";

	public const string SYSTEM_TAG_PRODUCTION = "SYSTEM_TAG_PRODUCTION";

	public const string SYSTEM_TAG_INDUSTRIAL = "SYSTEM_TAG_INDUSTRIAL";

	public const string SYSTEM_TAG_SERVICE = "SYSTEM_TAG_SERVICE";

	public const string SYSTEM_TAG_UTILITY = "SYSTEM_TAG_UTILITY";

	public const string SYSTEM_TAG_HULL = "SYSTEM_TAG_HULL";

	public const string SYSTEM_TAG_EXTERNAL = "SYSTEM_TAG_EXTERNAL";

	public const string SYSTEM_TAG_WEAPON = "SYSTEM_TAG_WEAPON";

	public const string SYSTEM_POWER_CHANGE = "SYSTEM_POWER_CHANGE";

	public const string NAVIGATION_ALTERNATIF_INT = "NAVIGATION_ALTERNATIF_INT";

	public const string PROPULSION_FUEL_DRAIN_INT = "PROPULSION_FUEL_DRAIN_INT";

	public const string BASE_PROPULSION_FUEL_DRAIN_INT = "BASE_PROPULSION_FUEL_DRAIN_INT";

	public const string PROPULSION_SPEED_INT = "PROPULSION_SPEED_INT";

	public const string BASE_PROPULSION_SPEED_INT = "BASE_PROPULSION_SPEED_INT";

	public const string PROPULSION_PROGRESS_CURRENT_INT = "PROPULSION_PROGRESS_CURRENT_INT";

	public const string PROPULSION_DISTANCE_INT = "PROPULSION_DISTANCE_INT";

	public const string WATER_RECYCLER_SYSTEM_TAG = "WATER_RECYCLER_SYSTEM_TAG";

	public const int WATER_RECYCLER_BASE_EFFICIENCY = 50;

	public const string WATER_RECYCLER_CURRENT_EFFICIENCY_INT = "WATER_RECYCLER_CURRENT_EFFICIENCY_INT";

	public const string AUTONOMOUS_MEDICAL_SYSTEM_TAG = "AUTONOMOUS_MEDICAL_SYSTEM_TAG";

	public const int AUTONOMOUS_MEDICAL_HEAL_RATE = 5;

	public const string AUTONOMOUS_MEDICAL_HEAL_RATE_INT = "WATER_RECYCLER_CURRENT_EFFICIENCY_INT";

	public const string ARMOR_CONDITIONER_SYSTEM_TAG = "ARMOR_CONDITIONER_SYSTEM_TAG";

	public const int ARMOR_CONDITIONER_REPAIR_RATE = 5;

	public const string ARMOR_CONDITIONER_REPAIR_RATE_INT = "ARMOR_CONDITIONER_REPAIR_RATE_INT";

	public const string HULL_REPAIR_SYSTEM_TAG = "HULL_REPAIR_SYSTEM_TAG";

	public const int HULL_REPAIR_RATE = 2;

	public const string HULL_REPAIR_RATE_INT = "HULL_REPAIR_RATE_INT";

	public const string MINING_DRILL_SYSTEM = "MINING_DRILL_SYSTEM";

	public const string CURRENT_MINING_POWER_INT = "CURRENT_MINING_POWER_INT";

	public const int BASE_MINING_POWER = 100;

	public const string SALVAGE_GANTRY_SYSTEM = "SALVAGE_GANTRY_SYSTEM";

	public const string CURRENT_SALVAGE_POWER_INT = "CURRENT_SALVAGE_POWER_INT";

	public const int BASE_SALVAGE_POWER = 100;

	public const string IN_SLOT_BOOL = "IN_SLOT_BOOL";

	public const string BIO_PRINTER_SYSTEM = "BIO_PRINTER_SYSTEM";

	public const string BIO_PRINTING_REQUIRED_WORK_INT = "BIO_PRINTING_REQUIRED_WORK_INT";

	public static void InitPowerSystem(GameItem item)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_POWER");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_POWER");
		});
	}

	public static void InitIndustrialSystem(GameItem item)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_INDUSTRIAL");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_INDUSTRIAL");
		});
	}

	public static void InitNavigationSystem(GameItem item, GameInventory invModule, int navAlternatif)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_NAVIGATION");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_NAVIGATION");
		});
		MachineryHelper.InitDrawTypeMachinery(item, 3, 15, slotedCheck: true);
		item.ModifyTag("DRAW_CURRENT_INT", (TagState state) =>
		{
			state.Enable().SetInt(15);
		});
		item.ModifyTag("NAVIGATION_ALTERNATIF_INT", (TagState state) =>
		{
			state.Enable().SetInt(navAlternatif);
		});
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceNavEffect(item);
			}
			ModuleHelper.ApplyModifiedPower(item);
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceNavEffect(item);
			}
			ModuleHelper.ApplyModifiedPower(item);
		};
	}

	public static void InitPropulsionSystem(GameItem item, GameInventory invModule, int fuelDrain, int speed)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_PROPULSION");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_PROPULSION");
		});
		item.ModifyTag("BASE_PROPULSION_FUEL_DRAIN_INT", (TagState state) =>
		{
			state.Enable().SetInt(fuelDrain);
		});
		item.ModifyTag("BASE_PROPULSION_SPEED_INT", (TagState state) =>
		{
			state.Enable().SetInt(speed);
		});
		item.ModifyTag("PROPULSION_FUEL_DRAIN_INT", (TagState state) =>
		{
			state.Enable().SetInt(fuelDrain);
		});
		item.ModifyTag("PROPULSION_SPEED_INT", (TagState state) =>
		{
			state.Enable().SetInt(speed);
		});
		item.mayInventoryRemoveItemFunc = (GameItem _, GameInventory _) => false;
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyPerformancePropulsionEffect(item);
				ModuleHelper.ApplyEfficiencyPropulsionEffect(item);
			}
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyPerformancePropulsionEffect(item);
				ModuleHelper.ApplyEfficiencyPropulsionEffect(item);
			}
		};
	}

	public static void InitWaterRecyclerSystem(GameItem item, GameInventory invModule, GameInventory internalTank, GameInventory bottleInv, int drawnPower)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_SERVICE");
		item.EnableTag("WATER_RECYCLER_SYSTEM_TAG");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_SERVICE");
		});
		item.ModifyTag("WATER_RECYCLER_CURRENT_EFFICIENCY_INT", (TagState state) =>
		{
			state.Enable().SetInt(50);
		});
		MachineryHelper.InitMachinery(item, drawnPower);
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceWaterRecyclerEffect(item);
			}
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceWaterRecyclerEffect(item);
			}
		};
		bottleInv.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("LIQUID_CONTAINER_TAG"))
			{
				List<GameItem> list = GraphUtils.FindAllChildrenType(internalTank, (GameItem gameItem2) => gameItem2.IsTag("LIQUID_CONTAINER_TAG"));
				List<GameItem> list2 = GraphUtils.FindAllChildrenType(bottleInv, (GameItem gameItem2) => gameItem2.IsTag("LIQUID_CONTAINER_TAG"));
				if (list.Count != 0 && list2.Count != 0)
				{
					LiquidContainerHelper.TransferLiquid(list2[0], list[0]);
				}
			}
		};
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem2, GameInventory parentInventory, SlotMarker slot) =>
		{
			List<GameCharacterItem> list = GraphUtils.FindAllChildrenType(GameMaster.current.shipManager.ship, (GameCharacterItem gameCharacterItem) => gameCharacterItem != null && !gameCharacterItem.isDead);
			if (list.Count == 0 || !IsSloted(gameItem2))
			{
				gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: true);
				});
			}
			else
			{
				gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
				int num = 0;
				foreach (GameCharacterItem item2 in list)
				{
					_ = item2;
					num += Survival.THIRST_LOSS;
				}
				int num2 = gameItem2.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT").GetInt();
				num = num * num2 / 100;
				if (MachineryHelper.IsMachineryToggleOn(gameItem2))
				{
					List<GameItem> list2 = GraphUtils.FindAllChildrenType(internalTank, (GameItem gameItem3) => gameItem3.IsTag("LIQUID_CONTAINER_TAG"));
					if (list2.Count != 0)
					{
						LiquidContainerHelper.AddLiquid(list2[0], num);
						List<GameItem> list3 = GraphUtils.FindAllChildrenType(bottleInv, (GameItem gameItem3) => gameItem3.IsTag("LIQUID_CONTAINER_TAG"));
						if (list2.Count != 0 && list3.Count != 0)
						{
							LiquidContainerHelper.TransferLiquid(list3[0], list2[0]);
						}
					}
				}
			}
		}));
	}

	public static void InitAutonomousMedicalSystem(GameItem item, GameInventory invModule, GameInventory patientSlot, int drawnPower)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_SERVICE");
		item.EnableTag("AUTONOMOUS_MEDICAL_SYSTEM_TAG");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_SERVICE");
		});
		item.ModifyTag("WATER_RECYCLER_CURRENT_EFFICIENCY_INT", (TagState state) =>
		{
			state.Enable().SetInt(5);
		});
		MachineryHelper.InitMachinery(item, drawnPower);
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceAutonomousMedicalEffect(item);
			}
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceAutonomousMedicalEffect(item);
			}
		};
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem2, GameInventory parentInventory, SlotMarker slot) =>
		{
			List<GameCharacterItem> list = GraphUtils.FindAllChildrenType(GameMaster.current.shipManager.ship, (GameCharacterItem gameCharacterItem) => gameCharacterItem != null && !gameCharacterItem.isDead);
			if (MachineryHelper.IsMachineryToggleOn(gameItem2) && IsSloted(gameItem2))
			{
				int healRate = gameItem2.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT").GetInt();
				if (list.Count != 0 && !ModuleHelper.IsAutoMedicalDone(list))
				{
					gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					MachineryHelper.CheckLowPowerMode(gameItem2);
					{
						foreach (GameCharacterItem item2 in list)
						{
							ModuleHelper.AutonomousMedicalHeal(item2, healRate);
						}
						return;
					}
				}
				gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: true);
				});
				MachineryHelper.CheckLowPowerMode(gameItem2);
			}
		}));
	}

	public static void InitHullRepairSystem(GameItem item, GameInventory invModule, int drawnPower)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_EXTERNAL");
		item.EnableTag("HULL_REPAIR_SYSTEM_TAG");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_EXTERNAL");
		});
		item.ModifyTag("HULL_REPAIR_RATE_INT", (TagState state) =>
		{
			state.Enable().SetInt(2);
		});
		MachineryHelper.InitMachinery(item, drawnPower);
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceHullRepairEffect(item);
			}
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceHullRepairEffect(item);
			}
		};
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem2, GameInventory parentInventory, SlotMarker slot) =>
		{
			List<GameItem> list = GraphUtils.FindAllChildrenType(GameMaster.current.shipManager.ship, (GameItem gameItem3) => gameItem3.IsTag("SYSTEM_TAG_HULL"));
			if (MachineryHelper.IsMachineryToggleOn(gameItem2) && IsSloted(gameItem2))
			{
				int repairAmount = gameItem2.GetTagReadonly("HULL_REPAIR_RATE_INT").GetInt();
				if (HullHelper.IsHullIntact(list[0]))
				{
					gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: true);
					});
				}
				else
				{
					gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					HullHelper.RepairHull(list[0], repairAmount);
				}
			}
		}));
	}

	public static void InitMiningDrillSystem(GameItem item, GameInventory invModule, int drawnPower)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("MINING_DRILL_SYSTEM");
		item.EnableTag("SYSTEM_TAG_EXTERNAL");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_EXTERNAL");
		});
		item.ModifyTag("CURRENT_MINING_POWER_INT", (TagState state) =>
		{
			state.Enable().SetInt(100);
		});
		MachineryHelper.InitMachinery(item, drawnPower);
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceMiningDrillEffect(item);
			}
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceMiningDrillEffect(item);
			}
		};
	}

	public static void InitSalvageGantrySystem(GameItem item, GameInventory invModule, int drawnPower)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SALVAGE_GANTRY_SYSTEM");
		item.EnableTag("SYSTEM_TAG_EXTERNAL");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_EXTERNAL");
		});
		item.ModifyTag("CURRENT_SALVAGE_POWER_INT", (TagState state) =>
		{
			state.Enable().SetInt(100);
		});
		MachineryHelper.InitMachinery(item, drawnPower);
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceSalvageGantryEffect(item);
			}
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyModifiedPower(item);
				ModuleHelper.ApplyPerformanceSalvageGantryEffect(item);
			}
		};
	}

	public static void InitArmorConditionerSystem(GameItem item, GameInventory invModule, GameInventory armorSlot, int drawnPower, int progressSpeed)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_INDUSTRIAL");
		item.SetType("SYSTEM_TAG_INDUSTRIAL");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_INDUSTRIAL");
		});
		item.EnableTag("ARMOR_CONDITIONER_SYSTEM_TAG");
		item.ModifyTag("ARMOR_CONDITIONER_REPAIR_RATE_INT", (TagState state) =>
		{
			state.Enable().SetInt(progressSpeed);
		});
		MachineryHelper.InitMachinery(item, drawnPower);
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyBasicModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyPerformanceArmorConditionerEffect(item);
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyBasicModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyPerformanceArmorConditionerEffect(item);
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem2, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (!IsSloted(gameItem2))
			{
				gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
				MachineryHelper.CheckLowPowerMode(gameItem2);
			}
			if (MachineryHelper.IsMachineryToggleOn(gameItem2))
			{
				List<GameItem> list = GraphUtils.FindAllChildrenType(armorSlot, (GameItem gameItem3) => gameItem3.IsTag("armor_item"));
				if (list.Count != 0)
				{
					gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					MachineryHelper.CheckLowPowerMode(gameItem2);
					int repairAmount = gameItem2.GetTagReadonly("ARMOR_CONDITIONER_REPAIR_RATE_INT").GetInt();
					ArmorHelper.RepairArmorWithAmount(list[0], repairAmount);
				}
				else
				{
					gameItem2.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: true);
					});
					MachineryHelper.CheckLowPowerMode(gameItem2);
				}
			}
		}));
	}

	public static void InitHydroponicSystem(GameItem item, GameInventory invModule, GameInventory seedIntake, GameInventory outputInventory, int drawnPower, int progressSpeed)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_INDUSTRIAL");
		item.SetType("SYSTEM_TAG_INDUSTRIAL");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_INDUSTRIAL");
		});
		string[] triggerStartProcessItemTags = new string[1] { "SEED_ITEM_TAG" };
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyBasicModuleEffect(invModule, addedItem, item);
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyBasicModuleEffect(invModule, addedItem, item);
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		seedIntake.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (MachineryHelper.IsStartingProcess(addedItem, triggerStartProcessItemTags))
			{
				item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		seedIntake.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (!MachineryHelper.IsStartingProcess(addedItem, triggerStartProcessItemTags))
			{
				item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem3, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (!IsSloted(gameItem3))
			{
				gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: true);
				});
			}
			else if (MachineryHelper.IsProcessingActive(gameItem3))
			{
				gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
			}
			else if (MachineryHelper.IsMachineryToggleOn(gameItem3))
			{
				List<GameItem> list = GraphUtils.FindAllChildrenType(seedIntake, (GameItem gameItem4) => gameItem4.IsTag("SEED_ITEM_TAG"));
				if (list.Count != 0)
				{
					gameItem3.ModifyTag("IS_ACTIVE_PROCESSING_BOOL", (TagState state) =>
					{
						state.SetBool(val: true);
					});
					int required = list[0].GetTagReadonly("REQUIRED_PROGRESS").GetInt();
					int harvestQuantity = list[0].GetTagReadonly("HARVEST_QUANTITY").GetInt();
					string harvestId = list[0].GetTagReadonly("HARVEST_ITEM_ID").GetString();
					gameItem3.ModifyTag("PROCESSING_CURRENT_PROGRESS_INT", (TagState state) =>
					{
						state.Enable().SetInt(0);
					});
					gameItem3.ModifyTag("PROCESSING_MAX_PROGRESS_INT", (TagState state) =>
					{
						state.Enable().SetInt(required);
					});
					gameItem3.ModifyTag("HARVEST_QUANTITY", (TagState state) =>
					{
						state.Enable().SetInt(harvestQuantity);
					});
					gameItem3.ModifyTag("HARVEST_ITEM_ID", (TagState state) =>
					{
						state.Enable().SetString(harvestId);
					});
					seedIntake.Expel(list[0]);
					gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					MachineryHelper.UpdatePowerUsageProcessTypeMachine(gameItem3);
				}
			}
		}));
		MachineryHelper.InitProgressTypeMachinery(item, drawnPower, progressSpeed);
		GameItem gameItem2 = item;
		gameItem2.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem2.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem3, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (MachineryHelper.IsProcessingActive(gameItem3))
			{
				int num = gameItem3.GetTagReadonly("PROCESSING_CURRENT_PROGRESS_INT").GetInt();
				int num2 = gameItem3.GetTagReadonly("PROCESSING_MAX_PROGRESS_INT").GetInt();
				if (num >= num2)
				{
					gameItem3.ModifyTag("IS_ACTIVE_PROCESSING_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					int yield = gameItem3.GetTagReadonly("HARVEST_QUANTITY").GetInt();
					string identifier = gameItem3.GetTagReadonly("HARVEST_ITEM_ID").GetString();
					yield = HydroponicHelper.ApplyBonusModuleYield(yield, gameItem3, invModule);
					for (int num3 = 0; num3 < yield; num3++)
					{
						GraphUtils.TryAcceptAllOrDestroy(outputInventory, DirectoryMaster.Item(identifier), "harvest output full");
					}
					gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: true);
					});
				}
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(gameItem3);
		}));
		item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
		{
			state.SetBool(val: true);
		});
		MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
	}

	public static void InitBioPrinterSystem(GameItem item, GameInventory invModule, GameInventory intake, GameInventory outputInventory, int drawnPower, int progressSpeed)
	{
		item.EnableTag("SYSTEM_TAG");
		item.EnableTag("SYSTEM_TAG_INDUSTRIAL");
		item.SetType("SYSTEM_TAG_INDUSTRIAL");
		item.ModifyTag("SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetString("SYSTEM_TAG_INDUSTRIAL");
		});
		string[] triggerStartProcessItemTags = new string[1] { "ORGAN_ITEM_TAG" };
		invModule.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyBasicModuleEffect(invModule, addedItem, item);
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		invModule.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(invModule, addedItem, item);
				ModuleHelper.ApplyBasicModuleEffect(invModule, addedItem, item);
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		intake.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (MachineryHelper.IsStartingProcess(addedItem, triggerStartProcessItemTags))
			{
				item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
			}
			if (addedItem.IsTag("ORGAN_ITEM_TAG"))
			{
				addedItem.EnableTag("IS_ORGAN_STORE_IN_TRANSPORT_CONTAINER");
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		intake.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (!MachineryHelper.IsStartingProcess(addedItem, triggerStartProcessItemTags))
			{
				item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
			}
			if (addedItem.IsTag("ORGAN_ITEM_TAG"))
			{
				addedItem.DisableTag("IS_ORGAN_STORE_IN_TRANSPORT_CONTAINER");
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
		};
		GameInventory gameInventory = outputInventory;
		gameInventory.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameInventory.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("ORGAN_ITEM_TAG"))
			{
				addedItem.EnableTag("IS_ORGAN_STORE_IN_TRANSPORT_CONTAINER");
			}
		}));
		GameInventory gameInventory2 = outputInventory;
		gameInventory2.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameInventory2.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("ORGAN_ITEM_TAG"))
			{
				addedItem.DisableTag("IS_ORGAN_STORE_IN_TRANSPORT_CONTAINER");
			}
		}));
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem3, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (!IsSloted(gameItem3))
			{
				gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: true);
				});
			}
			else if (MachineryHelper.IsProcessingActive(gameItem3))
			{
				gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
				{
					state.SetBool(val: false);
				});
			}
			else if (MachineryHelper.IsMachineryToggleOn(gameItem3))
			{
				List<GameItem> list = GraphUtils.FindAllChildrenType(intake, (GameItem gameItem4) => gameItem4.IsTag("ORGANIC_ORGAN_TAG") || gameItem4.IsTag("BIO_PRINTABLE_TAG"));
				if (list.Count != 0)
				{
					gameItem3.ModifyTag("IS_ACTIVE_PROCESSING_BOOL", (TagState state) =>
					{
						state.SetBool(val: true);
					});
					int required = list[0].GetTagReadonly("BIO_PRINTING_REQUIRED_WORK_INT").GetInt();
					int harvestQuantity = 1;
					string harvestId = list[0].identifier;
					gameItem3.ModifyTag("PROCESSING_CURRENT_PROGRESS_INT", (TagState state) =>
					{
						state.Enable().SetInt(0);
					});
					gameItem3.ModifyTag("PROCESSING_MAX_PROGRESS_INT", (TagState state) =>
					{
						state.Enable().SetInt(required);
					});
					gameItem3.ModifyTag("HARVEST_QUANTITY", (TagState state) =>
					{
						state.Enable().SetInt(harvestQuantity);
					});
					gameItem3.ModifyTag("HARVEST_ITEM_ID", (TagState state) =>
					{
						state.Enable().SetString(harvestId);
					});
					gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					MachineryHelper.UpdatePowerUsageProcessTypeMachine(gameItem3);
				}
			}
		}));
		MachineryHelper.InitProgressTypeMachinery(item, drawnPower, progressSpeed);
		GameItem gameItem2 = item;
		gameItem2.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem2.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem3, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (MachineryHelper.IsProcessingActive(gameItem3))
			{
				int num = gameItem3.GetTagReadonly("PROCESSING_CURRENT_PROGRESS_INT").GetInt();
				int num2 = gameItem3.GetTagReadonly("PROCESSING_MAX_PROGRESS_INT").GetInt();
				if (num >= num2)
				{
					gameItem3.ModifyTag("IS_ACTIVE_PROCESSING_BOOL", (TagState state) =>
					{
						state.SetBool(val: false);
					});
					int yield = gameItem3.GetTagReadonly("HARVEST_QUANTITY").GetInt();
					string identifier = gameItem3.GetTagReadonly("HARVEST_ITEM_ID").GetString();
					yield = HydroponicHelper.ApplyBonusModuleYield(yield, gameItem3, invModule);
					for (int num3 = 0; num3 < yield; num3++)
					{
						GraphUtils.TryAcceptAllOrDestroy(outputInventory, DirectoryMaster.Item(identifier), "harvest output full");
					}
					gameItem3.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
					{
						state.SetBool(val: true);
					});
				}
			}
			MachineryHelper.UpdatePowerUsageProcessTypeMachine(gameItem3);
		}));
		item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
		{
			state.SetBool(val: true);
		});
		MachineryHelper.UpdatePowerUsageProcessTypeMachine(item);
	}

	public static GameSlotInventory CreatePropulsionSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Propulsion System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_PROPULSION");
		gameSlotInventory.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
		};
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateNavigationSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Navigation System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_NAVIGATION");
		gameSlotInventory.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
		};
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreatePowerSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Power System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_POWER");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateIndustrialSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Industrial System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_INDUSTRIAL");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateServiceSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Service System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_SERVICE");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateUtilitySlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Utility System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_UTILITY");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateHullSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 4;
		gameSlotInventory.SetTooltipName("[Hull System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_HULL");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateExternalSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[External System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_EXTERNAL");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateArmamentSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 3;
		gameSlotInventory.SetTooltipName("[Armament System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG_WEAPON");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static GameSlotInventory CreateWildcardSlot()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.SetBackgroundFadeSprite("Items/items_construction", "steel_sheet_rough");
		gameSlotInventory.slotPriorityFunc = (GameItem _, GameInventory _, SlotMarker _) => 2;
		gameSlotInventory.SetTooltipName("[Wildcard System Slot]");
		ContainerHelper.AllowOnlyTaggedItem(gameSlotInventory, "SYSTEM_TAG");
		InitSystemSlotOnSlotEvent(gameSlotInventory);
		return gameSlotInventory;
	}

	public static void InitSystemSlotOnSlotEvent(GameSlotInventory utilInv)
	{
		utilInv.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(utilInv.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("SYSTEM_TAG"))
			{
				addedItem.ModifyTag("IN_SLOT_BOOL", (TagState state) =>
				{
					state.Enable().SetBool(val: true);
				});
			}
		}));
		utilInv.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(utilInv.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.IsTag("SYSTEM_TAG"))
			{
				addedItem.ModifyTag("IN_SLOT_BOOL", (TagState state) =>
				{
					state.Enable().SetBool(val: false);
				});
			}
		}));
	}

	public static bool IsSloted(GameItem system)
	{
		return system.GetTagReadonly("IN_SLOT_BOOL").GetBool();
	}

	public static void CreateShipSystemTooltip(RichTextBuilder builder, GameItem item)
	{
		if (item.IsTag("SYSTEM_TAG"))
		{
			if (!IsSloted(item))
			{
				builder.AddLine("*UNSLOTED, Disabled*", ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
			if (item.IsTag("SYSTEM_TAG_NAVIGATION"))
			{
				builder.AddLine("Once the Navigation System is fully charged, you may start a scan to find nearby Points of Interest.");
			}
			if (item.IsTag("SYSTEM_TAG_PROPULSION"))
			{
				builder.AddLine("The Propulsion System move the ship between Points of Interest by consumming Exotic Fuel");
			}
			if (item.IsTag("WATER_RECYCLER_SYSTEM_TAG"))
			{
				builder.AddLine("Water Recyle Rate: " + item.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT").GetInt() + "%", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (item.IsTag("AUTONOMOUS_MEDICAL_SYSTEM_TAG"))
			{
				builder.AddLine("Healing Rate: " + item.GetTagReadonly("WATER_RECYCLER_CURRENT_EFFICIENCY_INT").GetInt(), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (item.IsTag("ARMOR_CONDITIONER_SYSTEM_TAG"))
			{
				builder.AddLine("Repair Rate: " + item.GetTagReadonly("ARMOR_CONDITIONER_REPAIR_RATE_INT").GetInt(), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (item.IsTag("SYSTEM_TAG_HULL"))
			{
				HullHelper.CreateHullSystemTooltip(builder, item);
			}
			if (item.IsTag("HULL_REPAIR_SYSTEM_TAG"))
			{
				builder.AddLine("Repair Rate: " + item.GetTagReadonly("HULL_REPAIR_RATE_INT").GetInt(), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (item.IsTag("MINING_DRILL_SYSTEM"))
			{
				builder.AddLine("Mining Power: " + item.GetTagReadonly("CURRENT_MINING_POWER_INT").GetInt(), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (item.IsTag("SALVAGE_GANTRY_SYSTEM"))
			{
				builder.AddLine("Salvage Power: " + item.GetTagReadonly("CURRENT_SALVAGE_POWER_INT").GetInt(), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
		}
	}

	public static void EquipCargoShip(GameItem shipNode)
	{
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("large_storage_bay"));
		GameItem gameItem = DirectoryMaster.Item("propulsion_system");
		GraphUtils.TryAcceptAll(gameItem, DirectoryMaster.Item("exotic_fuel").SetUnitCount(50));
		GraphUtils.TryAcceptAll(shipNode, gameItem);
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("storage_bay"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("nav_system"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("civilian_hull"));
		GameItem gameItem2 = DirectoryMaster.Item("battery_hub");
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(shipNode, gameItem2);
		GameItem gameItem3 = DirectoryMaster.Item("water_recycler_system");
		GraphUtils.TryAcceptAll(gameItem3, DirectoryMaster.Item("water_tank"));
		GraphUtils.TryAcceptAll(shipNode, gameItem3);
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("hydroponic_system"));
		GameItem gameItem4 = DirectoryMaster.Item("firstaid_box");
		GraphUtils.TryAcceptAll(gameItem4, DirectoryMaster.Item("hemostatic_bandage_item"));
		GraphUtils.TryAcceptAll(gameItem4, DirectoryMaster.Item("hemostatic_bandage_item"));
		GraphUtils.TryAcceptAll(gameItem4, DropDirectory.FullBicarsolePillBottle());
		GraphUtils.TryAcceptAll(shipNode, gameItem4);
	}

	public static void EquipMilitaryTransportShip(GameItem shipNode)
	{
		GameItem gameItem = DirectoryMaster.Item("propulsion_system");
		GraphUtils.TryAcceptAll(gameItem, DirectoryMaster.Item("exotic_fuel").SetUnitCount(50));
		GraphUtils.TryAcceptAll(shipNode, gameItem);
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("storage_bay"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("nav_system"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("armored_hull"));
		GameItem gameItem2 = DirectoryMaster.Item("battery_hub");
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(shipNode, gameItem2);
		GameItem item = DirectoryMaster.Item("autonomous_medical_system");
		GraphUtils.TryAcceptAll(shipNode, item);
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("armor_conditioner_system"));
	}

	public static void EquipUtilityShip(GameItem shipNode)
	{
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("salvage_system"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("mining_system"));
		GameItem gameItem = DirectoryMaster.Item("propulsion_system");
		GraphUtils.TryAcceptAll(gameItem, DirectoryMaster.Item("exotic_fuel").SetUnitCount(50));
		GraphUtils.TryAcceptAll(shipNode, gameItem);
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("storage_bay"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("nav_system"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("heavy_duty_hull"));
		GameItem gameItem2 = DirectoryMaster.Item("battery_hub");
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(shipNode, gameItem2);
	}

	public static void EquipRefugeeShip(GameItem shipNode)
	{
		GameItem gameItem = DirectoryMaster.Item("propulsion_system");
		GraphUtils.TryAcceptAll(gameItem, DirectoryMaster.Item("exotic_fuel").SetUnitCount(25));
		GraphUtils.TryAcceptAll(shipNode, gameItem);
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("passenger_cabin"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("storage_bay"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("nav_system"));
		GraphUtils.TryAcceptAll(shipNode, DirectoryMaster.Item("makeshift_hull"));
		GameItem gameItem2 = DirectoryMaster.Item("battery_hub");
		GraphUtils.TryAcceptAll(gameItem2, DirectoryMaster.Item("energy_credit"));
		GraphUtils.TryAcceptAll(shipNode, gameItem2);
	}
}
