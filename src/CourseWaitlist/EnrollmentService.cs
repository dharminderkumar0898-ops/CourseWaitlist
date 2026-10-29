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

public sealed record EnrollmentResult(EnrollmentStatus Status);

public sealed record DropResult(DropStatus Status, string? PromotedLearnerId);

public sealed record OfferingSnapshot(
    int Capacity,
    IReadOnlyList<string> EnrolledLearners,
    IReadOnlyList<string> WaitlistedLearners);

public sealed class EnrollmentService : IEnrollmentService
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
            return new(EnrollmentStatus.InvalidLearnerId);
        }

        lock (_gate)
        {
            if (_enrolled.Contains(normalizedId))
            {
                return new(EnrollmentStatus.AlreadyEnrolled);
            }

            if (_waitlisted.Contains(normalizedId))
            {
                return new(EnrollmentStatus.AlreadyWaitlisted);
            }

            if (_enrolled.Count < _capacity)
            {
                if (!prerequisiteSatisfied)
                {
                    return new(EnrollmentStatus.PrerequisiteNotMet);
                }

                _enrolled.Add(normalizedId);
                return new(EnrollmentStatus.Enrolled);
            }

            _waitlist.Enqueue(new WaitlistEntry(normalizedId, prerequisiteSatisfied));
            _waitlisted.Add(normalizedId);
            return new(EnrollmentStatus.Waitlisted);
        }
    }

    public DropResult Drop(string? learnerId)
    {
        if (!TryNormalizeLearnerId(learnerId, out var normalizedId))
        {
            return new(DropStatus.InvalidLearnerId, null);
        }

        lock (_gate)
        {
            if (!_enrolled.Remove(normalizedId))
            {
                return new(DropStatus.NotEnrolled, null);
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

            return new(DropStatus.Dropped, promotedLearnerId);
        }
    }

    public OfferingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new OfferingSnapshot(
                _capacity,
                Array.AsReadOnly(_enrolled.ToArray()),
                Array.AsReadOnly(_waitlist.Select(entry => entry.LearnerId).ToArray()));
        }
    }

    private static bool TryNormalizeLearnerId(string? learnerId, out string normalizedId)
    {
        normalizedId = learnerId?.Trim() ?? string.Empty;
        return normalizedId.Length > 0;
    }

    private sealed record WaitlistEntry(string LearnerId, bool PrerequisiteSatisfied);
}