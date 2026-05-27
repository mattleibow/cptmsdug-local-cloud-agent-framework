using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Demo.MauiAgentApp.Orchestrations.SequentialHybrid;

/// <summary>
/// Stage 3 of the meeting-invite pipeline. Runs on-device.
///
/// Reads the picked customer email (forwarded by stage 2 as a small
/// From/To/Subject envelope + body) and produces a SHORT prose brief
/// — 2-3 sentences — capturing what the cloud needs to draft a useful
/// meeting invite:
///
///   • Who the customer is (full name, so the invite can address them)
///   • What identifier the issue is attached to (order ID, account ID, etc.)
///   • What the issue actually is and what they're asking for
///
/// The agent is explicitly told to OMIT sensitive PII that the cloud has
/// no business seeing: physical addresses, phone numbers, passwords,
/// credit-card / bank / SSN numbers. We keep the brief plain text — no
/// JSON, no list of tokens — so there's nothing for the constrained
/// decoder to mishandle.
/// </summary>
public static class LocalIssueSummariserAgentExtensions
{
    public static IHostApplicationBuilder AddLocalIssueSummariserAgent(
        this IHostApplicationBuilder builder, string name)
    {
        var options = new ChatClientAgentOptions
        {
            Name = name,
            Description =
                """
                On-device issue summariser: turns the picked email body into
                a 2-3 sentence brief. Customer name stays; addresses, phone
                numbers, passwords, card/SSN numbers are stripped.
                """,
            ChatOptions = new ChatOptions
            {
                MaxOutputTokens = 200,
                Instructions = """
                    The user message you receive is a customer's support
                    email, formatted as a small envelope:

                      From:    "..." <...>
                      To:      "..." <...>
                      Subject: ...

                      <body>

                    Your only job is to write a SHORT brief (2-3 sentences,
                    plain prose) describing what's going on, so a colleague
                    can write a meeting invite reply about it.

                    Keep in the brief — identifiers that refer to the
                    customer's ISSUE:
                      • The customer's name
                      • One transaction/support identifier they reference:
                        order ID, invoice number, receipt number, case
                        ID, ticket ID, account ID. These are the only
                        kinds of "ID" you may keep.

                    NEVER include in the brief — identifiers that refer to
                    the customer's PERSON. Drop them entirely; do not
                    quote, paraphrase, summarise, or "verify" them.
                      • Passwords — even if the customer wrote one in plain
                        text. NEVER quote, paraphrase, hint at, or mention
                        the specific password string in any form, even
                        inside quotes. If they wrote a password, just say
                        their reset failed — do not name the password.
                      • Postal / mailing addresses
                      • Phone numbers (any format, any country)
                      • Card numbers (credit, debit, bank account, IBAN,
                        even just the last four digits)
                      • Government identity numbers of any kind, in any
                        country: SSN, ITIN, NI / NIN, national ID, My
                        Number, Japan ID, tax ID, passport, driver's
                        licence, residence permit, Aadhaar, etc.
                      • Dates of birth, places of birth
                      • IP addresses, device IDs, location coordinates,
                        precise device names
                      • API keys, access tokens, session cookies

                    If you are unsure whether a number is an order ID
                    (keep) or a personal identifier (strip), STRIP it —
                    the colleague drafting the reply does not need it.
                    Three worked examples follow.

                      INPUT (example 1 — login issue):
                        From:    "Sam Lee" <sam.lee@example.com>
                        To:      "Alex Park" <alex.park@aurora-labs.com>
                        Subject: Cannot log in

                        Hi, I can't log in to account #A12345. I typed
                        Sunshine2024! like usual. My address is 1 Maple St,
                        Boston MA, phone 555-1212. Please reset so I can
                        see Order #99. Thanks - Sam Lee

                      GOOD BRIEF:
                        Sam Lee can't log in to account #A12345 — their
                        password is being rejected. They need a reset so
                        they can view Order #99.

                      BAD BRIEF (do NOT do this — the password is quoted):
                        Sam Lee can't log in to account #A12345. They
                        tried the password "Sunshine2024!" but it
                        failed. They need a reset to see Order #99.

                      INPUT (example 2 — refund):
                        From:    "Mira Sato" <mira@example.com>
                        To:      "Alex Park" <alex.park@aurora-labs.com>
                        Subject: Double charge on order #555

                        Hi support, I was double-charged on order #555
                        from card ending 4242. Address is 22 Oak Rd,
                        Portland. Please refund. - Mira Sato

                      GOOD BRIEF:
                        Mira Sato is reporting a duplicate charge on
                        order #555 and is asking for a refund.

                      INPUT (example 3 — 2FA reset with verification details):
                        From:    "Hiroshi T" <h@example.com>
                        To:      "Alex Park" <alex.park@aurora-labs.com>
                        Subject: 2FA reset urgent

                        Hi, I lost my phone, can't get into account
                        #ABC-42. My national ID is 999-45-3982 and my
                        number is +81 90 1234 5678 if you need to verify.
                        Please reset 2FA ASAP — payroll today. — Hiroshi

                      GOOD BRIEF:
                        Hiroshi needs an urgent 2FA reset for account
                        #ABC-42 after losing his phone. He has payroll
                        work that depends on regaining access today.

                      BAD BRIEF (do NOT — the national ID and phone are
                        quoted; "to verify" is not a reason to keep them):
                        Hiroshi needs a 2FA reset for account #ABC-42.
                        His national ID is 999-45-3982, phone
                        +81 90 1234 5678 for verification.

                    Output ONLY the brief. No subject, no greeting, no
                    signature, no headings, no commentary. Just 2-3 plain
                    sentences. Never quote a password, a phone number, an
                    address, a card number, a government ID, an IP
                    address, a date of birth, or an API key — not even
                    "to help the colleague verify the customer".
                    """,
            },
        };

        builder.AddAIAgent(name, (sp, key) => new ChatClientAgent(
            sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
            options).WithTelemetry());

        return builder;
    }
}
