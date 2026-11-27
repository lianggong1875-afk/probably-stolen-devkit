using System;
using System.Collections.Generic;
using System.Linq;

public class ThingContainer
{
	private Dictionary<EnergyType, long> thingDict;

	public double volume;

	public long heat;

	public ThingContainer(double volume = 1.0)
	{
		this.volume = volume;
		thingDict = new Dictionary<EnergyType, long>();
	}

	public long GetTotalMatter()
	{
		return GetValue(EnergyType.Matter);
	}

	public long GetTotalMass()
	{
		return GetValue(EnergyType.Matter);
	}

	public void BalanceHeat()
	{
	}

	private void Merge(Dictionary<EnergyType, long> source, Dictionary<EnergyType, long> target)
	{
		foreach (KeyValuePair<EnergyType, long> item in source)
		{
			if (target.ContainsKey(item.Key))
			{
				target[item.Key] += item.Value;
			}
			else
			{
				target.TryAdd(item.Key, item.Value);
			}
		}
	}

	public void Absorb(ThingContainer container)
	{
		Dictionary<EnergyType, long> dictionary = container.thingDict;
		container.thingDict = new Dictionary<EnergyType, long>();
		if (Enumerable.Count(thingDict) >= Enumerable.Count(dictionary))
		{
			Merge(dictionary, thingDict);
			return;
		}
		Merge(thingDict, dictionary);
		thingDict = dictionary;
	}

	public void Add(EnergyType thing, long value)
	{
		if (value < 0 || thing == EnergyType.None)
		{
			return;
		}
		List<KeyValuePair<EnergyType, long>> list = thingDict.Where((KeyValuePair<EnergyType, long> p) => p.Key.HasFlag(thing)).ToList();
		if (!list.Any())
		{
			thingDict.TryAdd(thing, value);
			return;
		}
		long num = value / Enumerable.Count(list);
		long num2 = value % Enumerable.Count(list);
		for (int num3 = 0; num3 < Enumerable.Count(list); num3++)
		{
			long value2 = list[num3].Value + num + ((num3 < num2) ? 1 : 0);
			thingDict[list[num3].Key] = value2;
		}
	}

	public long GetValue(EnergyType thing)
	{
		if (thing == EnergyType.None)
		{
			return 0L;
		}
		return (from p in thingDict
			where p.Key.HasFlag(thing)
			select p.Value).Sum();
	}

	public void Clear()
	{
		thingDict.Clear();
	}

	public long Remove(EnergyType thing, long value = long.MaxValue)
	{
		if (value < 0)
		{
			return 0L;
		}
		if (thing == EnergyType.None)
		{
			return value;
		}
		List<KeyValuePair<EnergyType, long>> list = thingDict.Where((KeyValuePair<EnergyType, long> p) => p.Key.HasFlag(thing)).ToList();
		if (!list.Any())
		{
			return value;
		}
		long totalValue = list.Select((KeyValuePair<EnergyType, long> p) => p.Value).Sum();
		long num = Math.Min(value, totalValue);
		long remainingAmount = totalValue - num;
		if (remainingAmount == 0L)
		{
			for (int num2 = 0; num2 < Enumerable.Count(list); num2++)
			{
				thingDict.Remove(list[num2].Key);
			}
			return num;
		}
		List<long> list2 = list.Select((KeyValuePair<EnergyType, long> p) => (long)((double)p.Value / (double)totalValue * (double)remainingAmount)).ToList();
		long num3 = list2.Sum();
		long num4 = remainingAmount - num3;
		long num5 = num4 / Enumerable.Count(list);
		long num6 = num4 % Enumerable.Count(list);
		for (int num7 = 0; num7 < Enumerable.Count(list); num7++)
		{
			long num8 = list2[num7] + num5 + ((num7 < num6) ? 1 : 0);
			if (num8 <= 0)
			{
				thingDict.Remove(list[num7].Key);
			}
			else
			{
				thingDict[list[num7].Key] = num8;
			}
		}
		return num;
	}
}
