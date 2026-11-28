using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StampUI : MonoBehaviour
{
	public TextMeshProUGUI amount;

	public List<GameObject> seals;

	public List<GameObject> letterings;

	public List<GameObject> logos;

	public List<GameObject> chips;

	private void Start()
	{
	}

	private void Update()
	{
	}

	public void OnInit(GameItem gameItem)
	{
		if (gameItem.IsTag("FOOD_STAMP_TAG"))
		{
			DisableAll();
			int num = gameItem.GetTagReadonly("STAMP_SEAL").GetInt();
			int num2 = gameItem.GetTagReadonly("STAMP_LETTERING").GetInt();
			int num3 = gameItem.GetTagReadonly("STAMP_LOGO").GetInt();
			int num4 = gameItem.GetTagReadonly("STAMP_CHIP").GetInt();
			int num5 = gameItem.GetTagReadonly("STAMP_AMOUNT").GetInt();
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
				chips[num4].SetActive(value: true);
			}
			amount.text = LocHelper.GetLocalizedMechanic("ins_stamp_credits", num5);
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
		foreach (GameObject chip in chips)
		{
			chip.SetActive(value: false);
		}
	}
}
