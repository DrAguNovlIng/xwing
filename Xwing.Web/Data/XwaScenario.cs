using System.Diagnostics.CodeAnalysis;

namespace Xwing.Web.Data;

[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "XWA scenarios are numbered 1–5.")]
public enum XwaScenario
{
    AssaultAtSatelliteArray = 1,
    ChanceEngagement = 2,
    SalvageMission = 3,
    ScrambleTheTransmissions = 4,
    AncientKnowledge = 5,
}
