using System;
using UnityEngine;

public class UseCountHelper
{
	public const string USE_COUNT_ITEM_TAG = "USE_COUNT_ITEM_TAG";

	public const string MAX_USE_COUNT_INT = "MAX_USE_COUNT_INT";

	public const string CURRENT_USE_COUNT_INT = "CURRENT_USE_COUNT_INT";

	public const string USE_COUNT_TRANSFERABLE_BOOL = "USE_COUNT_TRANSFERABLE_BOOL";

	public const string USE_COUNT_DESTROY_ON_EMPTY_BOOL = "USE_COUNT_DESTROY_ON_EMPTY_BOOL";

	public const string USE_COUNT_AUTO_VALUE_BOOL = "USE_COUNT_AUTO_VALUE_BOOL";

	public const string USE_COUNT_BASE_VALUE_INT = "USE_COUNT_BASE_VALUE_INT";

	public const string USE_COUNT_VALUE_PER_USE_INT = "USE_COUNT_VALUE_PER_USE_INT";

	public const string USE_COUNT_REFILL_DAYS_INT = "USE_COUNT_REFILL_DAYS_INT";

	public const string USE_COUNT_REFILL_PROGRESS_INT = "USE_COUNT_REFILL_PROGRESS_INT";

	public static void InitUseCountItem(GameItem item, int maxUseCount, bool transferable = true, bool destroyOnEmpty = true, bool useAutoValue = false, int baseValue = 0, int valuePerUse = 0)
	{
		item.ModifyTag("USE_COUNT_ITEM_TAG", (TagState state) =>
		{
			state.Enable().SetBool(val: true);
		});
		item.ModifyTag("MAX_USE_COUNT_INT", (TagState state) =>
		{
			state.Enable().SetInt(maxUseCount);
		});
		item.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
		{
			state.Enable().SetInt(maxUseCount);
		});
		item.ModifyTag("USE_COUNT_TRANSFERABLE_BOOL", (TagState state) =>
		{
			state.Enable().SetBool(transferable);
		});
		item.ModifyTag("USE_COUNT_DESTROY_ON_EMPTY_BOOL", (TagState state) =>
		{
			state.Enable().SetBool(destroyOnEmpty);
		});
		if (useAutoValue)
		{
			item.ModifyTag("USE_COUNT_AUTO_VALUE_BOOL", (TagState state) =>
			{
				state.Enable().SetBool(val: true);
			});
			item.ModifyTag("USE_COUNT_BASE_VALUE_INT", (TagState state) =>
			{
				state.Enable().SetInt(baseValue);
			});
			item.ModifyTag("USE_COUNT_VALUE_PER_USE_INT", (TagState state) =>
			{
				state.Enable().SetInt(valuePerUse);
			});
		}
		if (transferable)
		{
			item.mayThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(item.mayThisTargetItemFunc, (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => s.identifier.Equals(t.identifier)));
			item.canThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(item.canThisTargetItemFunc, (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => s.identifier.Equals(t.identifier)));
		}
		item.onThisTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(item.onThisTargetItemFunc, (Action<GameItem, GameItem>)((GameItem s, GameItem t) =>
		{
			if (s.GetTagReadonly("USE_COUNT_TRANSFERABLE_BOOL").GetBool() && s.identifier.Equals(t.identifier) && GeneralHelper.IsItemOwned(s, t))
			{
				int num = s.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt();
				int num2 = t.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt();
				int num3 = t.GetTagReadonly("MAX_USE_COUNT_INT").GetInt();
				int b = Mathf.Max(0, num3 - num2);
				int num4 = Mathf.Min(num, b);
				int remaining = num - num4;
				int newTargetCurrent = num2 + num4;
				t.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
				{
					state.Enable().SetInt(newTargetCurrent);
				});
				s.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
				{
					state.Enable().SetInt(remaining);
				});
				if (t.GetTagReadonly("USE_COUNT_AUTO_VALUE_BOOL").GetBool())
				{
					int num5 = t.GetTagReadonly("USE_COUNT_BASE_VALUE_INT").GetInt();
					int num6 = t.GetTagReadonly("USE_COUNT_VALUE_PER_USE_INT").GetInt();
					t.unitValue = num5 + newTargetCurrent * num6;
				}
				if (s.GetTagReadonly("USE_COUNT_AUTO_VALUE_BOOL").GetBool())
				{
					int num7 = s.GetTagReadonly("USE_COUNT_BASE_VALUE_INT").GetInt();
					int num8 = s.GetTagReadonly("USE_COUNT_VALUE_PER_USE_INT").GetInt();
					s.unitValue = num7 + remaining * num8;
				}
				if (s.GetTagReadonly("USE_COUNT_DESTROY_ON_EMPTY_BOOL").GetBool() && remaining == 0)
				{
					s.Destroy();
				}
			}
		}));
	}

	public static void Use(GameItem item, int charge = 1)
	{
		if (item.IsTag("USE_COUNT_ITEM_TAG"))
		{
			int num = item.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt();
			int newCharge = Mathf.Max(0, num - charge);
			item.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(newCharge);
			});
			bool flag = item.GetTagReadonly("USE_COUNT_DESTROY_ON_EMPTY_BOOL").GetBool();
			if (item.GetTagReadonly("USE_COUNT_AUTO_VALUE_BOOL").GetBool())
			{
				int num2 = item.GetTagReadonly("USE_COUNT_BASE_VALUE_INT").GetInt();
				int num3 = item.GetTagReadonly("USE_COUNT_VALUE_PER_USE_INT").GetInt();
				item.unitValue = num2 + newCharge * num3;
			}
			if (flag && newCharge <= 0)
			{
				item.Destroy();
			}
		}
	}

	public static bool CanUse(GameItem item, int charge = 1)
	{
		if (item.IsTag("USE_COUNT_ITEM_TAG") && item.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt() >= charge)
		{
			return true;
		}
		return false;
	}

	public static void CreateUseCountItemTooltip(RichTextBuilder builder, GameItem item)
	{
		if (!item.IsTag("USE_COUNT_ITEM_TAG"))
		{
			return;
		}
		int num = item.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt();
		int num2 = item.GetTagReadonly("MAX_USE_COUNT_INT").GetInt();
		if (num2 == 0)
		{
			num2 = 1;
		}
		builder.Add(LocHelper.GetLocalizedMechanic("mech_use_count_use_left"));
		builder.AddProgressBar((float)num / (float)num2, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
		builder.AddLine("(" + num + "/" + (float)num2 + ")", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		if (item.IsTag("USE_COUNT_REFILL_DAYS_INT"))
		{
			int num3 = Mathf.Max(1, item.GetTagReadonly("USE_COUNT_REFILL_DAYS_INT").GetInt());
			builder.AddLine(LocHelper.GetLocalizedMechanic("mech_use_count_refill", num3), ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			if (num < num2)
			{
				int num4 = item.GetTagReadonly("USE_COUNT_REFILL_PROGRESS_INT").GetInt();
				int num5 = Mathf.Max(1, num3 - num4);
				string text = ((num5 == 1) ? LocHelper.GetLocalizedMechanic("mech_use_count_refill_tomorrow") : LocHelper.GetLocalizedMechanic("mech_use_count_refill_next", num5));
				builder.AddLine(text, ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
		}
	}

	public static void InitAutoRefill(GameItem item, int daysPerUse)
	{
		item.ModifyTag("USE_COUNT_REFILL_DAYS_INT", (TagState state) =>
		{
			state.Enable().SetInt(Mathf.Max(1, daysPerUse));
		});
		item.ModifyTag("USE_COUNT_REFILL_PROGRESS_INT", (TagState state) =>
		{
			state.Enable().SetInt(0);
		});
	}

	public static void OnNight()
	{
		foreach (GameItem allItem in EmporiumEntry.Instance.GetAllItems())
		{
			if (allItem == null || !allItem.IsTag("USE_COUNT_ITEM_TAG") || !allItem.IsTag("USE_COUNT_REFILL_DAYS_INT"))
			{
				continue;
			}
			int num = allItem.GetTagReadonly("MAX_USE_COUNT_INT").GetInt();
			if (allItem.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt() >= num)
			{
				allItem.ModifyTag("USE_COUNT_REFILL_PROGRESS_INT", (TagState state) =>
				{
					state.Enable().SetInt(0);
				});
				continue;
			}
			int num2 = Mathf.Max(1, allItem.GetTagReadonly("USE_COUNT_REFILL_DAYS_INT").GetInt());
			int progress = allItem.GetTagReadonly("USE_COUNT_REFILL_PROGRESS_INT").GetInt() + 1;
			if (progress >= num2)
			{
				ReplenishUseCount(allItem, 1);
				progress = 0;
			}
			allItem.ModifyTag("USE_COUNT_REFILL_PROGRESS_INT", (TagState state) =>
			{
				state.Enable().SetInt(progress);
			});
		}
	}

	public static void ModMaxUseCount(GameItem item, int amount)
	{
		if (item.IsTag("USE_COUNT_ITEM_TAG"))
		{
			int num = item.GetTagReadonly("MAX_USE_COUNT_INT").GetInt();
			int b = item.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt();
			int newMax = Mathf.Max(0, num + amount);
			int newCurrent = Mathf.Min(newMax, b);
			item.ModifyTag("MAX_USE_COUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(newMax);
			});
			item.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(newCurrent);
			});
		}
	}

	public static void ReplenishUseCount(GameItem item, int amount)
	{
		if (item.IsTag("USE_COUNT_ITEM_TAG"))
		{
			int a = item.GetTagReadonly("MAX_USE_COUNT_INT").GetInt();
			int num = item.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt();
			int newCurrent = Mathf.Min(a, num + amount);
			item.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(newCurrent);
			});
			int num2 = newCurrent - num;
			if (item.GetTagReadonly("USE_COUNT_AUTO_VALUE_BOOL").GetBool() && num2 > 0)
			{
				int num3 = item.GetTagReadonly("USE_COUNT_BASE_VALUE_INT").GetInt();
				int num4 = item.GetTagReadonly("USE_COUNT_VALUE_PER_USE_INT").GetInt();
				item.unitValue = num3 + newCurrent * num4;
			}
		}
	}

	public static void SetCurrentUseCount(GameItem item, int amount)
	{
		if (item.IsTag("USE_COUNT_ITEM_TAG"))
		{
			int max = item.GetTagReadonly("MAX_USE_COUNT_INT").GetInt();
			int newCurrent = Mathf.Clamp(amount, 0, max);
			item.ModifyTag("CURRENT_USE_COUNT_INT", (TagState state) =>
			{
				state.Enable().SetInt(newCurrent);
			});
			if (item.GetTagReadonly("USE_COUNT_AUTO_VALUE_BOOL").GetBool())
			{
				int num = item.GetTagReadonly("USE_COUNT_BASE_VALUE_INT").GetInt();
				int num2 = item.GetTagReadonly("USE_COUNT_VALUE_PER_USE_INT").GetInt();
				item.unitValue = num + newCurrent * num2;
			}
		}
	}
}
