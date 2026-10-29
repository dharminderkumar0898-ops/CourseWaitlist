namespace CourseWaitlist;

public interface IEnrollmentService
{
    EnrollmentResult Enroll(string? learnerId, bool prerequisiteSatisfied);

    DropResult Drop(string? learnerId);

    OfferingSnapshot GetSnapshot();
}
