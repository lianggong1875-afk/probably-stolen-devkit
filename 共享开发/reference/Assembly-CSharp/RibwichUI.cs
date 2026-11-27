using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RibwichUI : MonoBehaviour
{
	public GameObject meatNormal;

	public GameObject meatDouble;

	public List<GameObject> topBun;

	public List<GameObject> wrap;

	public List<GameObject> pickles;

	public List<GameObject> davreLogo;

	public List<GameObject> ribwichLogo;

	public GameObject barcode;

	public TextMeshProUGUI barcodeTMP;

	public GameObject facility;

	public TextMeshProUGUI facilityTMP;

	private void Start()
	{
	}

	private void Update()
	{
	}

	public void OnInit(GameItem gameItem)
	{
		if (gameItem.IsTag("RIBWICH_TAG"))
		{
			DisableAll();
			int num = gameItem.GetTagReadonly("RIBWICH_MEAT_TAG").GetInt();
			int index = gameItem.GetTagReadonly("RIBWICH_BUN_TAG").GetInt();
			int index2 = gameItem.GetTagReadonly("RIBWICH_WRAP_TAG").GetInt();
			int index3 = gameItem.GetTagReadonly("RIBWICH_PICKLES_TAG").GetInt();
			int num2 = gameItem.GetTagReadonly("RIBWICH_DAVRE_LOGO_TAG").GetInt();
			int num3 = gameItem.GetTagReadonly("RIBWICH_RIBWICH_LOGO_TAG").GetInt();
			int num4 = gameItem.GetTagReadonly("RIBWICH_FACILITY_NUMBER_TAG").GetInt();
			int num5 = gameItem.GetTagReadonly("RIBWICH_PRODUCTION_MONTH_TAG").GetInt();
			int num6 = gameItem.GetTagReadonly("RIBWICH_PRODUCTION_YEAR_TAG").GetInt();
			if (gameItem.GetTagReadonly("RIBWICH_PRODUCTION_TAG").GetInt() == 0)
			{
				barcode.SetActive(value: true);
			}
			barcodeTMP.text = num5 + "/ \n" + num6;
			if (gameItem.GetTagReadonly("RIBWICH_FACILITY_TAG").GetInt() == 0)
			{
				facility.SetActive(value: true);
			}
			facilityTMP.text = "#" + num4;
			topBun[index].SetActive(value: true);
			wrap[index2].SetActive(value: true);
			pickles[index3].SetActive(value: true);
			if (num2 >= 0)
			{
				davreLogo[num2].SetActive(value: true);
			}
			if (num3 >= 0)
			{
				ribwichLogo[num3].SetActive(value: true);
			}
			Debug.Log("RibwichUI: Ribwich initialized with meat option " + num);
			switch (num)
			{
			case 0:
				meatNormal.SetActive(value: true);
				meatDouble.SetActive(value: false);
				break;
			case 1:
				meatDouble.SetActive(value: true);
				meatNormal.SetActive(value: false);
				DisableList(topBun);
				DisableList(pickles);
				break;
			}
		}
	}

	public static void DisableList(List<GameObject> gameObjects)
	{
		foreach (GameObject gameObject in gameObjects)
		{
			gameObject.SetActive(value: false);
		}
	}

	public void DisableAll()
	{
		meatNormal.SetActive(value: false);
		meatDouble.SetActive(value: false);
		foreach (GameObject item in topBun)
		{
			item.SetActive(value: false);
		}
		foreach (GameObject item2 in wrap)
		{
			item2.SetActive(value: false);
		}
		foreach (GameObject pickle in pickles)
		{
			pickle.SetActive(value: false);
		}
		foreach (GameObject item3 in davreLogo)
		{
			item3.SetActive(value: false);
		}
		foreach (GameObject item4 in ribwichLogo)
		{
			item4.SetActive(value: false);
		}
		barcode.SetActive(value: false);
		facility.SetActive(value: false);
	}
}
