public class EventListInnate
{
	public static StoreEvent CreateRentDayEvent()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "rent_day",
			newsName = LocHelper.GetLocalizedMechanic("event_rent_due"),
			displayName = LocHelper.GetLocalizedMechanic("event_rent_due"),
			newsDescription = "",
			duration = 1,
			cooldown = 0,
			eventType = StoreEvent.EventType.INNATE
		};
		storeEvent.addClientFromEventActionId = storeEvent.identifier + "addClientAction";
		storeEvent.isHiddenFromNewsPaper = true;
		storeEvent.IsKnown = true;
		return storeEvent;
	}

	public static StoreEvent CreateMortgagePaymentEvent()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "mortgage_payment",
			newsName = LocHelper.GetLocalizedMechanic("event_mortgage_payment"),
			displayName = LocHelper.GetLocalizedMechanic("event_mortgage_payment"),
			newsDescription = "",
			duration = 1,
			cooldown = 0,
			eventType = StoreEvent.EventType.INNATE
		};
		storeEvent.addClientFromEventActionId = storeEvent.identifier + "addClientAction";
		storeEvent.isHiddenFromNewsPaper = true;
		storeEvent.IsKnown = true;
		return storeEvent;
	}

	public static StoreEvent CreateCartelVisitEvent()
	{
		StoreEvent storeEvent = new StoreEvent
		{
			identifier = "cartel_visit",
			newsName = LocHelper.GetLocalizedMechanic("event_cartel_visit"),
			displayName = LocHelper.GetLocalizedMechanic("event_cartel_visit"),
			newsDescription = "",
			duration = 1,
			cooldown = 0,
			eventType = StoreEvent.EventType.INNATE
		};
		storeEvent.addClientFromEventActionId = storeEvent.identifier + "addClientAction";
		storeEvent.isHiddenFromNewsPaper = true;
		storeEvent.IsKnown = true;
		return storeEvent;
	}
}
