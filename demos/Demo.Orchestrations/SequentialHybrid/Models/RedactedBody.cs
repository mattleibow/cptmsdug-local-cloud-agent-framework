using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Demo.Orchestrations.SequentialHybrid.Models;

/// <summary>
/// Output of the on-device body-redactor agent: four bounded lists of strings
/// copied verbatim from the picked email body. The discriminated-union shape
/// (one list with a Kind field per item) was replaced by these typed lists
/// because small on-device models (Apple Intelligence) would ride the
/// autoregressive pattern <c>{ "Kind": "AMOUNT", "Value": "$5,000" }</c> and
/// emit a runaway invented series. Each list bounded by <c>[MaxLength]</c>
/// turns into a JSON-schema <c>maxItems</c> the constrained decoder enforces.
///
/// The downstream <c>CloudPromptAdapter</c> walks all four lists, drops any
/// value that doesn't literally appear in the body, assigns tokens
/// (PERSON_1, COMPANY_1, …) and performs the substitution itself.
/// </summary>
[Description(
    """
    Sensitive strings spotted in the email body, grouped by kind. Each list
    contains only literal substrings of the body. Leave a list empty if the
    body has none of that kind.
    """)]
public sealed record RedactedBody(
    [property: Description("Person LAST names that appear in the body, copied character-for-character. Never include first names. Do not invent or guess — only copy what is literally written.")]
    [property: MaxLength(5)]
    List<string> PersonLastNames,

    [property: Description("Company or organisation names that appear in the body, copied character-for-character. Do not invent or guess — only copy what is literally written.")]
    [property: MaxLength(5)]
    List<string> Companies,

    [property: Description("Project or product names that appear in the body, copied character-for-character. Do not invent or guess — only copy what is literally written.")]
    [property: MaxLength(5)]
    List<string> Projects,

    [property: Description("Dollar amounts that appear in the body, copied character-for-character — every character from the '$' to the end of the number (e.g. the digits and any thousand-separator commas). Must match a pattern like $N or $N,NNN. Do not construct, round, infer, or extrapolate amounts. Never include a partial amount like '$' or '$3' on its own. Only copy amounts you can see.")]
    [property: MaxLength(5)]
    List<string> Amounts);
