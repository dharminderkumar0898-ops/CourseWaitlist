using System.Collections.Concurrent;
using System.Collections.Generic;
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
        var snapshot = service.GetSnapshot();
        Assert.Single(snapshot.EnrolledLearners);
        Assert.Single(snapshot.WaitlistedLearners);
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
        Assert.Equal(EnrollmentStatus.Waitlisted, service.Enroll("ineligible", prerequisiteSatisfied: true).Status);
        Assert.Equal(new[] { "ineligible" }, service.GetSnapshot().WaitlistedLearners);
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
    public void AllowsLearnerToEnrollAfterPrerequisiteIsSatisfied()
    {
        var service = new EnrollmentService(capacity: 1);

        Assert.Equal(EnrollmentStatus.PrerequisiteNotMet, service.Enroll("learner-1", false).Status);
        Assert.Equal(EnrollmentStatus.Enrolled, service.Enroll("learner-1", true).Status);
        Assert.Equal(new[] { "learner-1" }, service.GetSnapshot().EnrolledLearners);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RejectsBlankEnrollmentIdsWithoutChangingState(string? learnerId)
    {
        var service = new EnrollmentService(capacity: 1);

        Assert.Equal(EnrollmentStatus.InvalidLearnerId, service.Enroll(learnerId, true).Status);

        var snapshot = service.GetSnapshot();
        Assert.Empty(snapshot.EnrolledLearners);
        Assert.Empty(snapshot.WaitlistedLearners);
    }

    [Fact]
    public void DropOfWaitlistedLearnerDoesNotRemoveThemFromTheWaitlist()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("incumbent", true);
        service.Enroll("waitlisted", true);

        var result = service.Drop("waitlisted");

        Assert.Equal(DropStatus.NotEnrolled, result.Status);
        Assert.Null(result.PromotedLearnerId);
        Assert.Equal(new[] { "incumbent" }, service.GetSnapshot().EnrolledLearners);
        Assert.Equal(new[] { "waitlisted" }, service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void DropNormalizesLearnerIdAndPromotesOnlyOnce()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", true);
        service.Enroll("learner-2", true);

        var result = service.Drop(" LEARNER-1 ");
        var repeatedDrop = service.Drop("learner-1");

        Assert.Equal(DropStatus.Dropped, result.Status);
        Assert.Equal("learner-2", result.PromotedLearnerId);
        Assert.Equal(DropStatus.NotEnrolled, repeatedDrop.Status);
        Assert.Null(repeatedDrop.PromotedLearnerId);
        Assert.Equal(new[] { "learner-2" }, service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void DropPromotesEligibleLearnersInFifoOrderUntilCapacityIsFilled()
    {
        var service = new EnrollmentService(capacity: 2);
        service.Enroll("incumbent-1", true);
        service.Enroll("incumbent-2", true);
        service.Enroll("ineligible", false);
        service.Enroll("first-eligible", true);
        service.Enroll("second-eligible", true);
        service.Enroll("still-waitlisted", true);

        var result = service.Drop("incumbent-1");

        var snapshot = service.GetSnapshot();
        Assert.Equal("first-eligible", result.PromotedLearnerId);
        Assert.Equal(2, snapshot.EnrolledLearners.Count);
        Assert.Contains("incumbent-2", snapshot.EnrolledLearners);
        Assert.Contains("first-eligible", snapshot.EnrolledLearners);
        Assert.Equal(new[] { "second-eligible", "still-waitlisted" }, snapshot.WaitlistedLearners);
    }

    [Fact]
    public void LeavesVacancyWhenWaitlistContainsNoEligibleLearners()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("incumbent", true);
        service.Enroll("ineligible-1", false);
        service.Enroll("ineligible-2", false);

        var result = service.Drop("incumbent");

        Assert.Equal(DropStatus.Dropped, result.Status);
        Assert.Null(result.PromotedLearnerId);
        Assert.Empty(service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
        Assert.Equal(EnrollmentStatus.Enrolled, service.Enroll("new-learner", true).Status);
    }

    [Fact]
    public void PreviouslyReturnedSnapshotDoesNotChangeAfterEnrollmentAndDrop()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", true);
        var snapshot = service.GetSnapshot();

        service.Enroll("learner-2", true);
        service.Drop("learner-1");

        Assert.Equal(new[] { "learner-1" }, snapshot.EnrolledLearners);
        Assert.Empty(snapshot.WaitlistedLearners);
        Assert.Equal(new[] { "learner-2" }, service.GetSnapshot().EnrolledLearners);
    }

    [Fact]
    public void SnapshotCollectionsCannotBeMutated()
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", true);
        var snapshot = service.GetSnapshot();
        var enrolledLearners = Assert.IsAssignableFrom<IList<string>>(snapshot.EnrolledLearners);

        Assert.Throws<NotSupportedException>(() => enrolledLearners[0] = "changed");
        Assert.Equal(new[] { "learner-1" }, service.GetSnapshot().EnrolledLearners);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RejectsBlankDropIdsWithoutChangingState(string? learnerId)
    {
        var service = new EnrollmentService(capacity: 1);
        service.Enroll("learner-1", true);

        Assert.Equal(DropStatus.InvalidLearnerId, service.Drop(learnerId).Status);
        Assert.Equal(new[] { "learner-1" }, service.GetSnapshot().EnrolledLearners);
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
    public void ConcurrentDuplicateRequestsCreateOnlyOneEnrollment()
    {
        const int requestCount = 100;
        var service = new EnrollmentService(capacity: 1);
        var results = new ConcurrentBag<EnrollmentResult>();

        Parallel.For(0, requestCount, _ =>
            results.Add(service.Enroll("same-learner", prerequisiteSatisfied: true)));

        Assert.Single(results, result => result.Status == EnrollmentStatus.Enrolled);
        Assert.Equal(requestCount - 1, results.Count(result => result.Status == EnrollmentStatus.AlreadyEnrolled));
        Assert.Equal(new[] { "same-learner" }, service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnrollmentService(capacity));
    }
}