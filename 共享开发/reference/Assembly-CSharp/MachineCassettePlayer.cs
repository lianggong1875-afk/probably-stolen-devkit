using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

public class MachineCassettePlayer
{
	public const string CURRENT_PLAYING_CASSETTE_INDEX_TAG = "CURRENT_PLAYING_CASSETTE_INDEX_TAG";

	public const string CURRENT_PLAYING_TRACK_ID_TAG = "CURRENT_PLAYING_TRACK_ID_TAG";

	public const string CASETTE_PLAYER_STATE_INT = "CASETTE_PLAYER_STATE_INT";

	private static readonly ConditionalWeakTable<GameInventory, GameItem> deckOwners = new ConditionalWeakTable<GameInventory, GameItem>();

	public static GameItem GetDeckOwner(GameInventory deck)
	{
		if (deck == null)
		{
			return null;
		}
		if (!deckOwners.TryGetValue(deck, out var value))
		{
			return null;
		}
		return value;
	}

	public static GameItem CassettePlayer()
	{
		(PixelWindow, GameGridInventory, GameGridInventory, MachineryButton[], MachineryButton, MachineryText, MachineryButton) tuple = CreateMachineInventoryWindow();
		PixelWindow item = tuple.Item1;
		GameGridInventory item2 = tuple.Item2;
		GameGridInventory cassette = tuple.Item3;
		MachineryButton[] item3 = tuple.Item4;
		MachineryButton item4 = tuple.Item5;
		MachineryText volumeLabel = tuple.Item6;
		MachineryButton item5 = tuple.Item7;
		GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(item).SetSpriteAndShape("Items/items_furniture", "cassette_player");
		machine.name = LocHelper.GetLocalizedItem("item_cassette_player_name");
		machine.flavorText = "";
		machine.shortDescription = "";
		machine.EnableTag("IMPORTANT_TAG");
		machine.mayInventoryRemoveItemFunc = (GameItem _, GameInventory _) => false;
		machine.unitValue = 0L;
		machine.EnableTag("CONTAINER_TOOLTIP_TAG");
		deckOwners.Add(cassette, machine);
		item2.mayInventoryAddItemFunc = (GameItem gameItem, GameInventory _) => GeneralHelper.IsItemOwned(gameItem) && gameItem.IsTag("CASSETTE_TAG");
		cassette.mayInventoryAddItemFunc = (GameItem gameItem, GameInventory _) => GeneralHelper.IsItemOwned(gameItem) && gameItem.IsTag("CASSETTE_TAG");
		GameGridInventory gameGridInventory = cassette;
		gameGridInventory.onSlotAddItemLateFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameGridInventory.onSlotAddItemLateFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem, GameInventory _, SlotMarker _) =>
		{
			AudioManager.Instance.Play3("cassette_insert");
			OnCassetteInventoryChanged(machine, cassette);
		}));
		GameGridInventory gameGridInventory2 = cassette;
		gameGridInventory2.onSlotRemoveItemLateFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(gameGridInventory2.onSlotRemoveItemLateFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem, GameInventory _, SlotMarker _) =>
		{
			AudioManager.Instance.Play3("cassette_remove");
			OnCassetteInventoryChanged(machine, cassette);
			if (gameItem is GameItemElement gameItemElement)
			{
				gameItemElement.RefreshPlayingFrame();
			}
		}));
		item2.onSlotAddItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item2.onSlotAddItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem, GameInventory _, SlotMarker _) =>
		{
			AudioManager.Instance.Play3("cassette_insert");
		}));
		item2.onSlotRemoveItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item2.onSlotRemoveItemFunc, (Action<GameItem, GameInventory, SlotMarker>)((GameItem gameItem, GameInventory _, SlotMarker _) =>
		{
			AudioManager.Instance.Play3("cassette_remove");
		}));
		item3[0].SetCallback(() =>
		{
			OnPreviousButtonPressed(machine, cassette);
		});
		item3[1].SetCallback(() =>
		{
			OnPlayButtonPressed(machine, cassette);
		});
		item3[2].SetCallback(() =>
		{
			OnStopButtonPressed(machine);
		});
		item3[3].SetCallback(() =>
		{
			OnNextButtonPressed(machine, cassette);
		});
		item4.SetCallback(() =>
		{
			ChangeVolumeClick(volumeLabel, isIncrease: false);
		});
		item5.SetCallback(() =>
		{
			ChangeVolumeClick(volumeLabel, isIncrease: true);
		});
		item.closeCallback = () =>
		{
			EmporiumOption.Instance.SaveMusicVolume();
		};
		item.openCallback = () =>
		{
			volumeLabel.SetText(LocHelper.GetLocalizedMechanic("mech_cassette_player_vol_value", (EmporiumOption.Instance.musicVolume * 100f).ToString("0")));
		};
		machine.onLoaded = (GameItem gameItem) =>
		{
			OnMachineLoaded(machine, cassette);
		};
		return machine;
	}

	public static GameGridInventory GetCassetteInventory()
	{
		return MachineHelper.GetBodyStorage(EmporiumEntry.Instance.cassettePlayer, 1) as GameGridInventory;
	}

	public static void ChangeVolumeClick(MachineryText volumeLabel, bool isIncrease)
	{
		float musicVolume = EmporiumOption.Instance.musicVolume;
		musicVolume = ((!isIncrease) ? (musicVolume - 0.05f) : (musicVolume + 0.05f));
		musicVolume = Mathf.Clamp01(musicVolume);
		volumeLabel.SetText(LocHelper.GetLocalizedMechanic("mech_cassette_player_vol_value", (musicVolume * 100f).ToString("0")));
		EmporiumOption.Instance.musicVolume = musicVolume;
		EmporiumOption.Instance.ApplyMusicVolume();
	}

	public static List<string> GetTrackListFromCassette(GameGridInventory cassettes)
	{
		List<string> list = new List<string>();
		foreach (GameItem childItem in cassettes.childItems)
		{
			if (childItem.IsTag("CASSETTE_TAG"))
			{
				string item = childItem.GetTagReadonly("CASSETTE_CONTENT").GetString();
				list.Add(item);
			}
		}
		return list;
	}

	public static void OnPlayButtonPressed(GameItem machine, GameGridInventory cassetteInventory)
	{
		switch (machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt())
		{
		case 0:
		{
			List<string> trackListFromCassette = GetTrackListFromCassette(cassetteInventory);
			if (trackListFromCassette.Count != 0 && !string.IsNullOrEmpty(trackListFromCassette[0]))
			{
				PlayTrackAtIndex(machine, trackListFromCassette, 0);
			}
			break;
		}
		case 1:
			Pause(machine);
			break;
		case 2:
			machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
			{
				tag.Enable().SetInt(1);
			});
			AudioManager.Instance.UnpauseMusic();
			AudioManager.Instance.PlayAndCallback(AudioManager.OnTrackDone);
			RefreshPlayingFrames(machine);
			break;
		}
	}

	private static void Pause(GameItem machine)
	{
		AudioManager.StopAudioCoroutine();
		machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
		{
			tag.Enable().SetInt(2);
		});
		AudioManager.Instance.PauseMusic();
		RefreshPlayingFrames(machine);
	}

	public static void PauseForGameOver()
	{
		if (!(EmporiumEntry.Instance == null) && EmporiumEntry.Instance.cassettePlayer != null)
		{
			GameItem cassettePlayer = EmporiumEntry.Instance.cassettePlayer;
			if (cassettePlayer.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt() == 1)
			{
				Pause(cassettePlayer);
			}
		}
	}

	private static void PlayTrackAtIndex(GameItem machine, List<string> trackList, int index)
	{
		AudioManager.StopAudioCoroutine();
		machine.ModifyTag("CURRENT_PLAYING_CASSETTE_INDEX_TAG", (TagState tag) =>
		{
			tag.Enable().SetInt(index);
		});
		machine.ModifyTag("CURRENT_PLAYING_TRACK_ID_TAG", (TagState tag) =>
		{
			tag.Enable().SetString(trackList[index]);
		});
		machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
		{
			tag.Enable().SetInt(1);
		});
		AudioManager.Instance.PlayMusic(trackList[index]);
		AudioManager.Instance.PlayAndCallback(AudioManager.OnTrackDone);
		RefreshPlayingFrames(machine);
	}

	private static void RefreshPlayingFrames(GameItem machine)
	{
		if (!(MachineHelper.GetBodyStorage(machine, 1) is GameInventory gameInventory))
		{
			return;
		}
		foreach (GameItem childItem in gameInventory.childItems)
		{
			if (childItem is GameItemElement gameItemElement)
			{
				gameItemElement.RefreshPlayingFrame();
			}
		}
	}

	public static void OnMachineLoaded(GameItem machine, GameGridInventory cassetteInventory)
	{
		switch (machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt())
		{
		case 2:
			machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
			{
				tag.Enable().SetInt(0);
			});
			RefreshPlayingFrames(machine);
			break;
		case 1:
		{
			List<string> trackListFromCassette = GetTrackListFromCassette(cassetteInventory);
			if (trackListFromCassette.Count == 0)
			{
				machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
				{
					tag.Enable().SetInt(0);
				});
				break;
			}
			string text = machine.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString();
			int num = (string.IsNullOrEmpty(text) ? (-1) : trackListFromCassette.IndexOf(text));
			if (num < 0)
			{
				num = Mathf.Clamp(machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt(), 0, trackListFromCassette.Count - 1);
			}
			if (!AudioManager.Instance.CanPlay(trackListFromCassette[num]))
			{
				machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
				{
					tag.Enable().SetInt(0);
				});
				RefreshPlayingFrames(machine);
			}
			else
			{
				PlayTrackAtIndex(machine, trackListFromCassette, num);
			}
			break;
		}
		}
	}

	public static void OnCassetteInventoryChanged(GameItem machine, GameGridInventory cassetteInventory)
	{
		if (EmporiumEntry.Instance == null || machine != EmporiumEntry.Instance.cassettePlayer || machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt() == 0)
		{
			return;
		}
		List<string> trackListFromCassette = GetTrackListFromCassette(cassetteInventory);
		if (trackListFromCassette.Count == 0)
		{
			OnStopButtonPressed(machine);
			return;
		}
		string text = machine.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString();
		int newIndex = (string.IsNullOrEmpty(text) ? (-1) : trackListFromCassette.IndexOf(text));
		if (newIndex >= 0)
		{
			machine.ModifyTag("CURRENT_PLAYING_CASSETTE_INDEX_TAG", (TagState tag) =>
			{
				tag.Enable().SetInt(newIndex);
			});
			RefreshPlayingFrames(machine);
		}
		else
		{
			int index = Mathf.Clamp(machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt(), 0, trackListFromCassette.Count - 1);
			PlayTrackAtIndex(machine, trackListFromCassette, index);
		}
	}

	public static void OnStopButtonPressed(GameItem machine)
	{
		int num = machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
		if (num == 1 || num == 2)
		{
			machine.ModifyTag("CASETTE_PLAYER_STATE_INT", (TagState tag) =>
			{
				tag.Enable().SetInt(0);
			});
			AudioManager.StopAudioCoroutine();
			AudioManager.Instance.StopMusic();
			RefreshPlayingFrames(machine);
		}
	}

	public static void OnPreviousButtonPressed(GameItem machine, GameGridInventory cassetteInventory)
	{
		int num = machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
		if (num == 1 || num == 2)
		{
			List<string> trackListFromCassette = GetTrackListFromCassette(cassetteInventory);
			if (trackListFromCassette.Count != 0)
			{
				int index = (machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt() - 1 + trackListFromCassette.Count) % trackListFromCassette.Count;
				PlayTrackAtIndex(machine, trackListFromCassette, index);
			}
		}
	}

	public static void OnNextButtonPressed(GameItem machine, GameGridInventory cassetteInventory)
	{
		int num = machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
		if (num == 1 || num == 2)
		{
			List<string> trackListFromCassette = GetTrackListFromCassette(cassetteInventory);
			if (trackListFromCassette.Count != 0)
			{
				int index = (machine.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt() + 1) % trackListFromCassette.Count;
				PlayTrackAtIndex(machine, trackListFromCassette, index);
			}
		}
	}

	public static (PixelWindow, GameGridInventory, GameGridInventory, MachineryButton[], MachineryButton, MachineryText, MachineryButton) CreateMachineInventoryWindow(bool isDraggable = true, bool isCentered = true, int minWidth = 50)
	{
		GameGridInventory gameGridInventory = new GameGridInventory(6, 6);
		GameGridInventory gameGridInventory2 = new GameGridInventory(6, 6);
		MachineryButton machineryButton = MachineryButton.FromAtlas("Window/elements_inner", "previous");
		MachineryButton machineryButton2 = MachineryButton.FromAtlas("Window/elements_inner", "play");
		MachineryButton machineryButton3 = MachineryButton.FromAtlas("Window/elements_inner", "pause");
		MachineryButton machineryButton4 = MachineryButton.FromAtlas("Window/elements_inner", "next");
		MachineryStack content = MachineryStack.Horizontal(machineryButton, machineryButton2, machineryButton3, machineryButton4);
		MachineryButton machineryButton5 = MachineryButton.FromAtlas("Window/elements_inner", "down");
		MachineryText machineryText = new MachineryText(LocHelper.GetLocalizedMechanic("mech_cassette_player_vol_default"), 6, HorizontalAlignmentOptions.Center, uppercase: false).SetFillWidth(fill: false);
		MachineryButton machineryButton6 = MachineryButton.FromAtlas("Window/elements_inner", "up");
		MachineryStack content2 = MachineryStack.Horizontal(machineryButton5, machineryText, machineryButton6);
		return (MachineHelper.CreateMachineryWindow(isDraggable, MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_storage"), gameGridInventory, "storage"), MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_cassette"), gameGridInventory2, "cassette"), MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_controls"), content, "controls"), MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_cassette_player_label_volume"), content2, "volume")), gameGridInventory, gameGridInventory2, new MachineryButton[4] { machineryButton, machineryButton2, machineryButton3, machineryButton4 }, machineryButton5, machineryText, machineryButton6);
	}
}
