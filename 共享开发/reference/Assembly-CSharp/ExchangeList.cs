using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExchangeList
{
	[ES3NonSerializable]
	public static List<string> modulePool = new List<string> { "system_module_performance", "system_module_efficiancy", "system_module_quality", "system_module_overclock", "system_module_eco", "system_module_fineness" };

	[ES3NonSerializable]
	public static List<int> modulePoolOdd = new List<int> { 100, 100, 100, 33, 33, 33 };

	public static ExchangeBarter FilterBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "filter_barter",
			priority = 1,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			onAccept = (List<GameItem> list) =>
			{
				PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.WaterFilter());
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemId = "water_filter"
		};
		exchangeBarter.conditions.Add(item);
		BarterCondition item2 = new BarterCondition
		{
			requiredItemId = "water_filter"
		};
		exchangeBarter.conditions.Add(item2);
		BarterCondition item3 = new BarterCondition
		{
			requiredItemId = "water_filter"
		};
		exchangeBarter.conditions.Add(item3);
		return exchangeBarter;
	}

	public static ExchangeBarter ModuleBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "module_barter",
			priority = 1,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			onAccept = (List<GameItem> list) =>
			{
				if (RNG.Roll(1))
				{
					PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("system_capped_neural_core"));
				}
				else
				{
					List<string> list2 = list.Select((GameItem gameItem) => gameItem.identifier).ToList();
					List<(string, int)> list3 = new List<(string, int)>();
					for (int num = 0; num < modulePool.Count; num++)
					{
						string text = modulePool[num];
						if (!list2.Contains(text))
						{
							list3.Add((text, modulePoolOdd[num]));
						}
					}
					if (list3.Count > 0)
					{
						int maxExclusive = list3.Sum(((string module, int odds) m) => m.odds);
						int randomIntExculsive = RNG.GetRandomIntExculsive(0, maxExclusive);
						int num2 = 0;
						foreach (var item5 in list3)
						{
							string item3 = item5.Item1;
							int item4 = item5.Item2;
							num2 += item4;
							if (randomIntExculsive < num2)
							{
								PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item(item3));
								break;
							}
						}
					}
				}
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemTag = "MODULE"
		};
		exchangeBarter.conditions.Add(item);
		BarterCondition item2 = new BarterCondition
		{
			requiredItemTag = "MODULE"
		};
		exchangeBarter.conditions.Add(item2);
		return exchangeBarter;
	}

	public static ExchangeBarter DreamDustBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "dream_dust_barter",
			priority = 2,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			onAccept = (List<GameItem> list) =>
			{
				int num = 0;
				foreach (GameItem item3 in list)
				{
					num += (int)ChemicalProductHelper.GetPurity(item3);
				}
				if (num >= 60)
				{
					bool flag = true;
					if (num < 120)
					{
						flag = false;
					}
					if (RNG.Roll(15))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.WaterFilter());
					}
					else if (RNG.Roll(15))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("small_raw_meat"));
					}
					else if (RNG.Roll(15))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.RealIngotPurity("INGOT_PURITY_FAIR"));
					}
					else if (RNG.Roll(1) & flag)
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("energy_credit_breeder"));
					}
					else if (RNG.Roll(1) & flag)
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("scav_token"));
					}
					else if (RNG.Roll(8))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("common_electronic"));
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("common_electronic"));
						}
					}
					else if (RNG.Roll(5))
					{
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("kitchen_cleaver"));
						}
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("kitchen_knife"));
					}
					else if (RNG.Roll(8))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("common_medical"));
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("common_medical"));
						}
					}
					else if (RNG.Roll(2))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("birth_control_pill"));
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("birth_control_pill"));
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("pack_condom"));
						}
					}
					else if (!StartingPerk.IsBannedCommissary() && RNG.Roll(10))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.CreateRealStamp80());
					}
					else if (RNG.Roll(4) & flag)
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.EnergyCredit(100));
					}
					else if (RNG.Roll(10))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("blood_bag"));
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("blood_bag"));
						}
					}
					else
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("processed_meat"));
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("processed_meat"));
						}
						if (flag)
						{
							PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("processed_meat"));
						}
					}
				}
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemId = "dream_dust"
		};
		exchangeBarter.conditions.Add(item);
		BarterCondition item2 = new BarterCondition
		{
			requiredItemId = "dream_dust"
		};
		exchangeBarter.conditions.Add(item2);
		return exchangeBarter;
	}

	public static ExchangeBarter CommandKeycardBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "commandKeycardBarter",
			priority = 1,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			onAccept = (List<GameItem> list) =>
			{
				string[] array = new string[3] { "sec_keycard", "eng_keycard", "med_keycard" };
				for (int i = 0; i < 2; i++)
				{
					int num = UnityEngine.Random.Range(0, array.Length);
					GameItem item2 = DirectoryMaster.Item(array[num]);
					PlayerStore.Instance.exchangeManager.itemsBuffer.Add(item2);
				}
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemId = "cmd_keycard"
		};
		exchangeBarter.conditions.Add(item);
		return exchangeBarter;
	}

	public static ExchangeBarter PoisonBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "poison_barter",
			priority = 1,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			onAccept = (List<GameItem> list) =>
			{
				int num = 0;
				foreach (GameItem item3 in list)
				{
					if (item3.IsTag("CHEMICAL_PRODUCT_TAG"))
					{
						num += (int)ChemicalProductHelper.GetPurity(item3) / 2;
					}
					else
					{
						ContrabandHelper.ApplyContrabandFeature(item3);
						PermitHelper.ApplyRestrictedFeature(item3);
						num += (int)item3.GetCurrentValue(useRetailMarkup: false, withChild: true, includeEvents: true, forceMarkup: false, 0L);
					}
				}
				num = Mathf.Clamp(num, 0, 100);
				PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.EnergyCredit(num));
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemTag = "POISON"
		};
		exchangeBarter.conditions.Add(item);
		BarterCondition item2 = new BarterCondition
		{
			requiredItemTag = "POISON"
		};
		exchangeBarter.conditions.Add(item2);
		return exchangeBarter;
	}

	public static ExchangeBarter BulletBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "bullet_barter",
			priority = 1,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			onAccept = (List<GameItem> list) =>
			{
				int num = 0;
				foreach (GameItem item3 in list)
				{
					num += ContrabandHelper.GetContrabandLevel(item3);
					num += ContrabandHelper.GetRestrictedLevel(item3);
				}
				for (int i = 0; i < num + 2; i++)
				{
					if (RNG.Roll(20))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("cat_bar"));
					}
					else if (RNG.Roll(20))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("processed_meat"));
					}
					else if (RNG.Roll(20))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("soda_red"));
					}
					else if (RNG.Roll(20))
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("processed_milk"));
					}
					else
					{
						PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("galaxy_blend"));
					}
				}
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemTag = "AMMUNITION"
		};
		exchangeBarter.conditions.Add(item);
		BarterCondition item2 = new BarterCondition
		{
			requiredItemTag = "AMMUNITION"
		};
		exchangeBarter.conditions.Add(item2);
		return exchangeBarter;
	}

	public static ExchangeBarter SmugglerBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "smuggler_barter";
		exchangeBarter.priority = 10;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(DirectoryMaster.Item("smuggler_bay_mod"));
		};
		BarterCondition barterCondition = new BarterCondition();
		barterCondition.requiredItemId = "smuggler_bay";
		exchangeBarter.conditions.Add(barterCondition);
		for (int num = 0; num < 8; num++)
		{
			BarterCondition barterCondition2 = new BarterCondition();
			barterCondition2.requiredItemId = "pink_injector";
			exchangeBarter.conditions.Add(barterCondition2);
		}
		return exchangeBarter;
	}

	public static ExchangeBarter ImprovedDreamDustBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "improved_dream_dust_barter";
		exchangeBarter.priority = 4;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.isPromotional = true;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			double num2 = 0.0;
			foreach (GameItem item in list)
			{
				num2 += ChemicalProductHelper.GetPurity(item);
			}
			RNG.GetOdd(ImprovedDreamDustRewards((list.Count > 0) ? (num2 / (double)list.Count) : 0.0))();
		};
		for (int num = 0; num < 4; num++)
		{
			BarterCondition barterCondition = new BarterCondition();
			barterCondition.requiredItemId = "dream_dust";
			exchangeBarter.conditions.Add(barterCondition);
		}
		return exchangeBarter;
	}

	private static (Action Value, int Weight)[] ImprovedDreamDustRewards(double averagePurity)
	{
		if (!(averagePurity >= 99.0))
		{
			if (!(averagePurity >= 90.0))
			{
				if (!(averagePurity >= 80.0))
				{
					if (!(averagePurity >= 60.0))
					{
						if (!(averagePurity >= 45.0))
						{
							if (!(averagePurity >= 30.0))
							{
								return new (Action, int)[1] { (() =>
								{
									GiveExchangeItem("bandage_item", 2);
								}, 100) };
							}
							return new (Action, int)[3]
							{
								(() =>
								{
									GiveExchangeItem("bandage_item");
									GiveExchangeItem("hemostatic_bandage_item");
									GiveExchangeItem("topical_bandage_item");
								}, 40),
								(() =>
								{
									GiveExchangeItem("common_medical");
								}, 35),
								(() =>
								{
									GiveExchangeItem("pink_injector");
								}, 25)
							};
						}
						return new (Action, int)[5]
						{
							(() =>
							{
								GiveExchangeItem("pink_injector", 2);
							}, 35),
							(() =>
							{
								GiveExchangeItem("common_medical", 2);
							}, 25),
							(() =>
							{
								GiveExchangeItem(PreBuiltItemHelper.CreateRealExpiredInjector);
							}, 20),
							(() =>
							{
								GiveExchangeItem("blood_bag", 3);
							}, 15),
							(() =>
							{
								GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector);
							}, 5)
						};
					}
					return new (Action, int)[7]
					{
						(() =>
						{
							GiveExchangeItem("pink_injector", 3);
						}, 44),
						(() =>
						{
							GiveExchangeItem("common_medical", 5);
						}, 14),
						(() =>
						{
							GiveExchangeItem(PreBuiltItemHelper.CreateRealExpiredInjector);
							GiveExchangeItem("blood_bag", 2);
						}, 12),
						(() =>
						{
							GiveExchangeItem("blood_bag", 5);
						}, 11),
						(() =>
						{
							GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector);
						}, 7),
						(() =>
						{
							GiveExchangeItem("black_injector");
						}, 6),
						(() =>
						{
							GiveExchangeItem("blue_blood_bag");
						}, 6)
					};
				}
				return new (Action, int)[5]
				{
					(() =>
					{
						GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector);
						GiveExchangeItem("pink_injector", 3);
					}, 30),
					(() =>
					{
						GiveExchangeItem("common_medical", 5);
						GiveExchangeItem("salve");
					}, 20),
					(() =>
					{
						GiveExchangeItem("blue_blood_bag");
						GiveExchangeItem("blood_bag");
					}, 20),
					(() =>
					{
						GiveExchangeItem("surgery_tool");
						GiveExchangeItem("blood_bag", 2);
					}, 15),
					(() =>
					{
						GiveExchangeItem("black_injector");
					}, 15)
				};
			}
			return new (Action, int)[5]
			{
				(() =>
				{
					GiveExchangeItem("black_injector");
				}, 30),
				(() =>
				{
					GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector, 2);
					GiveExchangeItem("salve");
				}, 25),
				(() =>
				{
					GiveExchangeItem("blue_blood_bag");
					GiveExchangeItem("pink_injector", 2);
				}, 20),
				(() =>
				{
					GiveExchangeItem("surgery_tool");
					GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector);
				}, 15),
				(() =>
				{
					GiveExchangeItem("black_injector");
					GiveExchangeItem("blue_blood_bag");
				}, 10)
			};
		}
		return new (Action, int)[4]
		{
			(() =>
			{
				GiveExchangeItem("black_injector");
				GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector);
			}, 40),
			(() =>
			{
				GiveExchangeItem("blue_blood_bag", 2);
			}, 30),
			(() =>
			{
				GiveExchangeItem(PreBuiltItemHelper.CreateRealGenuineInjector, 3);
			}, 20),
			(() =>
			{
				GiveExchangeItem("black_injector", 2);
			}, 10)
		};
	}

	private static void GiveExchangeItem(string identifier, int count = 1)
	{
		GiveExchangeItem(() => DirectoryMaster.Item(identifier), count);
	}

	private static void GiveExchangeItem(Func<GameItem> create, int count = 1)
	{
		for (int i = 0; i < count; i++)
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(create());
		}
	}

	public static ExchangeBarter SecCrateBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "sec_crate_barter";
		exchangeBarter.priority = 2;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.isPromotional = true;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.LootCrateSecurity());
		};
		for (int num = 0; num < 3; num++)
		{
			BarterCondition barterCondition = new BarterCondition();
			barterCondition.requiredItemId = "sec_keycard";
			exchangeBarter.conditions.Add(barterCondition);
		}
		return exchangeBarter;
	}

	public static ExchangeBarter MedCrateBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "med_crate_barter";
		exchangeBarter.priority = 8;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.isPromotional = true;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.LootCrateMedical());
		};
		for (int num = 0; num < 4; num++)
		{
			BarterCondition barterCondition = new BarterCondition();
			barterCondition.requiredItemId = "med_keycard";
			exchangeBarter.conditions.Add(barterCondition);
		}
		return exchangeBarter;
	}

	public static ExchangeBarter EngCrateBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "eng_crate_barter";
		exchangeBarter.priority = 2;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.isPromotional = true;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.LootCrateEngineering());
		};
		for (int num = 0; num < 4; num++)
		{
			BarterCondition barterCondition = new BarterCondition();
			barterCondition.requiredItemId = "eng_keycard";
			exchangeBarter.conditions.Add(barterCondition);
		}
		return exchangeBarter;
	}

	public static ExchangeBarter SeedBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "seed_barter";
		exchangeBarter.priority = 2;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.isPromotional = true;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.EnergyCredit(100));
		};
		for (int num = 0; num < 5; num++)
		{
			BarterCondition barterCondition = new BarterCondition();
			barterCondition.requiredItemTag = "SEED";
			exchangeBarter.conditions.Add(barterCondition);
		}
		return exchangeBarter;
	}

	public static ExchangeBarter BMRepBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter
		{
			identifier = "bm_rep_barter",
			priority = 2,
			barterCounterPerNight = 1,
			currentBarterCounter = 0,
			isPromotional = true,
			onAccept = (List<GameItem> list) =>
			{
				StoreReputation.ModBMReputation(25);
			}
		};
		BarterCondition item = new BarterCondition
		{
			requiredItemId = "scav_token"
		};
		exchangeBarter.conditions.Add(item);
		return exchangeBarter;
	}

	public static ExchangeBarter ImmunivaxBarter()
	{
		ExchangeBarter exchangeBarter = new ExchangeBarter();
		exchangeBarter.identifier = "immunivax_barter";
		exchangeBarter.priority = 1;
		exchangeBarter.barterCounterPerNight = 1;
		exchangeBarter.currentBarterCounter = 0;
		exchangeBarter.isPromotional = true;
		exchangeBarter.onAccept = (List<GameItem> list) =>
		{
			PlayerStore.Instance.exchangeManager.itemsBuffer.Add(PreBuiltItemHelper.CreateRealGenuineInjector());
		};
		for (int num = 0; num < 2; num++)
		{
			BarterCondition barterCondition = new BarterCondition();
			barterCondition.requiredItemId = "pink_injector";
			exchangeBarter.conditions.Add(barterCondition);
		}
		return exchangeBarter;
	}
}
