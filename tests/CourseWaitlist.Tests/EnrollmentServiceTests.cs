using System.Collections.Concurrent;
using CourseWaitlist;
using Xunit;

namespace CourseWaitlist.Tests;

public sealed class EnrollmentServiceTests
{
    [Fact]
    public void EnrollsEligibleLearnerWhenCapacityIsAvailable()
    {
        var service = new EnrollmentService(capacity: 1);

        var result = service.Enroll("learner-1", prerequisiteSatisfied: true);

        Assert.Equal(EnrollmentStatus.Enrolled, result.Status);
        Assert.Equal(new[] { "learner-1" }, service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void AddsLearnersToWaitlistWhenCourseIsFull()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", prerequisiteSatisfied: true);

        var result = service.Enroll("learner-2", prerequisiteSatisfied: true);

        Assert.Equal(EnrollmentStatus.Waitlisted, result.Status);
        Assert.Equal(new[] { "learner-2" }, service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void DuplicateRequestsDoNotCreateDuplicateRecords()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", prerequisiteSatisfied: true);
        service.Enroll("learner-2", prerequisiteSatisfied: true);

        Assert.Equal(EnrollmentStatus.AlreadyEnrolled, service.Enroll(" LEARNER-1 ", true).Status);
        Assert.Equal(EnrollmentStatus.AlreadyWaitlisted, service.Enroll("learner-2", true).Status);
        Assert.Equal(1, service.GetSnapshot().EnrolledLearners.Count);
        Assert.Equal(1, service.GetSnapshot().WaitlistedLearners.Count);
    }

    [Fact]
    public void DropPromotesEarliestEligibleWaitlistedLearner()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", prerequisiteSatisfied: true);
        service.Enroll("learner-2", prerequisiteSatisfied: true);
        service.Enroll("learner-3", prerequisiteSatisfied: true);

        var result = service.Drop("learner-1");

        Assert.Equal(DropStatus.Dropped, result.Status);
        Assert.Equal("learner-2", result.PromotedLearnerId);
        Assert.Equal(new[] { "learner-2" }, service.GetSnapshot().EnrolledLearners);
        Assert.Equal(new[] { "learner-3" }, service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void PromotionDiscardsIneligibleEntriesAndContinuesInFifoOrder()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("incumbent", prerequisiteSatisfied: true);
        service.Enroll("ineligible", prerequisiteSatisfied: false);
        service.Enroll("eligible", prerequisiteSatisfied: true);

        var result = service.Drop("incumbent");

        Assert.Equal("eligible", result.PromotedLearnerId);
        Assert.Equal(new[] { "eligible" }, service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
        Assert.Equal(EnrollmentStatus.Enrolled, service.Enroll("ineligible", prerequisiteSatisfied: true).Status);
    }

    [Fact]
    public void RejectsIneligibleLearnerWhenASeatIsAvailable()
    {
        var service = new EnrollmentService(capacity: 1);

        var result = service.Enroll("learner-1", prerequisiteSatisfied: false);

        Assert.Equal(EnrollmentStatus.PrerequisiteNotMet, result.Status);
        Assert.Empty(service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void InvalidAndRepeatedDropsLeaveStateUnchanged()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", prerequisiteSatisfied: true);

        Assert.Equal(DropStatus.InvalidLearnerId, service.Drop(" ").Status);
        Assert.Equal(DropStatus.Dropped, service.Drop("learner-1").Status);
        Assert.Equal(DropStatus.NotEnrolled, service.Drop("learner-1").Status);
        Assert.Empty(service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void ConcurrentEnrollmentsNeverExceedCapacityOrDuplicateLearners()
    {
        const int requestCount = 100;
        const int capacity = 4;
        var service = new EnrollmentService(capacity);
        var results = new ConcurrentBag<EnrollmentResult>();

        Parallel.For(0, requestCount, index =>
            results.Add(service.Enroll($"learner-{index}", prerequisiteSatisfied: true)));

        var snapshot = service.GetSnapshot();
        Assert.Equal(capacity, snapshot.EnrolledLearners.Count);
        Assert.Equal(requestCount - capacity, snapshot.WaitlistedLearners.Count);
        Assert.Equal(requestCount, snapshot.EnrolledLearners.Concat(snapshot.WaitlistedLearners).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(requestCount, results.Count);
    }

    [Fact]
    public void RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnrollmentService(0));
    }
}