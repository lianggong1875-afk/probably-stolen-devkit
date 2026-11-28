using System;
using System.Collections.Generic;

public class StoreEventActionDict
{
	public const string ADD_CLIENT_ACTION = "addClientAction";

	public static Dictionary<string, Action> actions = new Dictionary<string, Action>
	{
		{
			"medicalSuppliesShortageOnActive",
			() =>
			{
				StoreRequest.ActivateRequest(StoreRequestList.MedShortageLL());
				StoreRequest.ActivateRequest(StoreRequestList.MedShortageUL());
				StoreRequest.ActivateRequest(StoreRequestList.MedShortageBM());
			}
		},
		{
			"construction_projectOnActive",
			() =>
			{
				StoreRequest.ActivateRequest(StoreRequestList.LLConstruction());
				StoreRequest.ActivateRequest(StoreRequestList.BMConstruction());
			}
		},
		{
			"meteorShowerOnActive",
			() =>
			{
				StoreRequest.ActivateRequest(StoreRequestList.MeteorLL());
				StoreRequest.ActivateRequest(StoreRequestList.MeteorUL());
			}
		},
		{
			"evidenceLockerBreakInOnActive",
			() =>
			{
				StoreRequest.ActivateRequest(StoreRequestList.SecEvidence());
				StoreRequest.ActivateRequest(StoreRequestList.BMEvidence());
			}
		},
		{
			"SecurityBudgetCutOnActive",
			() =>
			{
				PlayerStore.Instance.storeClientManager.ResetInspection();
			}
		},
		{
			"badNutrifruitHarvestOnActive",
			() =>
			{
				StoreRequest.ActivateRequest(StoreRequestList.LLSoupKitchen());
			}
		},
		{
			"evidenceLockerBreakInaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientUniqueList.UniqueEvidenceSeller();
				storeClient.eventSourceId = "evidenceLockerBreakIn";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"badNutrifruitHarvestaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateFoodBuyerClient();
				storeClient.eventSourceId = "badNutrifruitHarvest";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"seedShortageaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateFarmerSeedClient();
				storeClient.eventSourceId = "seedShortage";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"medicalSuppliesShortageaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreatePharmacistClient();
				storeClient.eventSourceId = "medicalSuppliesShortage";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"water_treatment_breakdownaddClientAction",
			() =>
			{
				if (RNG.Roll(50))
				{
					StoreClient storeClient = StoreClientListEvent.CreatePanicBuyerClient();
					storeClient.eventSourceId = "water_treatment_breakdown";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient);
				}
				else
				{
					StoreClient storeClient2 = StoreClientListEvent.CreateCorporateBuyerClient();
					storeClient2.eventSourceId = "water_treatment_breakdown";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient2);
				}
			}
		},
		{
			"scheduled_treatment_maintenanceaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateGenericWaterBuyer();
				storeClient.eventSourceId = "scheduled_treatment_maintenance";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"miner_return_ice_asteroidaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateIceMiner();
				storeClient.eventSourceId = "miner_return_ice_asteroid";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"lower_level_mob_justiceaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateLynchingCitizen();
				storeClient.eventSourceId = "lower_level_mob_justice";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"storeroom_excavatedaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateScavengerHouseholdClient();
				storeClient.eventSourceId = "storeroom_excavated";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"miningExpeditionReturnaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientList.CreateMiner();
				storeClient.eventSourceId = "miningExpeditionReturn";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"construction_projectaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateForeman();
				storeClient.eventSourceId = "construction_project";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"meteorShoweraddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreateForeman();
				storeClient.eventSourceId = "meteorShower";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"crackdownaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientList.CreateInspectionClient();
				storeClient.eventSourceId = "crackdown";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"powerShortageaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreatePowerEventLLBuyer();
				storeClient.eventSourceId = "powerShortage";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"powerPlantMaintenanceaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreatePowerMaintenanceEventLLBuyer();
				storeClient.eventSourceId = "powerPlantMaintenance";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
			}
		},
		{
			"stationWideBlackoutaddClientAction",
			() =>
			{
				StoreClient storeClient = StoreClientListEvent.CreatePowerEventULBuyer();
				storeClient.eventSourceId = "stationWideBlackout";
				PlayerStore.Instance.storeClientManager.AddClient(storeClient);
				if (RNG.Roll(25))
				{
					StoreClient storeClient2 = StoreClientListEvent.CreatePanickedShopkeeperBuyer();
					storeClient2.eventSourceId = "stationWideBlackout";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient2);
				}
			}
		},
		{
			"cruiseShipDockedaddClientAction",
			() =>
			{
				if (RNG.Roll(33))
				{
					StoreClient storeClient = StoreClientList.CreateCluelessTouristBuyer();
					storeClient.eventSourceId = "cruiseShipDocked";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient);
				}
				else if (RNG.Roll(33))
				{
					StoreClient storeClient2 = StoreClientList.CreateTouristWeaponSouvenirBuyer();
					storeClient2.eventSourceId = "cruiseShipDocked";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient2);
				}
				else
				{
					StoreClient storeClient3 = StoreClientList.CreateTouristJunkSouvenirBuyer();
					storeClient3.eventSourceId = "cruiseShipDocked";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient3);
				}
			}
		},
		{
			"oxymoreWaveaddClientAction",
			() =>
			{
				if (RNG.Roll(33))
				{
					StoreClient storeClient = StoreClientListEvent.CreateClinicVolunteerClient();
					storeClient.eventSourceId = "oxymoreWave";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient);
				}
				else if (RNG.Roll(33))
				{
					StoreClient storeClient2 = StoreClientListTierSubstance.CreateDesperateAddict();
					storeClient2.eventSourceId = "oxymoreWave";
					PlayerStore.Instance.storeClientManager.AddClient(storeClient2);
				}
			}
		}
	};
}
