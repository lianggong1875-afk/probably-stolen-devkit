public class CombatAbilityEffectPermanentDirectory : CombatAbilityEffectDirectory
{
	protected override void InitDirectory()
	{
		Add("extreme_dehydration", () =>
		{
			CombatAbilityEffect combatAbilityEffect = new CombatAbilityEffect("extreme_dehydration", -6, 999, CombatAbilityEffect.EffectType.debuff, CombatAbilityEffect.TargetType.singleFriendly, CombatEntity.EffectType.accuracyPercentage, "Extreme Dehydration", 1, 100, CombatAbilityEffect.ValueType.standard, isPermanent: true);
			combatAbilityEffect.EnableTag(CombatAbilityEffect.EffectTag.survivalThirst);
			return combatAbilityEffect;
		});
		Add("dehydration", () =>
		{
			CombatAbilityEffect combatAbilityEffect = new CombatAbilityEffect("dehydration", -4, 999, CombatAbilityEffect.EffectType.debuff, CombatAbilityEffect.TargetType.singleFriendly, CombatEntity.EffectType.accuracyPercentage, "Dehydration", 1, 100, CombatAbilityEffect.ValueType.standard, isPermanent: true);
			combatAbilityEffect.EnableTag(CombatAbilityEffect.EffectTag.survivalThirst);
			return combatAbilityEffect;
		});
		Add("thirsty", () =>
		{
			CombatAbilityEffect combatAbilityEffect = new CombatAbilityEffect("thirsty", -2, 999, CombatAbilityEffect.EffectType.debuff, CombatAbilityEffect.TargetType.singleFriendly, CombatEntity.EffectType.accuracyPercentage, "Thirsty", 1, 100, CombatAbilityEffect.ValueType.standard, isPermanent: true);
			combatAbilityEffect.EnableTag(CombatAbilityEffect.EffectTag.survivalThirst);
			return combatAbilityEffect;
		});
		Add("well_hydrated", () =>
		{
			CombatAbilityEffect combatAbilityEffect = new CombatAbilityEffect("well_hydrated", 5, 999, CombatAbilityEffect.EffectType.buff, CombatAbilityEffect.TargetType.self, CombatEntity.EffectType.accuracyPercentage, "Well Hydrated", 1, 100, CombatAbilityEffect.ValueType.standard, isPermanent: true);
			combatAbilityEffect.EnableTag(CombatAbilityEffect.EffectTag.survivalThirst);
			return combatAbilityEffect;
		});
		Add("dead", () => new CombatAbilityEffect("dead", 1, 999, CombatAbilityEffect.EffectType.debuff, CombatAbilityEffect.TargetType.none, CombatEntity.EffectType.none, "Dead", 1, 100, CombatAbilityEffect.ValueType.standard, isPermanent: true));
	}
}
