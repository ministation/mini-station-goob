using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI.Roles;

/// <summary>
/// Representative job used for department header icons in the job priority editor.
/// </summary>
public static class DepartmentRepresentativeJobs
{
    private static readonly Dictionary<string, ProtoId<JobPrototype>> Representatives = new()
    {
        ["Command"] = "Captain",
        ["Science"] = "Scientist",
        ["Cargo"] = "CargoTechnician",
        ["Security"] = "SecurityOfficer",
        ["Engineering"] = "StationEngineer",
        ["Medical"] = "MedicalDoctor",
        ["Civilian"] = "HeadOfPersonnel",
        ["Silicon"] = "Borg",
        ["CentralCommand"] = "CentralCommandOfficial",
        ["CentComm"] = "CentralCommandOfficial",
        ["Typan"] = "TypanCommander",
        ["Typan2"] = "TypanBorg",
        ["Legal"] = "Lawyer",
        ["Specific"] = "Reporter",
    };

    public static ProtoId<JobPrototype>? TryGetRepresentative(string departmentId)
    {
        return Representatives.GetValueOrDefault(departmentId);
    }
}
