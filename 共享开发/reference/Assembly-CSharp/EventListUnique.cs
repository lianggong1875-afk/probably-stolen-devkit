public class EventListUnique
{
	public static StoreEvent Wanted3CapturedEvent()
	{
		return new StoreEvent
		{
			identifier = "Wanted3CapturedEvent",
			newsName = LocHelper.GetLocalizedMechanic("event_wanted3_news"),
			newsDescription = LocHelper.GetLocalizedMechanic("event_wanted3_desc"),
			duration = 1,
			importance = 4,
			eventArea = StoreEvent.EventArea.ALL,
			eventType = StoreEvent.EventType.COSMETIC
		};
	}

	public static StoreEvent CreateLouAdmiralAssassinated()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "louAdmiralAssassinated",
			newsName = LocHelper.GetLocalizedMechanic("event_lou_admiral_news")
		};
		storeEvent.displayName = storeEvent.newsName;
		storeEvent.newsDescription = LocHelper.GetLocalizedMechanic("event_lou_admiral_desc");
		storeEvent.duration = 1;
		storeEvent.importance = 8;
		storeEvent.eventArea = StoreEvent.EventArea.ALL;
		storeEvent.eventType = StoreEvent.EventType.COSMETIC;
		return storeEvent;
	}

	public static StoreEvent CreateLouWarehouseShootout()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "louWarehouseShootout",
			newsName = LocHelper.GetLocalizedMechanicOrDefault("event_lou_warehouse_news", "Shootout in the Lower Levels")
		};
		storeEvent.displayName = storeEvent.newsName;
		storeEvent.newsDescription = LocHelper.GetLocalizedMechanicOrDefault("event_lou_warehouse_desc", "Residents of the Lower Levels reported sustained gunfire from an abandoned warehouse in the old freight district last night. Security found the building rigged with tripwires and, inside, the bodies of several heavily armed individuals in plain clothes, none carrying station identification. Nobody has come forward to claim them.");
		storeEvent.duration = 1;
		storeEvent.importance = 8;
		storeEvent.eventArea = StoreEvent.EventArea.ALL;
		storeEvent.eventType = StoreEvent.EventType.COSMETIC;
		return storeEvent;
	}

	public static StoreEvent CreateEleanorLeakNews()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "eleanorLeak",
			newsName = LocHelper.GetLocalizedMechanic("event_eleanor_leak_news")
		};
		storeEvent.displayName = storeEvent.newsName;
		storeEvent.newsDescription = LocHelper.GetLocalizedMechanic("event_eleanor_leak_desc");
		storeEvent.duration = 1;
		storeEvent.importance = 9;
		storeEvent.eventArea = StoreEvent.EventArea.ALL;
		storeEvent.eventType = StoreEvent.EventType.COSMETIC;
		return storeEvent;
	}

	public static StoreEvent CreateEvidenceLockerBreakIn()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "evidenceLockerBreakIn",
			newsName = LocHelper.GetLocalizedMechanic("event_evidence_locker_news"),
			displayName = LocHelper.GetLocalizedMechanic("event_evidence_locker_news"),
			newsDescription = LocHelper.GetLocalizedMechanic("event_evidence_locker_desc"),
			duration = 1,
			importance = 6,
			eventType = StoreEvent.EventType.NORMALE,
			eventArea = StoreEvent.EventArea.LOWER
		};
		storeEvent.addClientFromEventActionId = storeEvent.identifier + "addClientAction";
		storeEvent.onActiveActionId = "evidenceLockerBreakInOnActive";
		return storeEvent;
	}
}
