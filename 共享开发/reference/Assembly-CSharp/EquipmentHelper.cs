using System;
using System.Collections.Generic;

public class EquipmentHelper
{
	public static string EQUIPMENT_TAG = "EQUIPMENT_TAG";

	public static string CHARGE_EFFECT_ENABLED_BOOL = "CHARGE_EFFECT_ENABLED_BOOL";

	public static string EQUIPMENT_DRAW_CURRENT_INT = "EQUIPMENT_DRAW_CURRENT_INT";

	public static string EQUIPMENT_DRAW_TARGET_INT = "EQUIPMENT_DRAW_TARGET_INT";

	public static string EQUIPMENT_DRAW_CONSUMPTION_INT = "EQUIPMENT_DRAW_CONSUMPTION_INT";

	public static string EQUIPMENT_DRAW_CONSUMPTION_BACKUP_INT = "EQUIPMENT_DRAW_CONSUMPTION_BACKUP_INT";

	public static string EQUIPMENT_CHARGE_SPEED_INT = "EQUIPMENT_CHARGE_SPEED_INT";

	public static string EQUIPMENT_CHARGE_SPEED_BACKUP_INT = "EQUIPMENT_CHARGE_SPEED_BACKUP_INT";

	public static string EQUIPMENT_POWER_SWITCH_BOOL = "EQUIPMENT_POWER_SWITCH_BOOL";

	public static string EQUIPMENT_CAN_GET_CHARGE_BOOL = "EQUIPMENT_CAN_GET_CHARGE_BOOL";

	public const string EQUIPMENT_COMBAT_EFFECT = "EQUIPMENT_COMBAT_EFFECT";

	public static void AddCombatEffect(GameItem item, List<string> effects)
	{
		MetaHelper.StoreListInTagSystem(effects, "EQUIPMENT_COMBAT_EFFECT", item);
	}

	public static bool IsEquipmentCharged(GameItem item)
	{
		int num = item.GetTagReadonly(EQUIPMENT_DRAW_CURRENT_INT).GetInt();
		int num2 = item.GetTagReadonly(EQUIPMENT_DRAW_TARGET_INT).GetInt();
		if (num >= num2)
		{
			return true;
		}
		return false;
	}

	public static void ResetEquipmentDrawProgress(GameItem item)
	{
		if (item.IsTag(EQUIPMENT_TAG) && IsEquipmentCharged(item))
		{
			item.ModifyTag(EQUIPMENT_DRAW_CURRENT_INT, (TagState state) =>
			{
				state.Enable().SetInt(0);
			});
		}
	}

	public static void InitEquipment(GameItem item, GameInventory itemInventoryBattery, GameInventory moduleInv, int drawnPower, int targetPower, bool switchOn = false)
	{
		AudioHelper.InitEquipment(item);
		item.ModifyTag(EQUIPMENT_TAG, (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.ModifyTag(CHARGE_EFFECT_ENABLED_BOOL, (TagState state) =>
		{
			state.Enable().SetBool(val: false);
		});
		item.ModifyTag(EQUIPMENT_DRAW_CURRENT_INT, (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.ModifyTag(EQUIPMENT_DRAW_TARGET_INT, (TagState state) =>
		{
			state.Enable().SetInt(targetPower);
		});
		item.ModifyTag(EQUIPMENT_DRAW_CONSUMPTION_INT, (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		item.ModifyTag(EQUIPMENT_DRAW_CONSUMPTION_BACKUP_INT, (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		item.ModifyTag(EQUIPMENT_CHARGE_SPEED_INT, (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		item.ModifyTag(EQUIPMENT_CHARGE_SPEED_BACKUP_INT, (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		if (switchOn)
		{
			item.ModifyTag(EQUIPMENT_POWER_SWITCH_BOOL, (TagState state) =>
			{
				state.Enable().SetBool(val: true);
			});
		}
		else
		{
			item.ModifyTag(EQUIPMENT_POWER_SWITCH_BOOL, (TagState state) =>
			{
				state.Enable().SetBool(val: false);
			});
		}
		item.ModifyTag(EQUIPMENT_CAN_GET_CHARGE_BOOL, (TagState state) =>
		{
			state.Enable().SetBool(val: false);
		});
		item.mayToggleSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
		item.canToggleSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
		((GameSlotInventory)itemInventoryBattery).SetBackgroundFadeSprite("Items/itematlas", "energy_credit");
		ContainerHelper.AllowOnlyTaggedItem(itemInventoryBattery, "power_source_item");
		ContainerHelper.AllowOnlyTaggedItem(moduleInv, "EQUIPMENT_MODULE_TAG");
		GameItem gameItem = item;
		gameItem.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameItem.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem2, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (gameItem2.GetTagReadonly(EQUIPMENT_POWER_SWITCH_BOOL).GetBool() && !IsEquipmentCharged(gameItem2))
			{
				List<GameItem> list = GraphUtils.FindAllChildrenType(itemInventoryBattery, (GameItem gameItem3) => gameItem3.IsTag("power_source_item"));
				if (list.Count > 0)
				{
					int num = list[0].GetTagReadonly("power_source_item_energy").GetInt();
					int num2 = list[0].GetTagReadonly("power_source_item_bandwidth").GetInt();
					int num3 = 0;
					num3 = ((num >= num2) ? num2 : num);
					int current_draw = gameItem2.GetTagReadonly(EQUIPMENT_DRAW_CURRENT_INT).GetInt();
					gameItem2.GetTagReadonly(EQUIPMENT_DRAW_TARGET_INT).GetInt();
					int chargeSpeed = gameItem2.GetTagReadonly(EQUIPMENT_CHARGE_SPEED_INT).GetInt();
					int num4 = gameItem2.GetTagReadonly(EQUIPMENT_DRAW_CONSUMPTION_INT).GetInt();
					gameItem2.GetTagReadonly(EQUIPMENT_DRAW_CONSUMPTION_BACKUP_INT).GetInt();
					int newAvailableEnergy = num;
					if (num3 >= num4)
					{
						newAvailableEnergy = num - num4;
						gameItem2.ModifyTag(EQUIPMENT_DRAW_CURRENT_INT, (TagState state) =>
						{
							state.SetInt(current_draw + chargeSpeed);
						});
						list[0].ModifyTag("power_source_item_energy", (TagState state) =>
						{
							state.SetInt(newAvailableEnergy);
						});
					}
					UpdateCanChargeEquipmentTag(gameItem2, itemInventoryBattery);
				}
			}
		}));
		itemInventoryBattery.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			UpdateCanChargeEquipmentTag(item, itemInventoryBattery);
		};
		itemInventoryBattery.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			UpdateCanChargeEquipmentTag(item, itemInventoryBattery);
		};
		item.onToggleSlotItemFunc = (GameItem gameItem2, GameInventory inventory, SlotMarker _) =>
		{
			bool currentState = !gameItem2.GetTagReadonly(EQUIPMENT_POWER_SWITCH_BOOL).GetBool();
			gameItem2.ModifyTag(EQUIPMENT_POWER_SWITCH_BOOL, (TagState state) =>
			{
				state.Enable().SetBool(currentState);
			});
			UpdateCanChargeEquipmentTag(gameItem2, itemInventoryBattery);
		};
	}

	public static void UpdateCanChargeEquipmentTag(GameItem item, GameInventory itemInventoryBattery)
	{
		if (CanChargeEquipment(item, itemInventoryBattery))
		{
			item.ModifyTag(EQUIPMENT_CAN_GET_CHARGE_BOOL, (TagState state) =>
			{
				state.Enable().SetBool(val: true);
			});
		}
		else
		{
			item.ModifyTag(EQUIPMENT_CAN_GET_CHARGE_BOOL, (TagState state) =>
			{
				state.Enable().SetBool(val: false);
			});
		}
	}

	public static bool CanChargeEquipment(GameItem item, GameInventory itemInventoryBattery)
	{
		List<GameItem> list = GraphUtils.FindAllChildrenType(itemInventoryBattery, (GameItem gameItem) => gameItem.IsTag("power_source_item"));
		if (list.Count <= 0)
		{
			return false;
		}
		int num = item.GetTagReadonly(EQUIPMENT_DRAW_CONSUMPTION_INT).GetInt();
		int num2 = list[0].GetTagReadonly("power_source_item_energy").GetInt();
		int num3 = list[0].GetTagReadonly("power_source_item_bandwidth").GetInt();
		int num4 = 0;
		num4 = ((num2 >= num3) ? num3 : num2);
		if (num <= num4)
		{
			return true;
		}
		return false;
	}

	public static void CreateEquipmentTooltip(RichTextBuilder builder, GameItem item)
	{
		if (item.IsTag(EQUIPMENT_TAG))
		{
			int num = item.GetTagReadonly(EQUIPMENT_DRAW_CURRENT_INT).GetInt();
			int num2 = item.GetTagReadonly(EQUIPMENT_DRAW_TARGET_INT).GetInt();
			int num3 = item.GetTagReadonly(EQUIPMENT_CHARGE_SPEED_INT).GetInt();
			int num4 = item.GetTagReadonly(EQUIPMENT_DRAW_CONSUMPTION_INT).GetInt();
			bool flag = item.GetTagReadonly(EQUIPMENT_POWER_SWITCH_BOOL).GetBool();
			bool flag2 = item.GetTagReadonly(EQUIPMENT_CAN_GET_CHARGE_BOOL).GetBool();
			builder.AddLine("Charge Speed: " + num3 + "/Turn", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddLine("Power Use   : " + num4 + "/Turn", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (flag)
			{
				builder.AddLine("Switched On", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else
			{
				builder.AddLine("Switched Off", ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
			builder.AddLine(" ");
			builder.AddLine("Internal Accumulateur:", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddProgressBar((float)num / (float)num2, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
			builder.AddLine("(" + num + "/" + (float)num2 + ")", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (!flag)
			{
				builder.AddLine("Charging Paused", ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
			else if (num == num2)
			{
				builder.AddLine("Ready", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else if (!flag2)
			{
				builder.AddLine("Insuficient Power To Charge", ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
			else
			{
				builder.AddLine("Charging...", ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
			MachineryHelper.AddTooltipModuleBonus(builder, item);
		}
	}

	public static void EnableCombatEffect(GameItem item)
	{
		if (!item.IsTag(EQUIPMENT_TAG) || item.GetTagReadonly(CHARGE_EFFECT_ENABLED_BOOL).GetBool())
		{
			return;
		}
		foreach (string item2 in MetaHelper.RetrieveListInTagSystem("EQUIPMENT_COMBAT_EFFECT", item))
		{
			item.effects.Add(DirectoryMaster.CombatAbilityEffect(item2));
		}
		item.ModifyTag(CHARGE_EFFECT_ENABLED_BOOL, (TagState state) =>
		{
			state.SetBool(val: true);
		});
	}

	public static void DisableCombatEffect(GameItem item)
	{
		if (!item.IsTag(EQUIPMENT_TAG) || !item.GetTagReadonly(CHARGE_EFFECT_ENABLED_BOOL).GetBool())
		{
			return;
		}
		foreach (string item2 in MetaHelper.RetrieveListInTagSystem("EQUIPMENT_COMBAT_EFFECT", item))
		{
			item.effects.Remove(DirectoryMaster.CombatAbilityEffect(item2));
		}
		item.ModifyTag(CHARGE_EFFECT_ENABLED_BOOL, (TagState state) =>
		{
			state.SetBool(val: false);
		});
	}

	public static void SwitchOnEquipment(GameItem item)
	{
		item.ModifyTag(EQUIPMENT_POWER_SWITCH_BOOL, (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
	}

	public static void SwitchOffEquipment(GameItem item)
	{
		item.ModifyTag(EQUIPMENT_POWER_SWITCH_BOOL, (TagState state) =>
		{
			state.Enable().SetBool(val: false);
		});
	}
}
