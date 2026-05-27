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
public sealed record PickedEmail(
    string FromFullName,
    string FromEmail,
    string ToFullName,
    string ToEmail,
    string Subject,
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
/// One mapping entry produced by the body-redactor agent. Using a list of
/// pairs (rather than a <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>)
/// makes the JSON schema simpler for smaller on-device models — some of them
/// struggle with dictionary-shaped output but handle ordered arrays fine.
/// </summary>
public sealed record RedactionPair(string Token, string Original);

/// <summary>
/// Output of the on-device body-redactor agent. The <see cref="Body"/> is
/// what gets sent to the cloud, and <see cref="Mapping"/> stores any
/// COMPANY_n / PERSON_n / etc. tokens the redactor introduced so the final
/// on-device assembly stage can rehydrate them.
/// </summary>
public sealed record RedactedBody(
    string Body,
    List<RedactionPair> Mapping);
