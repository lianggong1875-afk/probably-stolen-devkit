using System.Collections.Generic;
using UnityEngine;

public class CigaretteUI : MonoBehaviour
{
	public List<GameObject> seals;

	public List<GameObject> letterings;

	public List<GameObject> logos;

	public List<GameObject> boxes;

	private void Start()
	{
	}

	private void Update()
	{
	}

	public void OnInit(GameItem gameItem)
	{
		if (gameItem.IsTag("CIGARETTE_TAG"))
		{
			DisableAll();
			int num = gameItem.GetTagReadonly("CIGARETTE_SEAL").GetInt();
			int num2 = gameItem.GetTagReadonly("CIGARETTE_LETTERING").GetInt();
			int num3 = gameItem.GetTagReadonly("CIGARETTE_LOGO").GetInt();
			int num4 = gameItem.GetTagReadonly("CIGARETTE_BOX").GetInt();
			boxes[0].SetActive(value: true);
			if (num != -1)
			{
				seals[num].SetActive(value: true);
			}
			if (num2 != -1)
			{
				letterings[num2].SetActive(value: true);
			}
			if (num3 != -1)
			{
				logos[num3].SetActive(value: true);
			}
			if (num4 != -1)
			{
				boxes[num4].SetActive(value: true);
			}
		}
	}

	public void DisableAll()
	{
		foreach (GameObject seal in seals)
		{
			seal.SetActive(value: false);
		}
		foreach (GameObject lettering in letterings)
		{
			lettering.SetActive(value: false);
		}
		foreach (GameObject logo in logos)
		{
			logo.SetActive(value: false);
		}
		foreach (GameObject box in boxes)
		{
			box.SetActive(value: false);
		}
	}
}
