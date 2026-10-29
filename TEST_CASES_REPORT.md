# Test Cases and Execution Report

## Execution summary

- **Execution date:** 2026-10-29
- **Command:** `dotnet test CourseWaitlist\CourseWaitlist.sln --configuration Release`
- **Result:** Passed — 29 passed, 0 failed, 0 skipped.
- **Test project:** `CourseWaitlist.Tests` targeting .NET 10.

## Enrollment service test cases

| Test case | Expected behavior |
|---|---|
| Enroll eligible learner with an available seat | Learner is enrolled and waitlist remains empty. |
| Enroll when course is full | Learner is appended to the waitlist. |
| Submit duplicate enrollment/waitlist requests | Correct duplicate status is returned and no duplicate record is created. |
| Reject an unmet prerequisite when a seat is available | Request is rejected; learner is not recorded. |
| Retry after satisfying the prerequisite | Learner can enroll if a seat is available. |
| Submit a null, empty, or whitespace-only learner ID | Request is rejected without changing state. |
| Drop a learner and promote from the waitlist | Earliest eligible waitlisted learner is promoted. |
| Encounter ineligible waitlist entries during promotion | Ineligible entries are discarded; promotion continues in FIFO order. |
| Drop when all waitlisted learners are ineligible | No learner is promoted and the seat remains available. |
| Drop an unknown or waitlisted learner | Drop is rejected; current enrollment and waitlist are unchanged. |
| Drop using a case/whitespace variant of an enrolled ID | Matching learner is dropped and promoted at most once. |
| Promote across multiple available seats | Eligible entries are promoted in FIFO order until capacity is filled. |
| Read a snapshot, then change service state | Previously returned snapshot remains unchanged. |
| Attempt to modify a snapshot collection | Mutation is rejected; service state remains unchanged. |
| Enroll many distinct learners concurrently | Capacity is not exceeded, IDs are unique, and every request has a result. |
| Submit the same learner concurrently | Exactly one enrollment is created; other requests report a duplicate. |
| Construct service with zero or negative capacity | Constructor throws `ArgumentOutOfRangeException`. |

## Console application test cases

| Test case | Expected behavior |
|---|---|
| Run enrollment, waitlist, status, drop, and promotion commands | Commands produce the expected output and updated state. |
| Enter malformed or unknown commands | Invalid-command feedback is shown without changing enrollment state. |
| End input without issuing `quit` | Application exits normally. |

## Scope note

This report covers automated tests for the current in-memory console solution. It does not cover external registrar, identity, persistence, or distributed-service integrations.
