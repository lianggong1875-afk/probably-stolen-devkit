using System;
using System.Collections.Generic;
using System.Linq;

public class ExchangeBarter
{
	public string identifier;

	public List<BarterCondition> conditions = new List<BarterCondition>();

	public int priority;

	public int barterCounterPerNight = 1;

	public int currentBarterCounter;

	public Action<List<GameItem>> onAccept;

	public bool isPromotional;

	public bool IsRequiredItemPresent()
	{
		if (GetRequiredItems().Count == conditions.Count && GetRequiredItems().Count != 0)
		{
			return true;
		}
		return false;
	}

	public List<GameItem> GetRequiredItemsOld()
	{
		List<GameItem> list = GraphUtils.FindAllChildrenType<GameItem>(EmporiumEntry.Instance.trashcan);
		List<GameItem> list2 = new List<GameItem>();
		foreach (BarterCondition condition in conditions)
		{
			foreach (GameItem item in list)
			{
				if (condition.IsItemValid(item))
				{
					list2.Add(item);
					list.Remove(item);
					break;
				}
			}
		}
		return list2;
	}

	public List<GameItem> GetRequiredItems()
	{
		List<GameItem> source = GraphUtils.FindAllChildrenType<GameItem>(EmporiumEntry.Instance.trashcan) ?? new List<GameItem>();
		List<GameItem> list = new List<GameItem>();
		HashSet<GameItem> usedItems = new HashSet<GameItem>();
		foreach (BarterCondition condition in conditions)
		{
			GameItem gameItem = source.FirstOrDefault((GameItem gameItem2) => !usedItems.Contains(gameItem2) && condition.IsItemValid(gameItem2));
			if (gameItem != null)
			{
				list.Add(gameItem);
				usedItems.Add(gameItem);
			}
		}
		return list;
	}

	public bool CanAccept()
	{
		if (IsRequiredItemPresent())
		{
			return currentBarterCounter > 0;
		}
		return false;
	}

	public void Accept()
	{
		if (IsRequiredItemPresent())
		{
			Debug.Log(identifier + " Exchange Accepted");
			List<GameItem> requiredItems = GetRequiredItems();
			currentBarterCounter--;
			if (onAccept != null)
			{
				onAccept(requiredItems);
			}
			for (int num = requiredItems.Count - 1; num >= 0; num--)
			{
				requiredItems[num].Destroy();
			}
		}
	}

	public void Reset()
	{
		currentBarterCounter = barterCounterPerNight;
	}
}
