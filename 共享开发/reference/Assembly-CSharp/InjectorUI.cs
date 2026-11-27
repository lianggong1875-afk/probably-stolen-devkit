using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InjectorUI : MonoBehaviour
{
	public TextMeshProUGUI serialTMP;

	public TextMeshProUGUI companyTMP;

	public TextMeshProUGUI modelTMP;

	public List<GameObject> display;

	public List<GameObject> color;

	public List<GameObject> broken;

	public GameObject body;

	private void Start()
	{
	}

	private void Update()
	{
	}

	public void OnInit(GameItem gameItem)
	{
		if (!gameItem.IsTag("INS_INJECTOR_TAG"))
		{
			return;
		}
		DisableAll();
		int num = gameItem.GetTagReadonly("INJECTOR_INDICATOR").GetInt();
		int num2 = gameItem.GetTagReadonly("INJECTOR_BODY").GetInt();
		int num3 = gameItem.GetTagReadonly("INJECTOR_COLOR").GetInt();
		string text = gameItem.GetTagReadonly("INJECTOR_COMPANY").GetString();
		string text2 = gameItem.GetTagReadonly("INJECTOR_MODEL").GetString();
		string text3 = gameItem.GetTagReadonly("INJECTOR_SERIAL").GetString();
		if (num != -1)
		{
			display[num].SetActive(value: true);
		}
		if (num2 != -1)
		{
			body.SetActive(value: true);
			if (num2 != 0)
			{
				body.SetActive(value: false);
				broken[num2 - 1].SetActive(value: true);
			}
		}
		if (num3 != -1)
		{
			body.SetActive(value: false);
			color[num3].SetActive(value: true);
		}
		serialTMP.text = text3;
		companyTMP.text = text;
		modelTMP.text = text2;
	}

	public void DisableAll()
	{
		body.SetActive(value: false);
		serialTMP.text = "";
		companyTMP.text = "";
		modelTMP.text = "";
		foreach (GameObject item in display)
		{
			item.SetActive(value: false);
		}
		foreach (GameObject item2 in color)
		{
			item2.SetActive(value: false);
		}
		foreach (GameObject item3 in broken)
		{
			item3.SetActive(value: false);
		}
	}
}
