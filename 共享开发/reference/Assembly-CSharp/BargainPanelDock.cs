using UnityEngine;

public class BargainPanelDock : MonoBehaviour
{
	public Transform tablet;

	private void LateUpdate()
	{
		if (!(tablet == null) && !(tablet.parent != transform.parent))
		{
			int siblingIndex = tablet.GetSiblingIndex();
			int siblingIndex2 = transform.GetSiblingIndex();
			if (siblingIndex2 > siblingIndex)
			{
				transform.SetSiblingIndex(siblingIndex);
			}
			else if (siblingIndex2 < siblingIndex - 1)
			{
				transform.SetSiblingIndex(siblingIndex - 1);
			}
		}
	}
}
