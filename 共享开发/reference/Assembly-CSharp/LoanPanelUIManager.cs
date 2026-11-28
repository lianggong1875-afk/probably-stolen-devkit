using System.Globalization;
using TMPro;
using UnityEngine;

public class LoanPanelUIManager : MonoBehaviour
{
	public GameObject panel;

	public TextMeshProUGUI body;

	public static LoanPanelUIManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(this);
		}
		else
		{
			Instance = this;
		}
	}

	private void Start()
	{
	}

	private void Update()
	{
	}

	public void OpenUI()
	{
		panel.SetActive(value: true);
		PaperUIManager.Instance.audioSourcePickup.Play();
		PlayerStore instance = PlayerStore.Instance;
		bool flag = instance.propertyBoughtAt != 0;
		int num = (flag ? instance.propertyBoughtAt : instance.GetStoreBuyPrice(applyDownPayment: true));
		int num2;
		if (flag)
		{
			num2 = ((instance.mortgageDownPaid != 0) ? instance.mortgageDownPaid : 3000);
		}
		else
		{
			num2 = instance.GetMortgageDownPayment();
		}
		int num3;
		if (flag)
		{
			num3 = ((instance.mortgageTotalWeeks != 0) ? instance.mortgageTotalWeeks : 14);
		}
		else
		{
			num3 = PlayerStore.GetMortgageWeeks();
		}
		int num4;
		if (flag)
		{
			num4 = ((instance.mortgageInterestRateTaken != 0) ? instance.mortgageInterestRateTaken : 100);
		}
		else
		{
			num4 = Difficulty.Rules.mortgageInterestRate;
		}
		int num5 = ((instance.mortgageWeeklyPayment != 0) ? instance.mortgageWeeklyPayment : PlayerStore.CalculateWeeklyPayment(num, num4, num3));
		string text = ((float)num4 / 52f).ToString("0.###", CultureInfo.InvariantCulture);
		body.text = "\n" + LocHelper.GetLocalizedUI("ui_loan_contract_amount", num) + "\n" + LocHelper.GetLocalizedUI("ui_loan_contract_down_payment", num2) + "\n" + LocHelper.GetLocalizedUI("ui_loan_contract_interest", num4, text) + "\n" + LocHelper.GetLocalizedUI("ui_loan_contract_repayment", num3, num5) + "\n";
	}

	public void CloseUI()
	{
		panel.SetActive(value: false);
		PaperUIManager.Instance.audioSourceDrop.Play();
	}
}
