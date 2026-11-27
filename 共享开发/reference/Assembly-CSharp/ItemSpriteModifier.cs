using UnityEngine;

public struct ItemSpriteModifier(Sprite sprite, Vector2 position, int orientation = 0, bool isFlipped = false, int priority = 0, bool haveMask = false, Color color = default(Color))
{
	public readonly Sprite sprite = sprite;

	public readonly Vector2 position = position;

	public readonly int orientation = orientation;

	public readonly bool isFlipped = isFlipped;

	public readonly int priority = priority;

	public readonly bool haveMask = haveMask;

	public readonly Color color = ((color == default(Color)) ? Color.white : color);
}
