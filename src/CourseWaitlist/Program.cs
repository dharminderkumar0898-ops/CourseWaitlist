using System.Globalization;
using CourseWaitlist;

var capacity = 2;
if (args.Length > 1 ||
    (args.Length == 1 &&
     (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out capacity) || capacity <= 0)))
{
    Console.Error.WriteLine("Usage: CourseWaitlist [positive-capacity]");
    return 1;
}

var service = new EnrollmentService(capacity);
return new ConsoleApplication(service, Console.In, Console.Out).Run();
