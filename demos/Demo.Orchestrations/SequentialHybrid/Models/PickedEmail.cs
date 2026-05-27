using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Demo.Orchestrations.SequentialHybrid.Models;

/// <summary>
/// Structured payload produced by the on-device inbox-picker agent. Flat
/// fields and role-named field labels (<c>Sender</c>/<c>Recipient</c> rather
/// than <c>From</c>/<c>To</c>) so smaller on-device models can't conflate the
/// roles with the user's reply direction.
///
/// Stored in workflow state by the cloud-prompt adapter; read back by the
/// final-email assembler at the end of the workflow.
/// </summary>
[Description("The single inbox email the user wants to reply to, copied verbatim from the chosen inbox entry.")]
public sealed record PickedEmail(
    [property: Description("Copy verbatim from SENDER_NAME in the chosen inbox entry.")]
    string SenderName,

    [property: Description("Copy verbatim from SENDER_EMAIL in the chosen inbox entry.")]
    string SenderEmail,

    [property: Description("Copy verbatim from RECIPIENT_NAME in the chosen inbox entry.")]
    string RecipientName,

    [property: Description("Copy verbatim from RECIPIENT_EMAIL in the chosen inbox entry.")]
    string RecipientEmail,

    [property: Description("Copy verbatim from SUBJECT in the chosen inbox entry.")]
    string Subject,

    [property: Description("Copy the body of the chosen inbox entry character-for-character. Preserve every paragraph, date, dollar amount, and named person.")]
    string Body)
{
    /// <summary>First-name view of the sender, used for greetings + cloud prompts.</summary>
    [JsonIgnore]
    public string SenderFirstName => FirstNameFrom(SenderName, SenderEmail);

    /// <summary>First-name view of the recipient (the user), used for cloud prompts.</summary>
    [JsonIgnore]
    public string RecipientFirstName => FirstNameFrom(RecipientName, RecipientEmail);

    private static string FirstNameFrom(string? fullName, string? email) =>
        fullName?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        ?? email?.Split('@').FirstOrDefault()
        ?? "<unknown>";
}
