using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property, Inherited = false)]
public sealed class ModRestrictedAttribute : Attribute
{
	public string Rule { get; }

	public string Reason { get; }

	public ModRestrictedAttribute(string rule, string reason = null)
	{
		Rule = rule;
		Reason = reason;
	}
}
