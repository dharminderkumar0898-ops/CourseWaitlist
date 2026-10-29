# Course Waitlist

A bounded .NET 10 console prototype for enrollment, dropping, and FIFO waitlist promotion for one course offering. State is held in memory and is lost when the process exits.

## Run

Install the .NET 10 SDK. From the repository root:

```powershell
dotnet restore CourseWaitlist.sln
dotnet build CourseWaitlist.sln --no-restore
dotnet test CourseWaitlist.sln --no-build
dotnet run --project src/CourseWaitlist -- 2
```

The optional run argument sets a positive seat capacity (default: `2`). At the prompt, use `enroll <learner-id> <true|false>`, `drop <learner-id>`, `status`, or `quit`. The boolean represents whether the learner satisfies the offering's single prerequisite.

## Design

- `IEnrollmentService.cs` defines the service contract separately from `EnrollmentService.cs`, which owns one offering's state and implements it. `ConsoleApplication` receives the contract through its constructor, keeping command handling and presentation separate from enrollment rules.
- Enrollment and drop results expose domain statuses; user-facing messages are formatted by the console adapter. Offering snapshots contain detached, read-only collections.
- Learner IDs are trimmed and compared case-insensitively. A learner may have at most one active record: enrolled or waitlisted.
- Capacity invariant: the enrolled count never exceeds the positive configured capacity. Enrollment and drop/promotion are atomic under one private lock.
- An eligible learner takes a seat when available. If full, each new valid learner request is queued once, including a request whose prerequisite is currently unmet. Eligibility is recorded with that request.
- On drop, the service dequeues in FIFO order, discards ineligible entries, and promotes the first eligible entry. Ineligible entries are not retained; they can submit a new request after satisfying the prerequisite. If no eligible entry remains, the seat stays open.
- When a seat is available, a learner who does not satisfy the prerequisite is rejected and is not recorded. Duplicate enrolled/waitlisted requests and invalid/repeated drops return explicit statuses without changing state.

The in-memory lock protects concurrent calls within one process only. There is no cross-process coordination, durable state, changing-prerequisite re-evaluation, authentication, or multi-offering support.

## AI Use Record

**Prompt used:** The assessment prompt in this repository task, requesting a bounded single-offering enrollment/drop/waitlist service, documented prerequisite and queue assumptions, focused tests, runnable instructions, and an honest AI-use record while excluding unrelated registration features.

**Relevant AI design summary:** Use one service as the state boundary, serialize state changes with a lock, and evaluate FIFO waitlist entries when a seat opens. The ineligible-front-of-queue policy was made explicit as skip-and-discard; the learner must submit a new request after satisfying the prerequisite. A persistence layer and web API were excluded as outside scope.

**Review decision:** The service boundary, process-local lock, FIFO ordering, and focused tests were accepted. Eligibility is captured on the waitlist request and checked from that stored value during promotion; this is a deliberate simplification because no prerequisite system exists in the starter. Persistence and frontend work were omitted per the scope boundary.

**Files and decisions informed:** `IEnrollmentService.cs` defines the injected service contract. `EnrollmentService.cs` implements enrollment rules, state management, locking, duplicate guards, eligibility checks, and skip-and-discard promotion policy. `ConsoleApplication.cs` handles console commands and formats domain statuses. The test suite covers normal capacity, full offering, duplicates, FIFO promotion, prerequisite failure, invalid/repeated drops, read-only snapshots, console behavior, and concurrent calls. This README records the assumptions and tradeoffs.

**Independent verification:** Tests assert the resulting enrolled/waitlist snapshots and promotion identity, rather than relying only on result statuses. Concurrency tests check capacity and one-active-record invariants across parallel enrollments, including repeated requests for the same learner. Run `dotnet test CourseWaitlist.sln` from the repository root to execute the complete suite.

## Limitations

Prerequisite status is supplied by the caller and frozen when a full-offering request is queued; there is no registrar integration to refresh it. The CLI process is the store, so restart loses all records. The service lock is process-local and does not provide durable or distributed transaction guarantees.
