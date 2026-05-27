namespace Demo.Orchestrations.SequentialHybrid.Models;

/// <summary>
/// The identity of the user the demo is being run for. This stays constant
/// across all workflow runs so the fake inbox, the redactor's token map, and
/// the cloud-side reply writer can all reference the same person.
///
/// Used by:
///   - <see cref="Services.InboxService"/> when generating fake emails
///     (every email's <c>to:</c> field is this user).
///   - <see cref="EmailTriageWorkflow"/> redactor prompt — the redactor is
///     told the user's identity explicitly so it always emits the dedicated
///     <c>PERSON_USER</c> / <c>EMAIL_USER</c> tokens for them.
///   - Cloud reply writer — knows to sign with <c>PERSON_USER</c>.
/// </summary>
public static class UserProfile
{
    public const string Name = "Alex Park";
    public const string Email = "alex.park@aurora-labs.com";
}
