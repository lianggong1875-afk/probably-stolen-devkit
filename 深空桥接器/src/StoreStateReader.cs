using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal static class StoreStateReader
{
    public static JObject Read(string field)
    {
        try
        {
            var s = PlayerStore.Instance;
            JObject result;
            switch (field)
            {
                case "store_state": result = new JObject { ["stage"] = s.storeState.ToString(),["location"] = s.playerLocation.ToString(),
                    ["day"] = StoreStation.GetDayCounter(),["saveSlot"] = s.saveSlotId }; break;
                case "professions": result = new JObject { ["startType"] = s.startType.ToString(),["startScenarioId"] = s.startScenarioId,["appraisalIntroduced"] = s.isAppraisalIntroduced,
                    ["scope"] = "Native start type and appraisal introduction flag; not a registry of mod-defined professions" }; break;
                case "perks": result = new JObject { ["entries"] = s.startingPerks == null ? null : new JArray(s.startingPerks.Select(p => p == null ? (JToken)JValue.CreateNull() : new JObject {
                        ["id"] = p.id,["type"] = p.type.ToString(),["cost"] = p.cost })),
                    ["sourceAvailable"] = s.startingPerks != null,["lastAfterhourOutingDay"] = s.lastAfterhourOutingDay,
                    ["agoraphobiaResting"] = StartingPerkContent.IsAgoraphobiaResting(),["agoraphobiaReadyDay"] = StartingPerkContent.GetAgoraphobiaReadyDay(),
                    ["agoraphobiaRestNightsLeft"] = StartingPerkContent.GetAgoraphobiaRestNightsLeft() }; break;
                case "finance": result = new JObject { ["propertyPaid"] = s.IsPropertyPaid,["payingMortgage"] = s.isPayingMortgage,
                    ["mortgagePrincipal"] = s.propertyBoughtAt,["mortgageDownPayment"] = s.mortgageDownPaid,["mortgageInterestPercent"] = s.mortgageInterestRateTaken,
                    ["mortgageWeeklyPayment"] = s.mortgageWeeklyPayment,["mortgageWeeksLeft"] = s.mortgageWeekLeft,["mortgageTotalWeeks"] = s.mortgageTotalWeeks,
                    ["mortgageRemainingPayments"] = (long)s.mortgageWeekLeft*s.mortgageWeeklyPayment,
                    ["rentValue"] = s.rentValue,["daysUntilRentOrMortgage"] = s.GetRentDayCounter(),
                    ["loanValue"] = s.loanValue,["daysUntilLoan"] = s.GetLoanDayCounter(),
                    ["loanOfferedToGeorgio"] = s.loanOfferedToGeorgio,["dayWhenLoanOfferedGeorgio"] = s.dayWhenLoanOfferedGeorgio }; break;
                case "inspection":
                    var m = s.storeClientManager;
                    if (m == null) return new JObject { ["available"] = false,["reason"] = "customer_manager_unavailable" };
                    result = new JObject { ["countdownActive"] = m.IsInspectionCountdownActive(),["daysUntilInspection"] = m.dayUntilInspection,
                        ["daysSinceInspection"] = m.daySinceInspection,["seeded"] = m.inspectionSeeded,["securityAlarmActive"] = s.isSecurityAlarmActive }; break;
                default: throw new ArgumentException("Unknown store snapshot field: "+field);
            }
            result["available"] = true;
            return result;
        }
        catch (Exception error) { return new JObject { ["available"] = false,["error"] = BridgeFailure.Describe(error,"snapshot."+field) }; }
    }
}
