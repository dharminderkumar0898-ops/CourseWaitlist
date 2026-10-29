using CourseWaitlist;

var capacity = args.Length == 0
    ? 2
    : int.TryParse(args[0], out var parsedCapacity) ? parsedCapacity : 0;

if (capacity <= 0)
{
    Console.Error.WriteLine("Usage: CourseWaitlist [positive-capacity]");
    return 1;
}

var service = new EnrollmentService(capacity);
Console.WriteLine("Commands: enroll <learner-id> <prerequisite-met:true|false>, drop <learner-id>, status, quit");

while (Console.ReadLine() is { } line)
{
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
    {
        continue;
    }

    switch (parts[0].ToLowerInvariant())
    {
        case "enroll" when parts.Length == 3 && bool.TryParse(parts[2], out var prerequisiteSatisfied):
            Console.WriteLine(service.Enroll(parts[1], prerequisiteSatisfied).Message);
            break;
        case "drop" when parts.Length == 2:
            Console.WriteLine(service.Drop(parts[1]).Message);
            break;
        case "status" when parts.Length == 1:
            var snapshot = service.GetSnapshot();
            Console.WriteLine($"Enrolled ({snapshot.EnrolledLearners.Count}/{snapshot.Capacity}): {string.Join(", ", snapshot.EnrolledLearners)}");
            Console.WriteLine($"Waitlist: {string.Join(", ", snapshot.WaitlistedLearners)}");
            break;
        case "quit" when parts.Length == 1:
            return 0;
        default:
            Console.WriteLine("Invalid command. Use: enroll <learner-id> <true|false>, drop <learner-id>, status, quit");
            break;
    }
}

return 0;