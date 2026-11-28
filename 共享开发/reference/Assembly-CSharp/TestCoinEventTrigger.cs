using System.Linq;

public class TestCoinEventTrigger : EventTrigger
{
	public override bool? Trigger()
	{
		if (GameMaster.current.worldTags.GetTag("TEST_COIN_EVENT_STARTED").GetInt() > 1)
		{
			return false;
		}
		if (GameMaster.current.shipManager.ship.FindAllChildItems().FindAll((GameItem i) => i.identifier == "coin").Any())
		{
			return true;
		}
		return null;
	}

	public override EventData GenerateEvent()
	{
		EventData e = new EventData().SetText("Test Event", "Hello, this is a test event!");
		e.startAction = (EventData eventData) =>
		{
			TagState tag = GameMaster.current.worldTags.GetTag("TEST_COIN_EVENT_STARTED");
			tag.SetInt(tag.GetInt() + 1);
		};
		e.endTurn = 3;
		e.turnAction = (EventData eventData) =>
		{
			if (eventData.startTurn != eventData.turns)
			{
				OverlayHandler.current.PushEventRichText(new RichTextBuilder().Add("Event is almost ended! " + (eventData.endTurn - eventData.turns) + " turns left!", RenderHandler.ColorPalette.LightRed));
			}
		};
		e.endAction = (EventData eventData) =>
		{
			GameMaster.current.shipManager.ship?.TryAcceptAll(DirectoryMaster.Item("green_injector").SetAmount(1));
			OverlayHandler.current.PushEventRichText(new RichTextBuilder().Add("A green injector has appeared in the ship's cargo!", RenderHandler.ColorPalette.LightGreen));
		};
		e.AddChoice(DirectoryMaster.Item("coin"), "Give me some money!", "", null, null, () =>
		{
			GameMaster.current.shipManager.ship?.TryAcceptAll(DirectoryMaster.Item("coin").SetAmount(5));
			OverlayHandler.current.PushEventRichText(new RichTextBuilder().Add("Some coins has appeared in the ship's cargo!", RenderHandler.ColorPalette.LightGreen));
			e.ForceEnd();
			EventData eventData = new EventData().SetText("Chained event", "This is the event that runs after spawning some money!");
			GameMaster.current.shipManager.SetEvent(eventData);
		});
		return e;
	}
}
