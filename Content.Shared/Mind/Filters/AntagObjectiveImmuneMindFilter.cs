using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;

namespace Content.Shared.Mind.Filters;

/// <summary>
/// A mind filter that removes minds whose job belongs to a department marked
/// <see cref="DepartmentPrototype.AntagObjectiveImmune"/> (faction roles such as
/// Central Command and Taipan staff), so they can never be picked as an objective target.
/// </summary>
public sealed partial class AntagObjectiveImmuneMindFilter : MindFilter
{
    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? exclude, IEntityManager entMan, SharedMindSystem mindSys)
    {
        return IsJobObjectiveImmune(mind.Owner, entMan);
    }

    /// <summary>
    /// Returns true if the job of the given mind belongs to a department that must not be
    /// targeted by antagonist objectives.
    /// </summary>
    public static bool IsJobObjectiveImmune(EntityUid mindId, IEntityManager entMan)
    {
        var jobSys = entMan.System<SharedJobSystem>();
        if (!jobSys.MindTryGetJobId(mindId, out var jobId) || jobId is not { } job)
            return false;

        if (!jobSys.TryGetAllDepartments(job.Id, out var departments))
            return false;

        foreach (var department in departments)
        {
            if (department.AntagObjectiveImmune)
                return true;
        }

        return false;
    }
}
