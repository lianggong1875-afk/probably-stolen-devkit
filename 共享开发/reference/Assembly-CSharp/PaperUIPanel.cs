using UnityEngine;

public class PaperUIPanel : MonoBehaviour
{
	public PaperUI owner;

	private static bool quitting;

	private void OnApplicationQuit()
	{
		quitting = true;
	}

	private void OnDisable()
	{
		if (!quitting && owner != null && gameObject.scene.isLoaded)
		{
			owner.NotifyHidden();
		}
	}

	private void OnDestroy()
	{
		if (!quitting && owner != null)
		{
			owner.NotifyDestroyed(gameObject.activeSelf && gameObject.scene.isLoaded);
		}
	}
}
