namespace CourseWaitlist;

public enum EnrollmentStatus
{
    Enrolled,
    Waitlisted,
    AlreadyEnrolled,
    AlreadyWaitlisted,
    PrerequisiteNotMet,
    InvalidLearnerId
}

public enum DropStatus
{
    Dropped,
    NotEnrolled,
    InvalidLearnerId
}

public sealed record EnrollmentResult(EnrollmentStatus Status, string Message);

public sealed record DropResult(DropStatus Status, string? PromotedLearnerId, string Message);

public sealed record OfferingSnapshot(
    int Capacity,
    IReadOnlyList<string> EnrolledLearners,
    IReadOnlyList<string> WaitlistedLearners);

public sealed class EnrollmentService
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly HashSet<string> _enrolled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _waitlisted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<WaitlistEntry> _waitlist = new();

    public EnrollmentService(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public EnrollmentResult Enroll(string? learnerId, bool prerequisiteSatisfied)
    {
        if (!TryNormalizeLearnerId(learnerId, out var normalizedId))
        {
            return new(EnrollmentStatus.InvalidLearnerId, "Learner ID must not be blank.");
        }

        lock (_gate)
        {
            if (_enrolled.Contains(normalizedId))
            {
                return new(EnrollmentStatus.AlreadyEnrolled, "Learner is already enrolled.");
            }

            if (_waitlisted.Contains(normalizedId))
            {
                return new(EnrollmentStatus.AlreadyWaitlisted, "Learner is already waitlisted.");
            }

            if (_enrolled.Count < _capacity)
            {
                if (!prerequisiteSatisfied)
                {
                    return new(EnrollmentStatus.PrerequisiteNotMet, "The course prerequisite is not satisfied.");
                }

                _enrolled.Add(normalizedId);
                return new(EnrollmentStatus.Enrolled, "Learner enrolled.");
            }

            _waitlist.Enqueue(new WaitlistEntry(normalizedId, prerequisiteSatisfied));
            _waitlisted.Add(normalizedId);
            return new(EnrollmentStatus.Waitlisted, "Course is full; learner added to the waitlist.");
        }
    }

    public DropResult Drop(string? learnerId)
    {
        if (!TryNormalizeLearnerId(learnerId, out var normalizedId))
        {
            return new(DropStatus.InvalidLearnerId, null, "Learner ID must not be blank.");
        }

        lock (_gate)
        {
            if (!_enrolled.Remove(normalizedId))
            {
                return new(DropStatus.NotEnrolled, null, "Learner is not enrolled.");
            }

            string? promotedLearnerId = null;
            while (_enrolled.Count < _capacity && _waitlist.TryDequeue(out var candidate))
            {
                _waitlisted.Remove(candidate.LearnerId);
                if (!candidate.PrerequisiteSatisfied)
                {
                    continue;
                }

                _enrolled.Add(candidate.LearnerId);
                promotedLearnerId = candidate.LearnerId;
            }

            var message = promotedLearnerId is null
                ? "Learner dropped; no eligible waitlisted learner was promoted."
                : $"Learner dropped; {promotedLearnerId} was promoted from the waitlist.";
            return new(DropStatus.Dropped, promotedLearnerId, message);
        }
    }

    public OfferingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new OfferingSnapshot(_capacity, _enrolled.ToArray(), _waitlist.Select(entry => entry.LearnerId).ToArray());
        }
    }

    private static bool TryNormalizeLearnerId(string? learnerId, out string normalizedId)
    {
        normalizedId = learnerId?.Trim() ?? string.Empty;
        return normalizedId.Length > 0;
    }

    private sealed record WaitlistEntry(string LearnerId, bool PrerequisiteSatisfied);
}