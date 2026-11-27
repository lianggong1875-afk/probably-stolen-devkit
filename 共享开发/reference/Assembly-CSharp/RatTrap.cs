public class RatTrap
{
	public const string MOUSE_TRAP_TAG = "MOUSE_TRAP_TAG";

	public static GameItem CreateMouseTrap()
	{
		(PixelWindow, GameSlotInventory) tuple = CreateMouseTrapWindow();
		PixelWindow item = tuple.Item1;
		GameSlotInventory trapSlot = tuple.Item2;
		GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(item).SetSpriteAndShape("Items/items_husbandry", "mouse_trap");
		machine.name = LocHelper.GetLocalizedItem("husbandry_mouse_trap_name");
		machine.flavorText = LocHelper.GetLocalizedItem("husbandry_mouse_trap_flavor");
		machine.unitValue = 50L;
		machine.EnableTag("MOUSE_TRAP_TAG");
		machine.EnableTag("CONTAINER_TAG");
		machine.EnableTag("NESTABLE_DEVICE_TAG");
		AudioHelper.InitPlasticMachinery(machine);
		trapSlot.SetBackgroundFadeSprite("Items/items_food", "cheese");
		trapSlot.mayInventoryAddItemFunc = (GameItem gameItem, GameInventory _) =>
		{
			if (!GeneralHelper.IsItemOwned(machine))
			{
				return false;
			}
			if (!GeneralHelper.IsItemOwned(gameItem))
			{
				return false;
			}
			if (gameItem.IsTag("BACKPACK_TAG") || gameItem.IsTag("CONTAINER_TAG"))
			{
				return false;
			}
			if (gameItem.IsGameItemType("FOOD") && gameItem.identifier.Equals("processed_cheese"))
			{
				return true;
			}
			return gameItem.IsTag("ANIMAL_ITEM_TAG") ? true : false;
		};
		trapSlot.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			if (addedItem.identifier.Equals("processed_cheese"))
			{
				machine.SetSpriteAndShape("Items/items_husbandry", "mouse_trap_cheese");
			}
			else if (addedItem.IsTag("ANIMAL_ITEM_TAG") && addedItem.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString().Equals("ANIMAL_RAT"))
			{
				if (addedItem.GetTagReadonly("ANIMAL_SEXE_TAG").GetString().Equals("MALE"))
				{
					machine.SetSpriteAndShape("Items/items_husbandry", "mouse_trap_m");
				}
				else
				{
					machine.SetSpriteAndShape("Items/items_husbandry", "mouse_trap_f");
				}
			}
		};
		trapSlot.onSlotRemoveItemEndFunc = (GameItem removedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			machine.SetSpriteAndShape("Items/items_husbandry", "mouse_trap");
		};
		machine.onCycleEndSlotItemFunc = (GameItem curItem, GameInventory curInv, SlotMarker marker) =>
		{
			GameItem childItem = trapSlot.childItem;
			bool flag = childItem?.identifier.Equals("processed_cheese") ?? false;
			bool flag2 = childItem == null;
			if (flag || flag2)
			{
				if (RNG.Roll((!flag) ? 1 : 50))
				{
					if (flag)
					{
						childItem.Destroy();
					}
					string sex = (RNG.Roll(50) ? "MALE" : "FEMALE");
					int randomInt = RNG.GetRandomInt(3, 7);
					GameItem item2 = PreBuiltItemHelper.CreateRat(sex, randomInt);
					GraphUtils.TryAcceptAll(trapSlot, item2);
				}
				else if (flag && RNG.Roll(25))
				{
					childItem.Destroy();
				}
			}
		};
		machine.onLoaded = (GameItem gameItem) =>
		{
			gameItem.EnableTag("NESTABLE_DEVICE_TAG");
		};
		return machine;
	}

	public static (PixelWindow, GameSlotInventory) CreateMouseTrapWindow()
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory();
		gameSlotInventory.EnableTag("MOUSE_TRAP_TAG");
		return (MachineHelper.CreateMachineryWindow(true, MachineHelper.Panel(null, gameSlotInventory, "trap")), gameSlotInventory);
	}

	public static GameSlotInventory GetTrapSlot(GameItem trap)
	{
		return MachineHelper.GetBodyStorage(trap, 0) as GameSlotInventory;
	}

	public static void CreateTooltip(RichTextBuilder builder, GameItem item)
	{
		item.IsTag("MOUSE_TRAP_TAG");
	}
}
