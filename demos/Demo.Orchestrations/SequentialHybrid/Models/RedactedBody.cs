using System.ComponentModel;

namespace Demo.Orchestrations.SequentialHybrid.Models;

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
