using System;
using System.Collections.Generic;

public class MachineryHelper
{
	public const string MACHINERY_TAG = "machinery";

	public const string MACHINERY_DRAWN_POWER = "machinery_drawn_power";

	public const string MACHINERY_DRAWN_POWER_BASE = "MACHINERY_DRAWN_POWER_BASE";

	public const string IS_MACHINERY_POWERED = "is_machinery_powered";

	public const string IS_MACHINERY_RECEIVED_POWER = "machinery_received_power";

	public const string WILL_MACHINERY_RECEIVED_POWER = "WILL_MACHINERY_RECEIVED_POWER";

	public const string IS_DRAW_TYPE_MACHINE = "IS_DRAW_TYPE_MACHINE";

	public const string DRAW_CURRENT_INT = "DRAW_CURRENT_INT";

	public const string DRAW_TARGET_INT = "DRAW_TARGET_INT";

	public const string DRAW_CONSUMPTION_BACKUP_INT = "DRAW_CONSUMPTION_BACKUP_INT";

	public const string IS_PROCESSING_TYPE_MACHINE = "IS_PROCESSING_TYPE_MACHINE";

	public const string IS_ACTIVE_PROCESSING_BOOL = "IS_ACTIVE_PROCESSING_BOOL";

	public const string PROCESSING_CURRENT_PROGRESS_INT = "PROCESSING_CURRENT_PROGRESS_INT";

	public const string PROCESSING_MAX_PROGRESS_INT = "PROCESSING_MAX_PROGRESS_INT";

	public const string PROCESSING_PROGRESS_SPEED_INT = "PROCESSING_PROGRESS_SPEED_INT";

	public const string PROCESSING_PROGRESS_SPEED_BASE_INT = "PROCESSING_PROGRESS_SPEED_BASE_INT";

	public const string IS_BATTERY_POWERED_TAG = "IS_BATTERY_POWERED_TAG";

	public const string LOW_POWER_MODE_BOOL = "LOW_POWER_MODE_BOOL";

	public static bool IsMachineryToggleOn(GameItem item)
	{
		if (item.IsTag("machinery") && item.GetTagReadonly("is_machinery_powered").GetBool())
		{
			return true;
		}
		return false;
	}

	public static void UpdateProcessingTypeMachine(GameItem item)
	{
		if (!item.IsTag("IS_PROCESSING_TYPE_MACHINE"))
		{
			return;
		}
		if (!CanMachineOutput(item))
		{
			ConsumePoweredTag(item);
			return;
		}
		ConsumePoweredTag(item);
		int num = item.GetTagReadonly("PROCESSING_CURRENT_PROGRESS_INT").GetInt();
		int num2 = item.GetTagReadonly("PROCESSING_MAX_PROGRESS_INT").GetInt();
		int num3 = item.GetTagReadonly("PROCESSING_PROGRESS_SPEED_INT").GetInt();
		int newProgress = num + num3;
		if (newProgress > num2)
		{
			newProgress = num2;
		}
		if (num < num2)
		{
			item.ModifyTag("PROCESSING_CURRENT_PROGRESS_INT", (TagState state) =>
			{
				state.SetInt(newProgress);
			});
		}
	}

	public static bool IsProcessingActive(GameItem item)
	{
		if (item.IsTag("IS_PROCESSING_TYPE_MACHINE"))
		{
			return item.GetTagReadonly("IS_ACTIVE_PROCESSING_BOOL").GetBool();
		}
		return false;
	}

	private List<GameItem> GetAllMachineryInBase()
	{
		List<GameItem> list = GraphUtils.FindAllChildrenType<GameItem>(GameMaster.current.shipManager.ship);
		List<GameItem> list2 = new List<GameItem>();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num].IsTag("machinery"))
			{
				list2.Add(list[num]);
			}
		}
		return list2;
	}

	public static List<GameItem> GetAllPoweredOnMachinery(bool isFromShip = true)
	{
		List<GameItem> list = ((!isFromShip) ? GameMaster.current.raidManager.GetAllGameItemExcludeOnShip() : GraphUtils.FindAllChildrenType<GameItem>(GameMaster.current.shipManager.ship));
		List<GameItem> list2 = new List<GameItem>();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num].IsTag("machinery") && list[num].GetTagReadonly("is_machinery_powered").GetBool())
			{
				list2.Add(list[num]);
			}
		}
		return list2;
	}

	public static void InitMachinery(GameItem item, int drawnPower = 0, bool isTogglable = false)
	{
		item.SetGameItemType("MACHINE");
		item.EnableTag("machinery");
		item.SetType("machinery");
		item.ModifyTag("machinery_drawn_power", (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		item.ModifyTag("MACHINERY_DRAWN_POWER_BASE", (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		item.ModifyTag("is_machinery_powered", (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.ModifyTag("machinery_received_power", (TagState state) =>
		{
			state.Enable().SetBool(val: false);
		});
		item.EnableTag("IMPORTANT_TAG");
		AudioHelper.InitMetalMachine(item);
		if (isTogglable)
		{
			item.mayToggleSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
			item.canToggleSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
		}
		item.onToggleSlotItemFunc = (GameItem gameItem, GameInventory inventory, SlotMarker _) =>
		{
			bool currentState = !gameItem.GetTagReadonly("is_machinery_powered").GetBool();
			gameItem.ModifyTag("is_machinery_powered", (TagState state) =>
			{
				state.Enable().SetBool(currentState);
			});
		};
	}

	public static void InitProgressTypeMachinery(GameItem item, int drawnPower = 0, int progressSpeed = 1, bool isBatteryPowered = false)
	{
		InitMachinery(item, drawnPower);
		if (isBatteryPowered)
		{
			item.EnableTag("IS_BATTERY_POWERED_TAG");
		}
		item.EnableTag("IS_PROCESSING_TYPE_MACHINE");
		item.ModifyTag("IS_ACTIVE_PROCESSING_BOOL", (TagState state) =>
		{
			state.Enable().SetBool(val: false);
		});
		item.ModifyTag("PROCESSING_CURRENT_PROGRESS_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.ModifyTag("PROCESSING_MAX_PROGRESS_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.ModifyTag("PROCESSING_PROGRESS_SPEED_INT", (TagState state) =>
		{
			state.Enable().SetInt(progressSpeed);
		});
		item.ModifyTag("PROCESSING_PROGRESS_SPEED_BASE_INT", (TagState state) =>
		{
			state.Enable().SetInt(progressSpeed);
		});
		item.EnableTag("IS_PROCESSING_TYPE_MACHINE");
		item.ModifyTag("LOW_POWER_MODE_BOOL", (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem item2, GameInventory parentInventory, SlotMarker slot) =>
		{
			UpdateProcessingTypeMachine(item2);
		}));
	}

	public static int GetMachinePowerUsage(GameItem gameItem)
	{
		return gameItem.GetTagReadonly("machinery_drawn_power").GetInt();
	}

	public static void InitDrawTypeMachinery(GameItem item, int drawnPower = 0, int targetPower = 100, bool slotedCheck = false)
	{
		InitMachinery(item, drawnPower);
		item.EnableTag("IS_DRAW_TYPE_MACHINE");
		item.ModifyTag("DRAW_CURRENT_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
		item.ModifyTag("DRAW_TARGET_INT", (TagState state) =>
		{
			state.Enable().SetInt(targetPower);
		});
		item.ModifyTag("DRAW_CONSUMPTION_BACKUP_INT", (TagState state) =>
		{
			state.Enable().SetInt(drawnPower);
		});
		item.onTurnEndSlotItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item.onTurnEndSlotItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (!slotedCheck || ShipHelper.IsSloted(gameItem))
			{
				if (!CanMachineOutput(gameItem))
				{
					ConsumePoweredTag(gameItem);
				}
				else
				{
					ConsumePoweredTag(gameItem);
					int num = gameItem.GetTagReadonly("DRAW_CURRENT_INT").GetInt();
					int num2 = gameItem.GetTagReadonly("DRAW_TARGET_INT").GetInt();
					int num3 = gameItem.GetTagReadonly("machinery_drawn_power").GetInt();
					int backup_power_consumption = gameItem.GetTagReadonly("DRAW_CONSUMPTION_BACKUP_INT").GetInt();
					if (num >= num2)
					{
						gameItem.ModifyTag("machinery_drawn_power", (TagState state) =>
						{
							state.SetInt(0);
						});
					}
					else
					{
						gameItem.ModifyTag("machinery_drawn_power", (TagState state) =>
						{
							state.SetInt(backup_power_consumption);
						});
						int newCurrent = num + num3;
						if (newCurrent >= num2)
						{
							newCurrent = num2;
							gameItem.ModifyTag("machinery_drawn_power", (TagState state) =>
							{
								state.SetInt(0);
							});
						}
						gameItem.ModifyTag("DRAW_CURRENT_INT", (TagState state) =>
						{
							state.SetInt(newCurrent);
						});
					}
				}
			}
		}));
	}

	public static bool IsDrawSystemCharged(GameItem item)
	{
		if (!item.IsTag("IS_DRAW_TYPE_MACHINE"))
		{
			return false;
		}
		int num = item.GetTagReadonly("DRAW_CURRENT_INT").GetInt();
		int num2 = item.GetTagReadonly("DRAW_TARGET_INT").GetInt();
		if (num >= num2)
		{
			return true;
		}
		return false;
	}

	public static bool CanMachineOutput(GameItem item)
	{
		if (item.IsTag("machinery") && item.GetTagReadonly("is_machinery_powered").GetBool() && item.GetTagReadonly("machinery_received_power").GetBool())
		{
			return true;
		}
		return false;
	}

	public static void ConsumePoweredTag(GameItem item)
	{
		item.ModifyTag("machinery_received_power", (TagState state) =>
		{
			state.Enable().SetBool(val: false);
		});
	}

	public static void ResetProgress(GameItem item)
	{
		if (item.IsTag("IS_DRAW_TYPE_MACHINE"))
		{
			item.ModifyTag("DRAW_CURRENT_INT", (TagState state) =>
			{
				state.SetInt(0);
			});
		}
	}

	private static void CreatePowerInsufficientTooltip(RichTextBuilder builder, GameItem machine, int draw)
	{
		GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(machine);
		if (batterySlot != null)
		{
			GameItem childItem = batterySlot.childItem;
			if ((childItem == null || childItem.IsTag("power_source_item")) && (childItem == null || !((!CurrentConverterHelper.IsCurrentConverter(childItem)) ? PowerHelper.CanDrawPowerSource(childItem, draw) : (CurrentConverterHelper.IsOn(childItem) && !CurrentConverterHelper.IsBlackedOut() && CurrentConverterHelper.IsServiceActive()))))
			{
				builder.AddLine(LocHelper.GetLocalizedMechanicOrDefault("mech_machinery_power_insufficient", "INSUFFICIENT ENERGY"), ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
		}
	}

	public static void CreateMachineryTooltip(RichTextBuilder builder, GameItem item)
	{
		if ((!item.IsTag("machinery") && !item.IsTag("machinery_drawn_power")) || item.IsTag("BROKEN_MACHINE_TAG"))
		{
			return;
		}
		builder.AddLine(" ");
		int num = item.GetTagReadonly("machinery_drawn_power").GetInt();
		int num2 = item.GetTagReadonly("MACHINERY_DRAWN_POWER_BASE").GetInt();
		if (num > 0)
		{
			builder.Add(LocHelper.GetLocalizedMechanic("mech_machinery_required_energy"), RenderHandler.ColorPalette.LightCyan);
			if (num > num2)
			{
				builder.Add(LocHelper.GetLocalizedMechanic("mech_machinery_per_day", num), RenderHandler.ColorPalette.LightRed);
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_per_day_base", num2), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else if (num < num2)
			{
				builder.Add(LocHelper.GetLocalizedMechanic("mech_machinery_per_day", num), RenderHandler.ColorPalette.LightGreen);
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_per_day_base", num2), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else
			{
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_per_day", num), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			CreatePowerInsufficientTooltip(builder, item, num);
		}
		if (item.IsTag("PROGRESS_TYPE_MACHINE_TAG"))
		{
			int num3 = item.GetTagReadonly("BASE_PROCESSING_SPEED_TAG").GetInt();
			int num4 = item.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG").GetInt();
			builder.Add(LocHelper.GetLocalizedMechanic("mech_machinery_progress_speed"), RenderHandler.ColorPalette.LightCyan);
			if (num3 < num4)
			{
				builder.Add(LocHelper.GetLocalizedMechanic("mech_machinery_per_day", num4), RenderHandler.ColorPalette.LightGreen);
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_per_day_base", num3), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else if (num3 > num4)
			{
				builder.Add(LocHelper.GetLocalizedMechanic("mech_machinery_per_day", num4), RenderHandler.ColorPalette.LightRed);
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_per_day_base", num3), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else
			{
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_per_day", num4), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
		}
		if (item.IsTag("IS_DRAW_TYPE_MACHINE"))
		{
			int num5 = item.GetTagReadonly("DRAW_CURRENT_INT").GetInt();
			int num6 = item.GetTagReadonly("DRAW_TARGET_INT").GetInt();
			bool flag = item.GetTagReadonly("is_machinery_powered").GetBool();
			bool flag2 = item.GetTagReadonly("WILL_MACHINERY_RECEIVED_POWER").GetBool();
			builder.AddLine(" ");
			builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_accumulator"), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddProgressBar((float)num5 / (float)num6, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
			builder.AddLine("(" + num5 + "/" + (float)num6 + ")", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (!flag)
			{
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_charging_paused"), ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
			else if (num5 == num6)
			{
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_ready"), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			else if (!flag2)
			{
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_insufficient_power"), ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
			else
			{
				builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_charging"), ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
			}
		}
		if (item.IsTag("IS_PROCESSING_TYPE_MACHINE") && item.GetTagReadonly("IS_ACTIVE_PROCESSING_BOOL").GetBool())
		{
			int num7 = item.GetTagReadonly("PROCESSING_CURRENT_PROGRESS_INT").GetInt();
			int num8 = item.GetTagReadonly("PROCESSING_MAX_PROGRESS_INT").GetInt();
			builder.AddLine(" ");
			builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_progress"), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddProgressBar((float)num7 / (float)num8, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
			builder.AddLine("(" + num7 + "/" + (float)num8 + ")", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		}
		if (item.GetTagReadonly("LOW_POWER_MODE_BOOL").GetBool())
		{
			builder.AddLine(LocHelper.GetLocalizedMechanic("mech_machinery_low_power"), ignoreEmpty: true, RenderHandler.ColorPalette.LightGreen);
		}
		AddTooltipModuleBonus(builder, item);
	}

	public static void AddTooltipModuleBonus(RichTextBuilder builder, GameItem item, string performanceStr = null, string efficiencyStr = null, string qualityStr = null)
	{
		if (performanceStr == null)
		{
			performanceStr = LocHelper.GetLocalizedMechanic("mech_module_stat_performance");
		}
		if (efficiencyStr == null)
		{
			efficiencyStr = LocHelper.GetLocalizedMechanic("mech_module_stat_efficiency");
		}
		if (qualityStr == null)
		{
			qualityStr = LocHelper.GetLocalizedMechanic("mech_module_stat_quality");
		}
		if (item.GetTagReadonly("IS_RECEIVING_BONUS_FROM_MODULE_BOOL").GetBool())
		{
			int num = item.GetTagReadonly("TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT").GetInt();
			int num2 = item.GetTagReadonly("TOTAL_PERCENTAGE_POWER_BONUS_INT").GetInt();
			int num3 = item.GetTagReadonly("TOTAL_PERCENTAGE_QUALITY_BONUS_INT").GetInt();
			int num4 = item.GetTagReadonly("MACHINE_PERFORMANCE_RATIO_INT").GetInt();
			int num5 = item.GetTagReadonly("MACHINE_QUALITY_RATIO_INT").GetInt();
			string text = "";
			string text2 = "";
			string text3 = "";
			RenderHandler.ColorPalette color = RenderHandler.ColorPalette.LightCyan;
			RenderHandler.ColorPalette color2 = RenderHandler.ColorPalette.LightCyan;
			RenderHandler.ColorPalette color3 = RenderHandler.ColorPalette.LightCyan;
			if (num >= 0)
			{
				text = "+" + num;
			}
			else
			{
				color = RenderHandler.ColorPalette.LightOrange;
				text = "-" + -1 * num;
			}
			if (num2 >= 0)
			{
				text2 = "+" + num2;
			}
			else
			{
				text2 = "-" + -1 * num2;
				color2 = RenderHandler.ColorPalette.LightOrange;
			}
			if (num3 >= 0)
			{
				text3 = "+" + num3;
			}
			else
			{
				text3 = "-" + -1 * num3;
				color3 = RenderHandler.ColorPalette.LightOrange;
			}
			string text4 = "";
			if (item.IsTag("MACHINE_PERFORMANCE_RATIO_INT"))
			{
				text4 = " (" + LocHelper.GetLocalizedMechanic("mech_ratio") + " " + 1f / (float)num4 + ")";
			}
			string text5 = "";
			if (item.IsTag("MACHINE_QUALITY_RATIO_INT"))
			{
				text5 = " (" + LocHelper.GetLocalizedMechanic("mech_ratio") + " " + 1f / (float)num5 + ")";
			}
			builder.AddLine(" ", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			builder.AddLine(LocHelper.GetLocalizedMechanic("mech_effect_from_module"), ignoreEmpty: true, RenderHandler.ColorPalette.Cyan);
			if (num != 0)
			{
				builder.AddLine(LocHelper.BulletLine(performanceStr + ": " + text + "%" + text4), ignoreEmpty: true, color);
			}
			if (num2 != 0)
			{
				builder.AddLine(LocHelper.BulletLine(efficiencyStr + ": " + text2 + "%"), ignoreEmpty: true, color2);
			}
			if (num3 != 0)
			{
				builder.AddLine(LocHelper.BulletLine(qualityStr + ": " + text3 + "%" + text5), ignoreEmpty: true, color3);
			}
		}
	}

	public static bool IsStartingProcess(GameItem item, string[] tags)
	{
		foreach (string tagName in tags)
		{
			if (item.IsTag(tagName))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsLowPowerMode(GameItem item)
	{
		if (item.GetTagReadonly("LOW_POWER_MODE_BOOL").GetBool())
		{
			return true;
		}
		return false;
	}

	public static void UpdatePowerUsageProcessTypeMachine(GameItem item)
	{
		CheckLowPowerMode(item);
	}

	public static void CheckLowPowerMode(GameItem item)
	{
		if (IsLowPowerMode(item))
		{
			item.ModifyTag("machinery_drawn_power", (TagState state) =>
			{
				state.SetInt(0);
			});
		}
		else
		{
			ModuleHelper.ApplyModifiedPower(item);
		}
	}

	public static void CreateTrashPileTooltip(RichTextBuilder builder, GameItem item)
	{
		if (item.identifier.Equals("trash_pile"))
		{
			if (item.IsTag("PILE_SEARCHED_TODAY"))
			{
				builder.AddLine("I am too tired to search it again today", ignoreEmpty: true, RenderHandler.ColorPalette.Red);
			}
			else
			{
				builder.AddLine("I have the energy to search it today.", ignoreEmpty: true, RenderHandler.ColorPalette.Green);
			}
		}
	}

	public static int GetCurrentQualityBonus(GameItem gameItem)
	{
		return gameItem.GetTagReadonly("TOTAL_PERCENTAGE_QUALITY_BONUS_INT").GetInt();
	}

	public static int GetCurrentPerformanceBonus(GameItem gameItem)
	{
		return gameItem.GetTagReadonly("TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT").GetInt();
	}
}
