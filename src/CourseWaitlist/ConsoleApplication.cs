namespace CourseWaitlist;

public sealed class ConsoleApplication
{
    private readonly IEnrollmentService _service;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public ConsoleApplication(IEnrollmentService service, TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        _service = service;
        _input = input;
        _output = output;
    }

    public int Run()
    {
        _output.WriteLine("Commands: enroll <learner-id> <prerequisite-met:true|false>, drop <learner-id>, status, quit");

        while (_input.ReadLine() is { } line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            switch (parts[0].ToLowerInvariant())
            {
                case "enroll" when parts.Length == 3 && bool.TryParse(parts[2], out var prerequisiteSatisfied):
                    _output.WriteLine(FormatEnrollmentResult(_service.Enroll(parts[1], prerequisiteSatisfied)));
                    break;
                case "drop" when parts.Length == 2:
                    _output.WriteLine(FormatDropResult(_service.Drop(parts[1])));
                    break;
                case "status" when parts.Length == 1:
                    WriteSnapshot(_service.GetSnapshot());
                    break;
                case "quit" when parts.Length == 1:
                    return 0;
                default:
                    _output.WriteLine("Invalid command. Use: enroll <learner-id> <true|false>, drop <learner-id>, status, quit");
                    break;
            }
        }

        return 0;
    }

    private void WriteSnapshot(OfferingSnapshot snapshot)
    {
        _output.WriteLine($"Enrolled ({snapshot.EnrolledLearners.Count}/{snapshot.Capacity}): {string.Join(", ", snapshot.EnrolledLearners)}");
        _output.WriteLine($"Waitlist: {string.Join(", ", snapshot.WaitlistedLearners)}");
    }

    private static string FormatEnrollmentResult(EnrollmentResult result) =>
        result.Status switch
        {
            EnrollmentStatus.Enrolled => "Learner enrolled.",
            EnrollmentStatus.Waitlisted => "Course is full; learner added to the waitlist.",
            EnrollmentStatus.AlreadyEnrolled => "Learner is already enrolled.",
            EnrollmentStatus.AlreadyWaitlisted => "Learner is already waitlisted.",
            EnrollmentStatus.PrerequisiteNotMet => "The course prerequisite is not satisfied.",
            EnrollmentStatus.InvalidLearnerId => "Learner ID must not be blank.",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown enrollment status.")
        };

    private static string FormatDropResult(DropResult result) =>
        result.Status switch
        {
            DropStatus.Dropped when result.PromotedLearnerId is null =>
                "Learner dropped; no eligible waitlisted learner was promoted.",
            DropStatus.Dropped =>
                $"Learner dropped; {result.PromotedLearnerId} was promoted from the waitlist.",
            DropStatus.NotEnrolled => "Learner is not enrolled.",
            DropStatus.InvalidLearnerId => "Learner ID must not be blank.",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown drop status.")
        };
}
