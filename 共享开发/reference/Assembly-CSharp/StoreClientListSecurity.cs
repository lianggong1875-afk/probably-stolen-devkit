public class StoreClientListSecurity
{
	public const string SECURITY_OFFICER_ID = "securityOfficer";

	public const int FINE_MAX_CRIME = 300;

	public const int ARREST_MAX_CRIME = 1500;

	public static void SetEvidenceGoneDialogue(StoreClient storeClient)
	{
		storeClient.mainDialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_evidence_gone_1")).NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_evidence_gone_2"))
			.NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_evidence_gone_3"))
			.SetEndAction(() =>
			{
				StoreReputation.ModSecReputation(-20);
				PlayerStore.Instance.secData.state = 0;
				DialogUIManager.Instance.CurrentClientLeave();
			});
	}

	public static StoreClient CreateSecurityOfficer()
	{
		StoreClient storeClient = new StoreClient();
		storeClient.identifier = "securityOfficer";
		storeClient.displayName = LocHelper.GetLocalizedClientName("name_security_officer");
		storeClient.spriteName = SpriteDict.GetRandomSecSpriteName();
		storeClient.clientFaction = "FACTION_SECURITY";
		storeClient.dismissable = false;
		storeClient.clientIntent = StoreClient.ClientIntent.DIALOGUE;
		storeClient.mainDialogue.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_intro")).SetStartAction(() =>
		{
			SecData secData = PlayerStore.Instance.secData;
			int totalCrimeValue = secData.GetTotalCrimeValue();
			if (secData.evidenceLevel < 95 || totalCrimeValue <= 0)
			{
				SetEvidenceGoneDialogue(PlayerStore.Instance.currentClientInstance.GetClientBlueprint());
			}
			else if (totalCrimeValue < 300)
			{
				GraftFineDialogue(storeClient);
			}
			else if (totalCrimeValue < 1500)
			{
				GraftArrestDialogue(storeClient);
			}
			else
			{
				GraftExecutionDialogue(storeClient);
			}
		});
		storeClient.CompleteSecClient();
		storeClient.isSentenceOfficer = true;
		storeClient.OnDismiss = () =>
		{
			SecData secData = PlayerStore.Instance.secData;
			if (secData.state != 0 && secData.evidenceLevel >= 95 && secData.GetTotalCrimeValue() > 0)
			{
				secData.state = 0;
				secData.SendOfficer();
			}
		};
		return storeClient;
	}

	private static void GraftFineDialogue(StoreClient storeClient)
	{
		storeClient.mainDialogue.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_intro"));
		Dialogue dialogue = new Dialogue();
		Dialogue dialogue2 = new Dialogue();
		dialogue.SetText("PLAYER", LocHelper.GetLocalizedDialogue("dialog_sec_fine_choice_pay", PlayerStore.Instance.secData.GetFineValue())).NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_pay_1"))
			.SetEndAction(() =>
			{
				StoreReputation.ModSecReputation(-10);
				PlayerStore.Instance.ModCash(-PlayerStore.Instance.secData.GetFineValue());
				DialogUIManager.Instance.CurrentClientLeave();
				PlayerStore.Instance.secData.ResetCrime();
			});
		dialogue2.SetText("PLAYER", LocHelper.GetLocalizedDialogue("dialog_sec_fine_choice_refuse")).NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_refuse_1"))
			.NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_refuse_2"))
			.SetEndAction(() =>
			{
				if (PlayerStore.Instance.playerCash >= PlayerStore.Instance.secData.GetFineValue())
				{
					StoreReputation.ModSecReputation(-10);
					PlayerStore.Instance.ModCash(-PlayerStore.Instance.secData.GetFineValue());
					DialogUIManager.Instance.CurrentClientLeave();
					PlayerStore.Instance.secData.ResetCrime();
				}
				else
				{
					PlayerStore.Instance.ExecuteGameOver("arrest");
				}
			});
		Dialogue dialogue3 = storeClient.mainDialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_crime", PlayerStore.Instance.secData.GetBiggestCrime())).NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_amount", PlayerStore.Instance.secData.GetFineValue()))
			.NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_fine_threat"));
		dialogue3.AddChoice(DirectoryMaster.Item("$"), "", "", dialogue, () =>
		{
		}, () => PlayerStore.Instance.playerCash >= PlayerStore.Instance.secData.GetFineValue());
		dialogue3.AddChoice(DirectoryMaster.Item("$"), "", "", dialogue2, () =>
		{
		}, () => PlayerStore.Instance.playerCash < PlayerStore.Instance.secData.GetFineValue());
	}

	private static void GraftArrestDialogue(StoreClient storeClient)
	{
		storeClient.mainDialogue.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_arrest_intro"));
		storeClient.mainDialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_arrest_crime", PlayerStore.Instance.secData.GetBiggestCrime())).NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_arrest_1"))
			.NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_arrest_2"))
			.SetEndAction(() =>
			{
				PlayerStore.Instance.ExecuteGameOver("arrest");
			});
	}

	private static void GraftExecutionDialogue(StoreClient storeClient)
	{
		storeClient.mainDialogue.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_exec_intro"));
		storeClient.mainDialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_exec_crime", PlayerStore.Instance.secData.GetBiggestCrime())).NextDialogue()
			.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_exec_1"))
			.SetEndAction(() =>
			{
				PlayerStore.Instance.ExecuteGameOver("execution");
			});
	}

	public static StoreClient CreateRevokePermitOfficer()
	{
		StoreClient storeClient = new StoreClient();
		storeClient.identifier = "securityOfficerRevokePermit";
		storeClient.displayName = LocHelper.GetLocalizedClientName("name_security_officer");
		storeClient.spriteName = SpriteDict.GetRandomSecSpriteName();
		storeClient.clientFaction = "FACTION_SECURITY";
		storeClient.dismissable = false;
		storeClient.clientIntent = StoreClient.ClientIntent.DIALOGUE;
		storeClient.mainDialogue.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_revoke_1")).SetStartAction(() =>
		{
			bool flag = Shotgun.WouldPermitRevocationTakeGun();
			Dialogue dialogue = storeClient.mainDialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_revoke_2"));
			if (flag)
			{
				dialogue = dialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedOrDefault("Dialogue", "dialog_sec_revoke_shotgun", "That includes the shotgun. Hand it over."));
			}
			dialogue.NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_revoke_3")).SetEndAction(() =>
			{
				if (PermitHelper.IsGunPermitI() && StoreReputation.GetSecReputation() < 0)
				{
					PermitHelper.OnPermitRevoked(1, "FIREARM");
				}
				if (PermitHelper.IsGunPermitII() && StoreReputation.GetSecReputation() < 30)
				{
					PermitHelper.OnPermitRevoked(2, "FIREARM");
				}
				if (PermitHelper.IsGunPermitIII() && StoreReputation.GetSecReputation() < 60)
				{
					PermitHelper.OnPermitRevoked(3, "FIREARM");
				}
				DialogUIManager.Instance.CurrentClientLeave();
			});
		});
		storeClient.CompleteSecClient();
		return storeClient;
	}

	public static StoreClient CreateAugAllClearOfficer()
	{
		StoreClient storeClient = new StoreClient
		{
			identifier = "securityOfficerAugAllClear",
			displayName = LocHelper.GetLocalizedClientName("name_security_officer"),
			spriteName = SpriteDict.GetRandomSecSpriteName(),
			clientFaction = "FACTION_SECURITY",
			dismissable = false,
			clientIntent = StoreClient.ClientIntent.DIALOGUE
		};
		Dialogue gunLine = new Dialogue();
		storeClient.mainDialogue.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_aug_all_clear_1")).NextDialogue().SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_aug_all_clear_2"))
			.SetEndAction(() =>
			{
				if (!PermitHelper.IsGunPermitI())
				{
					Shotgun.LockShotgun();
				}
			})
			.SetNextDialogue(gunLine);
		gunLine.SetText("CLIENT", LocHelper.GetLocalizedDialogue("dialog_sec_aug_all_clear_3")).SetStartAction(() =>
		{
			if (PermitHelper.IsGunPermitI())
			{
				gunLine.SetText("CLIENT", LocHelper.GetLocalizedOrDefault("Dialogue", "dialog_sec_aug_all_clear_keep", "Your weapon permit covers the shotgun. Keep it, for self defence."));
			}
		}).SetEndAction(() =>
		{
			DialogUIManager.Instance.CurrentClientLeave();
		});
		storeClient.CompleteSecClient();
		return storeClient;
	}
}
