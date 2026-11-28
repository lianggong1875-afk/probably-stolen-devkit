using System;
using System.Collections.Generic;

public class TechnicianBackpackHelper
{
	public const string COMPLEXE_EQUIPMENT_TAG = "COMPLEXE_EQUIPMENT_TAG";

	public const string CHARGE_RATE_INT = "CHARGE_RATE_INT";

	public const string CURRENT_CHARGE_INT = "CURRENT_CHARGE_INT";

	public const string MAX_CHARGE_INT = "MAX_CHARGE_INT";

	public const string POWER_SWITCH_BOOL = "POWER_SWITCH_BOOL";

	public const string COMPLEXE_MODULE_TAG = "COMPLEXE_MODULE_TAG";

	public const string COMPLEXE_MODULE_ADJACENCY_TAG = "COMPLEXE_MODULE_ADJACENCY_TAG";

	public const string CHARGE_RATE_MODIFIER_INT = "CHARGE_RATE_MODIFIER_INT";

	public const string MAX_CHARGE_MODIFIER_INT = "MAX_CHARGE_MODIFIER_INT";

	public const string COMPLEXE_MODULE_POWER_ECO_AMOUNT_INT = "COMPLEXE_MODULE_POWER_ECO_AMOUNT_INT";

	public const string COMPLEXE_MODULE_EFFECTIVENESS_AMOUNT_INT = "COMPLEXE_MODULE_EFFECTIVENESS_AMOUNT_INT";

	public const string COMPLEXE_SYSTEM_TAG = "COMPLEXE_SYSTEM_TAG";

	public const string POWER_USE_INT = "POWER_USE_INT";

	public const string POWER_USE_BASE_INT = "POWER_USE_BASE_INT";

	public static void InitComplexeEquipment(GameItem item, GameInventory slotInventory, GameInventory itenInventory)
	{
		item.ModifyTag("COMPLEXE_EQUIPMENT_TAG", (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.ModifyTag("CHARGE_RATE_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.ModifyTag("CURRENT_CHARGE_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.ModifyTag("MAX_CHARGE_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.mayToggleSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
		item.canToggleSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem2, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (gameItem2.GetTagReadonly("POWER_SWITCH_BOOL").GetBool() && !IsEquipmentCharged(gameItem2))
			{
				List<GameItem> list = GraphUtils.FindAllChildrenType(slotInventory, (GameItem gameItem3) => gameItem3.IsTag("power_source_item"));
				if (list.Count > 0)
				{
					int num = list[0].GetTagReadonly("power_source_item_energy").GetInt();
					int num2 = list[0].GetTagReadonly("power_source_item_bandwidth").GetInt();
					int num3 = 0;
					num3 = ((num >= num2) ? num2 : num);
					int current_draw = gameItem2.GetTagReadonly("CURRENT_CHARGE_INT").GetInt();
					int num4 = gameItem2.GetTagReadonly("MAX_CHARGE_INT").GetInt();
					int num5 = gameItem2.GetTagReadonly("CHARGE_RATE_INT").GetInt();
					int num6 = num4 - current_draw;
					int newAvailableEnergy = 0;
					int amountCharged = 0;
					if (num3 >= num5)
					{
						newAvailableEnergy = num - num5;
						amountCharged = num5;
					}
					else
					{
						newAvailableEnergy = 0;
						amountCharged = num3;
					}
					if (amountCharged >= num6)
					{
						newAvailableEnergy = num - num6;
						amountCharged = num6;
					}
					gameItem2.ModifyTag("CURRENT_CHARGE_INT", (TagState state) =>
					{
						state.SetInt(current_draw + amountCharged);
					});
					list[0].ModifyTag("power_source_item_energy", (TagState state) =>
					{
						state.SetInt(newAvailableEnergy);
					});
				}
			}
		}));
		itenInventory.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			CheckInventoryStatus(item, itenInventory);
		};
		itenInventory.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			CheckInventoryStatus(item, itenInventory);
		};
		item.onToggleSlotItemFunc = (GameItem gameItem2, GameInventory inventory, SlotMarker _) =>
		{
			bool currentState = !gameItem2.GetTagReadonly("POWER_SWITCH_BOOL").GetBool();
			gameItem2.ModifyTag("POWER_SWITCH_BOOL", (TagState state) =>
			{
				state.Enable().SetBool(currentState);
			});
		};
	}

	public static void CheckBasicModule(List<GameItem> modules, GameItem item)
	{
		int totalChargeRate = 0;
		int totalCapacity = 0;
		int currentCharge = item.GetTagReadonly("CURRENT_CHARGE_INT").GetInt();
		foreach (GameItem module in modules)
		{
			int num = module.GetTagReadonly("CHARGE_RATE_MODIFIER_INT").GetInt();
			int num2 = module.GetTagReadonly("MAX_CHARGE_MODIFIER_INT").GetInt();
			totalChargeRate += num;
			totalCapacity += num2;
		}
		if (currentCharge >= totalCapacity)
		{
			currentCharge = totalCapacity;
		}
		item.ModifyTag("CURRENT_CHARGE_INT", (TagState state) =>
		{
			state.Enable().SetInt(currentCharge);
		});
		item.ModifyTag("CHARGE_RATE_INT", (TagState state) =>
		{
			state.Enable().SetInt(totalChargeRate);
		});
		item.ModifyTag("MAX_CHARGE_INT", (TagState state) =>
		{
			state.Enable().SetInt(totalCapacity);
		});
	}

	public static void CheckAdjacentModule(List<GameItem> systems, GameItem item)
	{
		foreach (GameItem system in systems)
		{
			List<GameItem> list = system.FindAdjacentType<GameItem>();
			int num = 0;
			int num2 = 0;
			foreach (GameItem item2 in list)
			{
				if (item2.IsTag("COMPLEXE_MODULE_ADJACENCY_TAG"))
				{
					int num3 = item2.GetTagReadonly("COMPLEXE_MODULE_POWER_ECO_AMOUNT_INT").GetInt();
					num += num3;
					int num4 = item2.GetTagReadonly("COMPLEXE_MODULE_EFFECTIVENESS_AMOUNT_INT").GetInt();
					num2 += num4;
				}
			}
			ApplyPowerEcoAmount(system, num);
			ApplyEffectivenessAmount(system, num2);
		}
	}

	public static void ApplyPowerEcoAmount(GameItem system, int powerReduction)
	{
		int num = system.GetTagReadonly("POWER_USE_BASE_INT").GetInt();
		int newPowerUsage = 0;
		if (num + powerReduction < 0)
		{
			newPowerUsage = 0;
		}
		else
		{
			newPowerUsage = num + powerReduction;
		}
		system.ModifyTag("POWER_USE_INT", (TagState state) =>
		{
			state.Enable().SetInt(newPowerUsage);
		});
	}

	public static void ApplyEffectivenessAmount(GameItem system, int totalEffectivenessAmount)
	{
		foreach (CombatAbilityEffect effect in system.effects)
		{
			if (effect.valueType == CombatAbilityEffect.ValueType.standard)
			{
				effect.value = effect.valueBase + totalEffectivenessAmount;
			}
			else if (effect.valueType == CombatAbilityEffect.ValueType.uniform)
			{
				effect.valueLow = effect.valueLowBase + totalEffectivenessAmount;
				effect.valueHigh = effect.valueHighBase + totalEffectivenessAmount;
			}
		}
	}

	public static void CheckInventoryStatus(GameItem item, GameInventory inventory)
	{
		List<GameItem> modules = GraphUtils.FindAllChildrenType(inventory, (GameItem gameItem) => gameItem.IsTag("COMPLEXE_MODULE_TAG"));
		List<GameItem> systems = GraphUtils.FindAllChildrenType(inventory, (GameItem gameItem) => gameItem.IsTag("COMPLEXE_SYSTEM_TAG"));
		CheckBasicModule(modules, item);
		CheckAdjacentModule(systems, item);
	}

	public static bool IsEquipmentCharged(GameItem item)
	{
		int num = item.GetTagReadonly("CURRENT_CHARGE_INT").GetInt();
		int num2 = item.GetTagReadonly("MAX_CHARGE_INT").GetInt();
		if (num >= num2)
		{
			return true;
		}
		return false;
	}

	public static void InitComplexeModule(GameItem item, int chargeRateMod = 0, int maxChargeMod = 0, bool adjacent = false, int powerEcoAmount = 0, int effectivenessAmount = 0)
	{
		item.ModifyTag("COMPLEXE_MODULE_TAG", (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.ModifyTag("CHARGE_RATE_MODIFIER_INT", (TagState state) =>
		{
			state.Enable().SetInt(chargeRateMod);
		});
		item.ModifyTag("MAX_CHARGE_MODIFIER_INT", (TagState state) =>
		{
			state.Enable().SetInt(maxChargeMod);
		});
		if (adjacent)
		{
			item.ModifyTag("COMPLEXE_MODULE_ADJACENCY_TAG", (TagState state) =>
			{
				state.Enable().SetBool(val: true);
			});
			item.ModifyTag("COMPLEXE_MODULE_POWER_ECO_AMOUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(powerEcoAmount);
			});
			item.ModifyTag("COMPLEXE_MODULE_EFFECTIVENESS_AMOUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(effectivenessAmount);
			});
		}
	}

	public static void InitComplexeSystem(GameItem item, int energyPerUse = 1)
	{
		item.ModifyTag("COMPLEXE_SYSTEM_TAG", (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.ModifyTag("POWER_USE_INT", (TagState state) =>
		{
			state.Enable().SetInt(energyPerUse);
		});
		item.ModifyTag("POWER_USE_BASE_INT", (TagState state) =>
		{
			state.Enable().SetInt(energyPerUse);
		});
		item.onItemTargetItemFunc = (GameItem s, GameItem t) =>
		{
		};
		GameItem gameItem = item;
		gameItem.onItemTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(gameItem.onItemTargetItemFunc, (Action<GameItem, GameItem>)((GameItem s, GameItem t) =>
		{
			GameItem gameItem2 = GraphUtils.FindParentType<GameItem>(item);
			if (gameItem2 != null && gameItem2.IsTag("COMPLEXE_EQUIPMENT_TAG") && HasPowerToUseComplexeSystem(item, gameItem2) && GameMaster.current.raidManager.CastAbility(s, t))
			{
				UsePowerForComplexeSystem(item, gameItem2);
			}
		}));
	}

	public static bool HasPowerToUseComplexeSystem(GameItem system, GameItem equipment)
	{
		int num = system.GetTagReadonly("POWER_USE_INT").GetInt();
		int num2 = equipment.GetTagReadonly("CURRENT_CHARGE_INT").GetInt();
		if (num <= num2)
		{
			return true;
		}
		return false;
	}

	public static void UsePowerForComplexeSystem(GameItem system, GameItem equipment)
	{
		int required = system.GetTagReadonly("POWER_USE_INT").GetInt();
		int available = equipment.GetTagReadonly("CURRENT_CHARGE_INT").GetInt();
		equipment.ModifyTag("CURRENT_CHARGE_INT", (TagState state) =>
		{
			state.Enable().SetInt(available - required);
		});
	}

	public static void CreateComplexeEquipmentTooltip(RichTextBuilder builder, GameItem item)
	{
		if (item.IsTag("COMPLEXE_EQUIPMENT_TAG"))
		{
			bool flag = item.GetTagReadonly("POWER_SWITCH_BOOL").GetBool();
			int num = item.GetTagReadonly("CURRENT_CHARGE_INT").GetInt();
			int num2 = item.GetTagReadonly("MAX_CHARGE_INT").GetInt();
			int num3 = item.GetTagReadonly("CHARGE_RATE_INT").GetInt();
			if (flag)
			{
				builder.AddLine("Switched On", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else
			{
				builder.AddLine("Switched Off", ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
			builder.AddLine("Charge Speed: " + num3 + "/Turn", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddLine(" ");
			builder.AddLine("Internal Accumulateur:", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddProgressBar((float)num / (float)num2, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
			builder.AddLine("(" + num + "/" + (float)num2 + ")", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (!flag)
			{
				builder.AddLine("Charging Paused", ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
		}
	}

	public static void CreateComplexeModuleTooltip(RichTextBuilder builder, GameItem item)
	{
		if (!item.IsTag("COMPLEXE_MODULE_TAG"))
		{
			return;
		}
		int num = item.GetTagReadonly("CHARGE_RATE_MODIFIER_INT").GetInt();
		int num2 = item.GetTagReadonly("MAX_CHARGE_MODIFIER_INT").GetInt();
		if (num != 0)
		{
			builder.AddLine("Charge Rate: " + MetaHelper.FormatTooltipInt(num) + "/Turn", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		}
		if (num2 != 0)
		{
			builder.AddLine("Capacitor Size: " + MetaHelper.FormatTooltipInt(num2), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		}
		if (item.IsTag("COMPLEXE_MODULE_ADJACENCY_TAG"))
		{
			int num3 = item.GetTagReadonly("COMPLEXE_MODULE_POWER_ECO_AMOUNT_INT").GetInt();
			int num4 = item.GetTagReadonly("COMPLEXE_MODULE_EFFECTIVENESS_AMOUNT_INT").GetInt();
			builder.AddLine("Adjacency Bonus:", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (num3 != 0)
			{
				builder.AddLine("Power Use Modifier: " + MetaHelper.FormatTooltipInt(num3) + "/Turn", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (num4 != 0)
			{
				builder.AddLine("Effectiveness Modifier: " + MetaHelper.FormatTooltipInt(num4) + "/Turn", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
		}
	}

	public static void CreateComplexeSystemTooltip(RichTextBuilder builder, GameItem item)
	{
		if (!item.IsTag("COMPLEXE_SYSTEM_TAG"))
		{
			return;
		}
		int num = item.GetTagReadonly("POWER_USE_INT").GetInt();
		item.GetTagReadonly("MAX_CHARGE_MODIFIER_INT").GetInt();
		if (num != 0)
		{
			builder.AddLine("Power Use: " + num, ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		}
		GameItem gameItem = GraphUtils.FindParentType<GameItem>(item);
		if (gameItem != null && gameItem.IsTag("COMPLEXE_EQUIPMENT_TAG"))
		{
			if (HasPowerToUseComplexeSystem(item, gameItem))
			{
				builder.AddLine("Ready.", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else
			{
				builder.AddLine("Not Enough Power.", ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
		}
	}
}
