using System;
using System.Collections.Generic;
using System.Linq;

public class ArmorHelper
{
	public const string ARMOR_TAG = "armor_item";

	public const string DEFAULT_ARMOR_VALUE = "default_armor_value";

	public const string CURRENT_MAX_ARMOR_VALUE = "current_max_armor_value";

	public const string CURRENT_ARMOR_VALUE = "current_armor_value";

	public const string ARMOR_EFFICIENCY = "armor_efficiency";

	public const string ARMOR_MITIGATION = "armor_mitigation";

	public const string ARMOR_PROTECTION = "armor_protection";

	public const string ARMOR_RESILIENCE = "armor_resilience";

	public const string ARMOR_ACCURACY_MODIFIER = "armor_accuracy_modifier";

	public const string ARMOR_DODGE_MODIFIER = "armor_dodge_modifier";

	public static int DistributeDamage(List<GameItem> allArmorItem, int damage, GameCharacterItem entity)
	{
		int num = damage * -1;
		int absorbedDamdage = 0;
		for (bool flag = true; flag && num > 0; flag = AnyArmorValueRemain(allArmorItem))
		{
			foreach (GameItem item in allArmorItem)
			{
				int armorValue = item.GetTagReadonly("current_armor_value").GetInt();
				if (armorValue > 0 && num > 0)
				{
					if (!RNG.Roll(item.GetTagReadonly("armor_mitigation").GetInt()))
					{
						item.ModifyTag("current_armor_value", (TagState state) =>
						{
							state.SetInt(armorValue - 1);
						});
					}
					num--;
					absorbedDamdage++;
				}
				if (num <= 0)
				{
					return 0;
				}
			}
		}
		if (absorbedDamdage != 0)
		{
			ActionQueue.current.Add(new EmptyAction(0.20000000298023224).SetBeginFunc(() =>
			{
				CombatLog.log(entity.name + "'s armor was able to defend against " + absorbedDamdage + " damage!");
				SplashTextManager.Instance.DisplayFloatText(entity, (-absorbedDamdage).ToString(), SplashTextManager.TextMode.ArmorDamage);
			}));
		}
		return num;
	}

	public static int DistributeDamageWithEfficiency(List<GameItem> allArmorItem, int damage, GameCharacterItem entity)
	{
		int num = damage * -1;
		int absorbedDamdage = 0;
		allArmorItem.OrderBy((GameItem item) => item.GetTagReadonly("armor_efficiency").GetInt()).ToList();
		foreach (GameItem item in allArmorItem)
		{
			int num2 = item.GetTagReadonly("current_armor_value").GetInt();
			int num3 = item.GetTagReadonly("armor_resilience").GetInt();
			if (0 >= num2 && num3 > 0)
			{
				int num4 = 0;
				num4 = ((num <= num3) ? num : num3);
				num -= num4;
				absorbedDamdage += num4;
			}
		}
		foreach (GameItem item2 in allArmorItem)
		{
			int num5 = item2.GetTagReadonly("armor_protection").GetInt();
			int armorValue = item2.GetTagReadonly("current_armor_value").GetInt();
			if (num5 > 0 || num < 1)
			{
				int maxBlockingDamage = 0;
				if (num > num5)
				{
					maxBlockingDamage = num5;
				}
				else
				{
					maxBlockingDamage = num;
				}
				if (armorValue <= maxBlockingDamage)
				{
					num5 = armorValue;
				}
				num -= maxBlockingDamage;
				absorbedDamdage += maxBlockingDamage;
				item2.ModifyTag("current_armor_value", (TagState state) =>
				{
					state.SetInt(armorValue - maxBlockingDamage);
				});
				if (num < 1)
				{
					break;
				}
			}
		}
		for (int num6 = 0; num6 <= num; num6++)
		{
			foreach (GameItem item3 in allArmorItem)
			{
				int armorValue2 = item3.GetTagReadonly("current_armor_value").GetInt();
				int chance = item3.GetTagReadonly("armor_efficiency").GetInt();
				if (armorValue2 <= 0 || num <= 0 || !RNG.Roll(chance))
				{
					continue;
				}
				if (!RNG.Roll(item3.GetTagReadonly("armor_mitigation").GetInt()))
				{
					item3.ModifyTag("current_armor_value", (TagState state) =>
					{
						state.SetInt(armorValue2 - 1);
					});
				}
				num--;
				absorbedDamdage++;
				break;
			}
		}
		if (absorbedDamdage != 0)
		{
			ActionQueue.current.Add(new EmptyAction(0.20000000298023224).SetBeginFunc(() =>
			{
				CombatLog.log(entity.name + "'s armor was able to defend against " + absorbedDamdage + " damage!");
				SplashTextManager.Instance.DisplayFloatText(entity, (-absorbedDamdage).ToString(), SplashTextManager.TextMode.ArmorDamage);
			}));
		}
		return num;
	}

	public static bool AnyArmorValueRemain(List<GameItem> allArmorItem)
	{
		bool result = false;
		foreach (GameItem item in allArmorItem)
		{
			if (item.GetTagReadonly("current_armor_value").GetInt() > 0)
			{
				result = true;
			}
		}
		return result;
	}

	public static void CreateArmorItemTooltip(RichTextBuilder builder, GameItem item)
	{
		if (!item.IsTag("armor_item"))
		{
			return;
		}
		int num = item.GetTagReadonly("current_armor_value").GetInt();
		int num2 = item.GetTagReadonly("current_max_armor_value").GetInt();
		int num3 = item.GetTagReadonly("armor_efficiency").GetInt();
		int num4 = item.GetTagReadonly("armor_mitigation").GetInt();
		int num5 = item.GetTagReadonly("armor_protection").GetInt();
		int num6 = item.GetTagReadonly("armor_resilience").GetInt();
		int num7 = item.GetTagReadonly("armor_accuracy_modifier").GetInt();
		int num8 = item.GetTagReadonly("armor_dodge_modifier").GetInt();
		if (num2 > 0)
		{
			builder.AddLine("Efficiency: " + num3 + "%", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (num4 != 0)
			{
				builder.AddLine("Mitigation: " + num4 + "%", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (num5 != 0)
			{
				builder.AddLine("Protection: " + num5, ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (num6 != 0)
			{
				builder.AddLine("Resilience: " + num6, ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			if (num7 != 0)
			{
				builder.AddLine("Accuracy  : " + num7, ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
			if (num8 != 0)
			{
				builder.AddLine("Dodge     : " + num8, ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
			builder.Add(" Armor: ", RenderHandler.ColorPalette.LightCyan);
			builder.AddProgressBar((float)num / (float)num2, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
			builder.AddLine("(" + num + "/" + (float)num2 + ")", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		}
	}

	public static void RepairArmorWithoutLoss(GameItem item)
	{
		int maxArmor = item.GetTagReadonly("current_max_armor_value").GetInt();
		item.ModifyTag("current_armor_value", (TagState state) =>
		{
			state.SetInt(maxArmor);
		});
	}

	public static void RepairArmorWithLoss(GameItem item)
	{
		int maxArmor = item.GetTagReadonly("current_max_armor_value").GetInt();
		if (maxArmor > 0)
		{
			item.ModifyTag("current_max_armor_value", (TagState state) =>
			{
				state.SetInt(maxArmor - 1);
			});
			item.ModifyTag("current_armor_value", (TagState state) =>
			{
				state.SetInt(maxArmor - 1);
			});
		}
	}

	public static void RepairArmorWithAmount(GameItem item, int repairAmount)
	{
		int num = item.GetTagReadonly("current_max_armor_value").GetInt();
		int num2 = item.GetTagReadonly("current_armor_value").GetInt();
		int newArmor = num2 + repairAmount;
		if (newArmor > num)
		{
			newArmor = num;
		}
		if (num > 0)
		{
			item.ModifyTag("current_armor_value", (TagState state) =>
			{
				state.SetInt(newArmor);
			});
		}
	}

	public static void InitArmorItem(GameItem item, int defaultArmorValue, int efficiency, int mitigation, int protection, int resilience = 0, int accuracyModifier = 0, int dodgeModifier = 0)
	{
		item.EnableTag("armor_item");
		item.ModifyTag("default_armor_value", (TagState state) =>
		{
			state.Enable().SetInt(defaultArmorValue);
		});
		item.ModifyTag("current_max_armor_value", (TagState state) =>
		{
			state.Enable().SetInt(defaultArmorValue);
		});
		item.ModifyTag("current_armor_value", (TagState state) =>
		{
			state.Enable().SetInt(defaultArmorValue);
		});
		item.ModifyTag("armor_efficiency", (TagState state) =>
		{
			state.Enable().SetInt(efficiency);
		});
		item.ModifyTag("armor_mitigation", (TagState state) =>
		{
			state.Enable().SetInt(mitigation);
		});
		item.ModifyTag("armor_protection", (TagState state) =>
		{
			state.Enable().SetInt(protection);
		});
		item.ModifyTag("armor_resilience", (TagState state) =>
		{
			state.Enable().SetInt(resilience);
		});
		item.ModifyTag("armor_accuracy_modifier", (TagState state) =>
		{
			state.Enable().SetInt(accuracyModifier);
		});
		item.ModifyTag("armor_dodge_modifier", (TagState state) =>
		{
			state.Enable().SetInt(dodgeModifier);
		});
		item.SetType("armor_item");
		item.SetGameItemType("ARMOR");
		item.onSlotAddItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item.onSlotAddItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem i, GameInventory _, SlotMarker _) =>
		{
			if (GraphUtils.FindParentType<GameItem>(i) is GameCharacterItem gameCharacterItem)
			{
				gameCharacterItem.accuracyPercentage += accuracyModifier;
				gameCharacterItem.dodge += dodgeModifier;
			}
		}));
		item.onSlotRemoveItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item.onSlotRemoveItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem i, GameInventory _, SlotMarker _) =>
		{
			if (GraphUtils.FindParentType<GameItem>(i) is GameCharacterItem gameCharacterItem)
			{
				gameCharacterItem.accuracyPercentage -= accuracyModifier;
				gameCharacterItem.dodge -= dodgeModifier;
			}
		}));
	}

	public static void ModifyArmor(GameItem item, int removeArmorValue, int removeMaxArmorValue)
	{
		int currentArmor = item.GetTagReadonly("current_armor_value").GetInt();
		int maxArmor = item.GetTagReadonly("current_max_armor_value").GetInt();
		item.ModifyTag("current_max_armor_value", (TagState state) =>
		{
			state.Enable().SetInt(maxArmor - removeMaxArmorValue);
		});
		item.ModifyTag("current_armor_value", (TagState state) =>
		{
			state.Enable().SetInt(currentArmor - removeArmorValue);
		});
	}

	public static int GetArmorItemValue(GameItem item)
	{
		if (item.IsTag("armor_item"))
		{
			int num = item.GetTagReadonly("current_armor_value").GetInt();
			int num2 = item.GetTagReadonly("current_max_armor_value").GetInt();
			int num3 = item.GetTagReadonly("armor_efficiency").GetInt();
			return (num + num2) * num3 / 100;
		}
		return 0;
	}
}
