using System;
using System.Collections.Generic;

public class StationMachinery : ItemDirectory
{
	protected override void InitDirectory()
	{
	}

	private GameItem CreateWaterFountain(bool isPaying = false)
	{
		(PixelWindow, GameSlotInventory, GameSlotInventory, ButtonElement, ButtonElement) tuple = DirectoryUtils.CreateWaterFountainWindow();
		PixelWindow item = tuple.Item1;
		GameSlotInventory itemInventoryLeft = tuple.Item2;
		GameSlotInventory itemInventoryRight = tuple.Item3;
		ButtonElement fillButton = tuple.Item4;
		ButtonElement drinkButton = tuple.Item5;
		GameItem gameItem = Empty().SetContentWindow(item).SetSpriteAndShape("Items/items_machine", "automatic_meat_processor");
		itemInventoryLeft.SetBackgroundFadeSprite("Items/items_food", "water_bottle_full_premium");
		itemInventoryRight.SetBackgroundFadeSprite("Items/itematlas", "energy_credit");
		gameItem.mayInventoryRemoveItemFunc = (GameItem _, GameInventory _) => false;
		fillButton.SetColor(ButtonElement.Color.Red);
		drinkButton.SetColor(ButtonElement.Color.Red);
		GameSlotInventory gameSlotInventory = itemInventoryLeft;
		gameSlotInventory.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameSlotInventory.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			StationMachinerieHelper.WaterFountainUpdateFillButton(fillButton, itemInventoryLeft, itemInventoryRight);
		}));
		GameSlotInventory gameSlotInventory2 = itemInventoryLeft;
		gameSlotInventory2.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameSlotInventory2.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			fillButton.SetColor(ButtonElement.Color.Red);
		}));
		GameSlotInventory gameSlotInventory3 = itemInventoryRight;
		gameSlotInventory3.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameSlotInventory3.onSlotAddItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			StationMachinerieHelper.WaterFountainUpdateFillButton(fillButton, itemInventoryLeft, itemInventoryRight);
			StationMachinerieHelper.WaterFountainUpdateDrinkButton(drinkButton, itemInventoryRight);
		}));
		GameSlotInventory gameSlotInventory4 = itemInventoryRight;
		gameSlotInventory4.onSlotRemoveItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameSlotInventory4.onSlotRemoveItemEndFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			fillButton.SetColor(ButtonElement.Color.Red);
			drinkButton.SetColor(ButtonElement.Color.Red);
		}));
		if (isPaying)
		{
			gameItem.name = "Paying Water Fountain";
		}
		else
		{
			gameItem.name = "Water Fountain";
		}
		ContainerHelper.AllowOnlyTaggedItem(itemInventoryLeft, "LIQUID_CONTAINER_TAG");
		ContainerHelper.AllowOnlyTaggedItem(itemInventoryRight, "power_source_item");
		int mlPerEnergy = 100;
		fillButton.SetCallback(() =>
		{
			List<GameItem> list = GraphUtils.FindAllChildrenType(itemInventoryLeft, (GameItem gameItem2) => gameItem2.IsTag("LIQUID_CONTAINER_TAG"));
			List<GameItem> list2 = GraphUtils.FindAllChildrenType(itemInventoryRight, (GameItem gameItem2) => gameItem2.IsTag("power_source_item"));
			if (list.Count != 0 && list2.Count != 0)
			{
				int num = list2[0].GetTagReadonly("power_source_item_energy").GetInt();
				int num2 = list[0].GetTagReadonly("LIQUID_CONTAINER_MAX").GetInt() - list[0].GetTagReadonly("LIQUID_CONTAINER_CURRENT").GetInt();
				int num3 = mlPerEnergy * num;
				int num4 = 0;
				int num5 = 0;
				if (num3 > num2)
				{
					num4 = num2;
					num5 = num2 / mlPerEnergy;
				}
				else
				{
					num4 = num3;
					num5 = num;
				}
				PowerHelper.TryRemoveEnergy(list2[0], num5, useBandwidth: false);
				LiquidContainerHelper.AddLiquid(list[0], num4);
			}
		});
		drinkButton.SetCallback(() =>
		{
			List<GameCharacterItem> allPlayerCharacters = GameMaster.current.raidManager.GetAllPlayerCharacters();
			List<GameItem> list = GraphUtils.FindAllChildrenType(itemInventoryRight, (GameItem gameItem2) => gameItem2.IsTag("power_source_item"));
			if (allPlayerCharacters.Count != 0 && list.Count != 0)
			{
				int num = 0;
				foreach (GameCharacterItem item2 in allPlayerCharacters)
				{
					num += item2.maxThirst - item2.currentThirst;
				}
				int num2 = list[0].GetTagReadonly("power_source_item_energy").GetInt();
				int num3 = 0;
				if (num < num2 * mlPerEnergy)
				{
					num3 = num / mlPerEnergy;
					foreach (GameCharacterItem item3 in allPlayerCharacters)
					{
						item3.currentThirst = item3.maxThirst;
					}
				}
				else
				{
					while (num2 >= 1)
					{
						foreach (GameCharacterItem item4 in allPlayerCharacters)
						{
							if (num2 >= 1)
							{
								item4.currentThirst += mlPerEnergy;
								if (item4.currentThirst > item4.maxThirst)
								{
									item4.currentThirst = item4.maxThirst;
								}
								num2--;
								num3++;
							}
						}
					}
				}
				PowerHelper.TryRemoveEnergy(list[0], num3, useBandwidth: false);
				AudioManager.Instance.TryPlayOne("drink");
			}
		});
		return gameItem;
	}

	private GameItem CreateNutritionPasteDispencer()
	{
		(PixelWindow, GameSlotInventory, GameSlotInventory, ButtonElement) tuple = DirectoryUtils.CreatePasteDispencerWindow();
		PixelWindow item = tuple.Item1;
		GameSlotInventory itemInventoryLeft = tuple.Item2;
		GameSlotInventory itemInventoryRight = tuple.Item3;
		ButtonElement item2 = tuple.Item4;
		GameItem gameItem = Empty().SetContentWindow(item).SetSpriteAndShape("Items/items_machine", "automatic_meat_processor");
		gameItem.name = "Nutrition Paste Dispencer";
		gameItem.mayInventoryRemoveItemFunc = (GameItem _, GameInventory _) => false;
		itemInventoryLeft.SetBackgroundFadeSprite("Items/items_food", "water_bottle_full_premium");
		itemInventoryRight.SetBackgroundFadeSprite("Items/itematlas", "energy_credit");
		item2.SetCallback(() =>
		{
			List<GameItem> list = GraphUtils.FindAllChildrenType(itemInventoryRight, (GameItem gameItem2) => gameItem2.IsTag("power_source_item"));
			List<GameItem> list2 = GraphUtils.FindAllChildrenType(itemInventoryRight, (GameItem gameItem2) => gameItem2.IsTag("power_source_item"));
			if (list.Count != 0 && list2.Count == 0 && list[0].GetTagReadonly("power_source_item_energy").GetInt() >= 10)
			{
				GraphUtils.TryAcceptAll(itemInventoryLeft, DirectoryMaster.Item("galaxy_blend"));
				PowerHelper.TryRemoveEnergy(list[0], 10, useBandwidth: false);
			}
		});
		return gameItem;
	}
}
