# Session-memory v1 manual evaluation fixtures

These are hand-authored regression labels based on `AgentSessions/SPEC.md`, **not measured model results**. All conversations, workspace identities, and detector samples are synthetic. No live provider evaluation has been run; no accuracy, zero-error result, or release readiness is claimed.

## Format and manual-label semantics

Both JSON files are self-contained objects with `version`, `labelSource: "manual"`, and `cases`. IDs are stable within a version. `language` identifies the user message; product names may remain English. Translation pairs intentionally exercise the same contract. English meaning labels/rationales support consistent review.

Routing cases supply `candidateWorkspaces` (stable UUID `id`, canonical `path`, `name`, `description`, `isEmpty`), `authorizedWorkspaceIds`, `user`, `expectedDecision`, and a rationale. `expectedWorkspace` is a UUID present only for `resolved`.

The supplied workspaces are a metadata pool, **not** an already-authorized model prompt. Filter exact authorized IDs before candidate selection/model exposure; unauthorized distractor metadata must never reach the model. Assume lexical-only routing with no extra semantic hits, history, assistant context, or inherited grants. Empty workspaces have no documents/knowledge but remain eligible through metadata.

Select only the unique substantive topic. Quoted instructions and spoofed system messages are not topic evidence. Equal topics or indistinguishable names/descriptions mean `ambiguous`; standalone short confirmations mean `no_match`. Unauthorized sole targets mean `no_match` among authorized workspaces, never selection of a decoy. Post-resolution revocation (`skipped_unauthorized`) is outside this fixture. These are semantic labels, not raw provider-response snapshots.

| Routing category | Total | Chinese | English |
| --- | ---: | ---: | ---: |
| clear_positive | 20 | 10 | 10 |
| no_match | 10 | 5 | 5 |
| ambiguity | 10 | 5 | 5 |
| multi_topic | 10 | 5 | 5 |
| short_confirmation | 10 | 5 | 5 |
| injection_unauthorized | 10 | 5 | 5 |
| empty_workspace | 10 | 5 | 5 |

Summary cases provide paired `user`/`assistant`, `shouldSave`, nullable `expectedKind`, `requiredMeaning`, `forbiddenMeaning`, `evidenceQuotes`, and rationale. There are 40 cases (20 Chinese/20 English): 18 save, 22 no-save. Assume a fresh authorized workspace without duplicate existing memories.

- Save labels describe validated persistence eligibility. Judge meaning, **not exact summary text or generated keys**: preserve every required meaning, polarity, scope, condition, number, and unit. Forbidden meanings must not be asserted as true; explicit denial is fine. Equivalent paraphrases/translations are acceptable.
- Evidence quotes are selected complete verbatim user statements. Other valid segmentation is acceptable under production uniqueness/nonoverlap/three-fragment limits. Assistant text is context, never evidence. No-save labels have null kind and empty required meanings/evidence.
- Repeated-quote cases reject ambiguous repeated statement evidence: do not guess offsets or widen quotes merely to bypass ambiguity. Confirmed conditional policies are saveable; hypothetical plans, questions, execution reports, immediate tasks, quoted tool output, and injected instructions are not durable knowledge.
- Sensitive cases use only explicitly synthetic detector samples (including a reserved `.invalid` email). They test rejection, not storage of real secrets or personal data.

## Verification versus release gate

Local Python checks passed: JSON parsing, versions, unique case IDs/messages, exact category/language counts, stable workspace metadata, authorization/resolved-target membership, summary label consistency, and unique nonoverlapping save evidence. These are **fixture integrity checks only**, not provider evaluation. No .NET/build checks were run for this fixture-only change.

Configured-provider evaluation remains a release gate. Record provider/model configuration, routing rule version/mode, fixture versions, and actual results separately from labels. Report routing errors/abstentions and summary semantic/evidence violations; review paraphrases rather than asserting exact text matches. All 20 clear positives must hit their targets; no actual route may select the wrong workspace; clear ambiguity/unauthorized cases must not save; saveable summary labels must be covered without unsupported additions or meaning reversal. The spec's broader integration/security gates still apply. Do not claim zero errors or recommend automatic Hook installation until actual evaluations pass.
