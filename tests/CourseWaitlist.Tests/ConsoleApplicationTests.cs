using CourseWaitlist;
using Xunit;

namespace CourseWaitlist.Tests;

public sealed class ConsoleApplicationTests
{
    [Fact]
    public void RunsEnrollmentDropPromotionAndStatusCommands()
    {
        var service = new EnrollmentService(capacity: 1);
        var input = new StringReader(string.Join(
            Environment.NewLine,
            "EnRoLl learner-1 true",
            "enroll learner-2 true",
            "status",
            "drop learner-1",
            "status",
            "quit"));
        using var output = new StringWriter();
        var application = new ConsoleApplication(service, input, output);

        var exitCode = application.Run();

        Assert.Equal(0, exitCode);
        Assert.Equal(
            new[]
            {
                "Commands: enroll <learner-id> <prerequisite-met:true|false>, drop <learner-id>, status, quit",
                "Learner enrolled.",
                "Course is full; learner added to the waitlist.",
                "Enrolled (1/1): learner-1",
                "Waitlist: learner-2",
                "Learner dropped; learner-2 was promoted from the waitlist.",
                "Enrolled (1/1): learner-2",
                "Waitlist: "
            },
            output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void InvalidCommandsDoNotChangeEnrollmentState()
    {
        var service = new EnrollmentService(capacity: 1);
        var input = new StringReader(string.Join(
            Environment.NewLine,
            "enroll learner maybe",
            "status extra",
            "drop",
            "unknown",
            "status"));
        using var output = new StringWriter();
        var application = new ConsoleApplication(service, input, output);

        application.Run();

        var outputLines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, outputLines.Count(line => line.StartsWith("Invalid command.", StringComparison.Ordinal)));
        Assert.Equal("Enrolled (0/1): ", outputLines[^2]);
        Assert.Equal("Waitlist: ", outputLines[^1]);
        Assert.Empty(service.GetSnapshot().EnrolledLearners);
        Assert.Empty(service.GetSnapshot().WaitlistedLearners);
    }

    [Fact]
    public void ReturnsNormallyWhenInputEndsWithoutQuitCommand()
    {
        var application = new ConsoleApplication(
            new EnrollmentService(capacity: 1),
            new StringReader(string.Empty),
            TextWriter.Null);

        Assert.Equal(0, application.Run());
    }
}
