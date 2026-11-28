public class MachineDealMaker
{
	private const int BASE_POWER_USE = 25;

	private const int BASE_CHANCE_INCREASE = 10;

	public static GameItem DealMaker()
	{
		(PixelWindow, GameSlotInventory, GameGridInventory, MachineryButton, GameSlotInventory) tuple = CreateMachineInventoryWindow();
		PixelWindow item = tuple.Item1;
		GameSlotInventory powerInv = tuple.Item2;
		GameGridInventory moduleGrid = tuple.Item3;
		MachineryButton item2 = tuple.Item4;
		GameSlotInventory item3 = tuple.Item5;
		item3.mayInventoryAddItemFunc = (GameItem gameItem, GameInventory _) => gameItem.identifier.Equals("postit");
		item3.SetBackgroundFadeSprite("Items/items_tool", "postit");
		GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(item).SetSpriteAndShape("Items/items_tool", "dealmaker");
		machine.name = "Deal Maker";
		machine.flavorText = "Handheld customer-analysis unit. Scans posture, speech, and spending tells to generate a real-time negotiation script. ";
		machine.shortDescription = "Activate while a customer is present for a better chance at a successful bargain.";
		machine.ModifyTag("MACHINE_QUALITY_RATIO_INT", (TagState state) =>
		{
			state.Enable().SetInt(10);
		});
		machine.unitValue = 400L;
		MachineHelper.SetupBatterySlot(powerInv, machine);
		machine.EnableTag("STANDARD_MACHINE_TAG");
		machine.SetGameItemType("MACHINE");
		MachineryHelper.InitMachinery(machine, 25);
		string[] tags = new string[1] { "MODULE_TYPE_UNIVERSAL" };
		ContainerHelper.AllowOnlyTaggedItemsOr(moduleGrid, tags, ownedOnly: true);
		moduleGrid.onSlotAddItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			ModuleEffectHelper.OnAddedModuleToGrid(addedItem, machine, moduleGrid.childItems);
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(moduleGrid, addedItem, machine);
			}
			UpdatePowerUse(machine);
		};
		moduleGrid.onSlotRemoveItemEndFunc = (GameItem addedItem, GameInventory parentInventory, SlotMarker slot) =>
		{
			ModuleEffectHelper.OnRemovedModuleFromGrid(addedItem, machine, moduleGrid.childItems);
			if (addedItem.IsTag("MODULE_TAG"))
			{
				ModuleHelper.ComputeModuleEffect(moduleGrid, addedItem, machine);
			}
			UpdatePowerUse(machine);
		};
		item2.SetCallback(() =>
		{
			TryActivate(machine, powerInv);
		});
		machine.mayActivateSlotItemFunc = (GameItem item4, GameInventory _, SlotMarker _) => GeneralHelper.IsItemOwned(item4);
		machine.canActivateSlotItemFunc = (GameItem machine2, GameInventory _, SlotMarker _) => CanActivate(machine2, powerInv);
		machine.onActivateSlotItemFunc = (GameItem machine2, GameInventory _, SlotMarker _) =>
		{
			TryActivate(machine2, powerInv);
		};
		return machine;
	}

	public static bool CanActivate(GameItem machine, GameSlotInventory powerInv)
	{
		if (PlayerStore.Instance == null || !PlayerStore.Instance.IsClientPresent())
		{
			return false;
		}
		if (PlayerStore.Instance.dealMakerActivatedThisCustomer)
		{
			return false;
		}
		int machinePowerUsage = MachineryHelper.GetMachinePowerUsage(machine);
		if (powerInv.childItem == null || !PowerHelper.CanDrawPowerSource(powerInv.childItem, machinePowerUsage))
		{
			return false;
		}
		return true;
	}

	public static void TryActivate(GameItem machine, GameSlotInventory powerInv)
	{
		if (CanActivate(machine, powerInv))
		{
			int machinePowerUsage = MachineryHelper.GetMachinePowerUsage(machine);
			PowerHelper.DrawPowerSource(powerInv.childItem, machinePowerUsage);
			PlayerStore.Instance.dealMakerBargainBonus = GetChanceIncrease(machine);
			PlayerStore.Instance.dealMakerActivatedThisCustomer = true;
			AudioManager.Instance.Play2("module_insert");
		}
	}

	public static void UpdatePowerUse(GameItem machine)
	{
		int num = machine.GetTagReadonly("MACHINERY_DRAWN_POWER_BASE").GetInt();
		int num2 = machine.GetTagReadonly("TOTAL_PERCENTAGE_POWER_BONUS_INT").GetInt();
		int num3 = machine.GetTagReadonly("TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT").GetInt();
		int num4 = num2 + num3;
		int newPowerUse = num - num * num4 / 100;
		if (newPowerUse < 1)
		{
			newPowerUse = 1;
		}
		machine.ModifyTag("machinery_drawn_power", (TagState state) =>
		{
			state.SetInt(newPowerUse);
		});
	}

	public static int GetChanceIncrease(GameItem machine)
	{
		int num = 10 + ModuleHelper.GetAjustedQuality(machine);
		if (num < 0)
		{
			num = 0;
		}
		return num;
	}

	public static (PixelWindow, GameSlotInventory, GameGridInventory, MachineryButton, GameSlotInventory) CreateMachineInventoryWindow(bool isDraggable = true, bool isCentered = true, int minWidth = 20)
	{
		GameSlotInventory gameSlotInventory = new GameSlotInventory();
		GameGridInventory gameGridInventory = new GameGridInventory("10000010001000001110000010001000001", 7);
		MachineryButton machineryButton = new MachineryButton("Activate");
		GameSlotInventory gameSlotInventory2 = new GameSlotInventory();
		return (MachineHelper.CreateMachineryWindow(isDraggable, MachineHelper.PowerPanel(gameSlotInventory), MachineHelper.Panel("Modules", gameGridInventory, "module"), MachineHelper.Panel("Control", machineryButton, "control"), MachineHelper.NotePanel(gameSlotInventory2)), gameSlotInventory, gameGridInventory, machineryButton, gameSlotInventory2);
	}

	public static void CreateTooltip(RichTextBuilder builder, GameItem item)
	{
		if (item.identifier.Equals("deal_maker"))
		{
			if (PlayerStore.Instance == null || !PlayerStore.Instance.IsClientPresent())
			{
				builder.AddLine("No customer present.", ignoreEmpty: true, RenderHandler.ColorPalette.LightOrange);
			}
			else if (PlayerStore.Instance.dealMakerActivatedThisCustomer)
			{
				builder.AddLine("Customer already under effect (+" + PlayerStore.Instance.dealMakerBargainBonus + "%).", ignoreEmpty: true, RenderHandler.ColorPalette.LightGreen);
			}
			else
			{
				builder.AddLine("Ready: activate to boost this customer's bargain odds.", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
			}
			builder.AddLine("Bargain success chance when activated: +" + GetChanceIncrease(item) + "%", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
		}
	}

	public static GameItem CreateNote()
	{
		GameItem gameItem = DirectoryMaster.Item("postit");
		gameItem.name = "Deal Maker Manual";
		gameItem.flavorText = "[Module Impact]: \n  Efficiency: Power Consumption\n  Performance: Power Consumption\n  Quality (0.1 Ratio): Bargain Success Chance\nActivate while a customer is present for a better chance at a successful bargain.";
		gameItem.SetGameItemType("DOCUMENT");
		gameItem.EnableTag("IMPORTANT_TAG");
		gameItem.unitValue = 0L;
		return gameItem;
	}
}
