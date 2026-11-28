using UnityEngine;

public class StoreDebugBell : MonoBehaviour, IClickable
{
	public void OnClick()
	{
		Debug.Log("Debug Bell Rang");
		PlayerStore.Instance.ExecuteGameOver("arrest");
	}

	private void Start()
	{
		gameObject.SetActive(value: false);
		if (!PlayerStore.Instance.devMode)
		{
			gameObject.SetActive(value: false);
		}
	}

	private void Update()
	{
	}
}
