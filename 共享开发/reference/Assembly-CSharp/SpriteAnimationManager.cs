using UnityEngine;

public class SpriteAnimationManager : MonoBehaviour
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
		GameObject gameObject = new GameObject("SpriteAnimationManager");
		Object.DontDestroyOnLoad(gameObject);
		gameObject.AddComponent<SpriteAnimationManager>();
	}

	private void Update()
	{
		SpriteAnimationHelper.Tick();
	}
}
