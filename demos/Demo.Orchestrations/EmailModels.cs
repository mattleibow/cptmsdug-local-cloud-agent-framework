using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Demo.Orchestrations;

/// <summary>
/// Structured payload produced by the on-device inbox-picker agent. Flat
/// fields are easier for smaller on-device models to fill in reliably than
/// nested objects.
///
/// Stored in workflow state under <see cref="CloudPromptAdapter.PickedEmailStateKey"/>
/// by the cloud-prompt adapter; read back by the final-email assembler at
/// the end of the workflow.
/// </summary>
[Description(
    "The single inbox email the user wants to reply to, copied verbatim " +
    "from the matching entry in the RAG-injected inbox context.")]
public sealed record PickedEmail(
    [property: Description(
        "Full name of the colleague who SENT the email — copy the FROM_NAME " +
        "value from the chosen inbox entry. NEVER use the user's own name here.")]
    string FromFullName,

    [property: Description(
        "Email address of the colleague who SENT the email — copy the " +
        "FROM_EMAIL value from the chosen inbox entry.")]
    string FromEmail,

    [property: Description(
        "Full name of the recipient — this is always the user, copied from " +
        "the TO_NAME value of the chosen inbox entry.")]
    string ToFullName,

    [property: Description(
        "Recipient email — this is always the user's email, copied from the " +
        "TO_EMAIL value of the chosen inbox entry.")]
    string ToEmail,

    [property: Description(
        "Subject line of the chosen inbox entry, copied verbatim.")]
    string Subject,

    [property: Description(
        "FULL body of the chosen inbox entry, copied character-for-character. " +
        "Never write a placeholder description like '<email body>'. Never " +
        "summarise. Never shorten. Preserve all paragraphs, dates, dollar " +
        "amounts, and named people.")]
    string Body)
{
    /// <summary>First-name-only view of the sender, used in cloud-bound prompts.</summary>
    [JsonIgnore]
    public string FromFirstName => FirstNameFrom(FromFullName, FromEmail);

    /// <summary>First-name-only view of the recipient (the user), used in cloud-bound prompts.</summary>
    [JsonIgnore]
    public string ToFirstName => FirstNameFrom(ToFullName, ToEmail);

    private static string FirstNameFrom(string? fullName, string? email) =>
        fullName?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        ?? email?.Split('@').FirstOrDefault()
        ?? "<unknown>";
}

/// <summary>
/// One entity the body-redactor agent identified inside the email body
/// (e.g. a last name, company, project, or dollar amount). The actual
/// token assignment and string substitution happens later in code — the
/// AI's only job here is to find and classify entities.
/// </summary>
[Description("A single sensitive piece of text the redactor found inside the email body.")]
public sealed record IdentifiedEntity(
    [property: Description(
        "The category of the entity. Must be exactly one of: " +
        "PERSON (a person's last name only, never a first name), " +
        "COMPANY (a company or organisation name), " +
        "PROJECT (a project or product name), " +
        "AMOUNT (a specific dollar amount).")]
    string Kind,

    [property: Description(
        "The exact text of the entity as it appears in the body, copied " +
        "character-for-character. The deterministic substituter will use " +
        "this to find and replace occurrences in the original body.")]
    string Value);

/// <summary>
/// Output of the on-device body-redactor agent. The agent only identifies
/// entities — it does NOT rewrite the body or generate token placeholders.
/// A downstream adapter does the deterministic substitution.
/// </summary>
[Description(
    "List of sensitive entities the redactor found in the email body so a " +
    "deterministic substituter can replace them with placeholder tokens.")]
public sealed record RedactedBody(
    [property: Description(
        "Every sensitive entity found in the body, in the order they appear " +
        "(roughly). Empty if nothing needed substituting.")]
    List<IdentifiedEntity> Entities);
