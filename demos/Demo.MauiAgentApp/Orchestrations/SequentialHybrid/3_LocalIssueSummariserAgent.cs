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

                    Keep in the brief, when present:
                      • The customer's name (from the From: line or the body)
                      • Any order / invoice / account / case ID

                    NEVER include in the brief — these are personal and
                    must be stripped:
                      • Passwords — even if the customer wrote one in plain
                        text. NEVER quote, paraphrase, hint at, or mention
                        the specific password string in any form, even
                        inside quotes. If they wrote a password, just say
                        their reset failed — do not name the password.
                      • Postal / mailing addresses
                      • Phone numbers
                      • Credit-card, bank-account, SSN or other ID numbers

                    Two worked examples:

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

                    Output ONLY the brief. No subject, no greeting, no
                    signature, no headings, no commentary. Just 2-3 plain
                    sentences. Never quote or paraphrase a password.
                    """,
            },
        };

        builder.AddAIAgent(name, (sp, key) => new ChatClientAgent(
            sp.GetRequiredKeyedService<IChatClient>(AIModels.Local),
            options).WithTelemetry());

        return builder;
    }
}
