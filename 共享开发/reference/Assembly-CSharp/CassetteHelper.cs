using UnityEngine;

public class CassetteHelper
{
	public const string CASSETTE_TAG = "CASSETTE_TAG";

	public const string CASSETTE_CONTENT = "CASSETTE_CONTENT";

	public const int FRAME_WIDTH_PX = 2;

	private const RenderHandler.ColorPalette FRAME_COLOR = RenderHandler.ColorPalette.Green;

	private const float FRAME_PAUSED_ALPHA = 0.45f;

	public static void InitCassetteItem(GameItem gameItem, string trackId)
	{
		gameItem.EnableTag("CASSETTE_TAG");
		gameItem.ModifyTag("CASSETTE_CONTENT", (TagState state) =>
		{
			state.Enable().SetString(trackId);
		});
	}

	public static Color? GetPlayingFrameColor(GameItem item)
	{
		if (item == null || !item.IsTag("CASSETTE_TAG"))
		{
			return null;
		}
		GameInventory parentInventory = item.parentInventory;
		GameItem deckOwner = MachineCassettePlayer.GetDeckOwner(parentInventory);
		if (deckOwner == null)
		{
			return null;
		}
		int num = deckOwner.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
		if (num != 1 && num != 2)
		{
			return null;
		}
		int num2 = deckOwner.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt();
		int num3 = 0;
		foreach (GameItem childItem in parentInventory.childItems)
		{
			if (!childItem.IsTag("CASSETTE_TAG"))
			{
				continue;
			}
			if (childItem == item)
			{
				if (num3 != num2)
				{
					return null;
				}
				Color value = RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.Green);
				if (num == 2)
				{
					value.a *= 0.45f;
				}
				return value;
			}
			num3++;
		}
		return null;
	}
}
