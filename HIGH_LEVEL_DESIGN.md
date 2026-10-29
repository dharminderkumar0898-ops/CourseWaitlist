# High-Level Design: Course Waitlist

## 1. Purpose and scope

Course Waitlist is a .NET console application for managing enrollment in one course offering. It supports enrolling learners, placing requests on a FIFO waitlist, dropping enrolled learners, and promoting eligible waitlisted learners when seats open.

The application is an in-memory prototype. It does not provide persistence, authentication, registrar integration, multiple offerings, or coordination between processes.

## 2. System context

```text
User
  |
  | console commands
  v
Program (composition root)
  |
  +--> ConsoleApplication (parse commands, format responses)
          |
          | IEnrollmentService
          v
      EnrollmentService (rules and in-memory state)
```

The user interacts through standard input and output. `Program` validates the optional capacity argument, constructs the service and console adapter, and starts the command loop.

## 3. Components

### Program

- Parses an optional positive course capacity; defaults to two seats.
- Creates the service and injects it into the console application.
- Returns a nonzero exit code when command-line capacity is invalid.

### ConsoleApplication

- Reads and validates `enroll`, `drop`, `status`, and `quit` commands.
- Calls the service through `IEnrollmentService`.
- Formats domain results and offering snapshots for display.
- Accepts injected input and output streams so command behavior can be tested without an interactive terminal.

### IEnrollmentService

- `IEnrollmentService.cs` defines the service operations used by the console adapter.
- `ConsoleApplication` receives the interface through its constructor, so the concrete service can be replaced without changing command handling.

### EnrollmentService

- `EnrollmentService.cs` implements `IEnrollmentService` and enforces enrollment, capacity, duplicate, prerequisite, and promotion rules.
- A private lock serializes state-changing operations and snapshot reads within the process.
- The service returns statuses and data; presentation text is the console adapter's responsibility.

## 4. State model

Each service instance represents one offering and holds:

- A fixed, positive capacity.
- A set of enrolled learner IDs.
- A FIFO queue of waitlist entries.
- A set of waitlisted learner IDs for duplicate detection.

Learner IDs are trimmed and compared case-insensitively. A learner can have at most one active record: enrolled or waitlisted. A waitlist entry records prerequisite eligibility at the time of the request.

Snapshots contain detached, read-only collections so callers cannot mutate service state through a returned snapshot.

## 5. Main workflows

### Enroll

1. Normalize the learner ID; reject null, empty, or whitespace-only IDs.
2. Return an explicit duplicate status if the learner is already enrolled or waitlisted.
3. If a seat is available, enroll the learner only when the prerequisite is satisfied; otherwise reject the request.
4. If the offering is full, append the request to the waitlist in FIFO order, recording its prerequisite status.

### Drop and promote

1. Normalize the learner ID; reject blank IDs or IDs that are not enrolled.
2. Remove the learner from enrollment.
3. Dequeue waitlist entries in FIFO order until a seat is filled or the queue is empty.
4. Discard entries whose recorded prerequisite status is unmet; promote the first eligible entry.
5. If no eligible entry remains, leave the seat open.

An ineligible entry is discarded rather than retained. That learner may submit a new request after satisfying the prerequisite.

### Status

The adapter obtains a snapshot and displays the enrolled learners, capacity, and current waitlist order.

## 6. Key design decisions

- **Single service boundary:** Enrollment state and rules are owned by one service rather than the console.
- **Interface-based dependency:** The console depends on `IEnrollmentService`, enabling isolated tests and alternate adapters.
- **Atomic in-process operations:** A private lock keeps enrollment, drop/promotion, and snapshot reads consistent for concurrent calls in one process.
- **FIFO waitlist:** Eligible learners are considered in request order; ineligible entries are skipped and discarded.
- **Caller-provided prerequisite status:** Eligibility is a boolean supplied for each request and frozen while waitlisted because no prerequisite system is integrated.
- **No persistence:** The service instance is the store; all state is lost when the process exits.

## 7. Quality attributes and constraints

- **Consistency:** Enrollment count does not exceed capacity, and duplicate active records are prevented by serialized service operations.
- **Testability:** Domain behavior and command handling are independently testable; console streams are injectable.
- **Simplicity:** The implementation is intentionally limited to one offering and one process.
- **Scalability and durability:** Not provided. A multi-user or production deployment would require persistent storage and transactional coordination.
- **Identity and authorization:** Not provided. Learner IDs are identifiers only and are not authenticated.

## 8. Build, run, and test

From the repository root:

```powershell
dotnet build CourseWaitlist.sln
dotnet test CourseWaitlist.sln
dotnet run --project src/CourseWaitlist -- 2
```

The run argument is optional and sets the positive seat capacity. At the prompt, use:

```text
enroll <learner-id> <true|false>
drop <learner-id>
status
quit
```
