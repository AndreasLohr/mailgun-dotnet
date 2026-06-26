# World-Class .NET SDK Task List

> Status legend: `[x]` done & tested · `[ ]` not done. Items deferred by an explicit, documented
> engineering decision are marked `[ ]` with a **(deferred: …)** note so the call is auditable.

## Critical fixes before calling it world-class

### DI options parity

- [x] Fix `MailgunServiceCollectionExtensions` so the DI registration copies `AllowInsecureBaseUrl` from configured `MailgunClientOptions`.
- [x] Fix `MailgunServiceCollectionExtensions` so the DI registration copies `MaxResponseContentBytes` from configured `MailgunClientOptions`.
- [x] Add regression tests proving direct `MailgunClient` construction and DI-based construction honor the same option values.
- [x] Add a test proving `AllowInsecureBaseUrl = true` behaves consistently in the DI path.
- [x] Add a test proving custom `MaxResponseContentBytes` behaves consistently in the DI path.

> Root-cause fix: replaced the hand-maintained field-by-field projection (which had silently dropped
> `OnResponse`, then `AllowInsecureBaseUrl` + `MaxResponseContentBytes`) with
> `MailgunClientOptions.CloneWithHttpClient` (`MemberwiseClone`). `OptionsParityTests` reflects over every
> settable property as a structural guard so no future option can be forgotten.

### Webhook `parent-signature` support

- [x] Add `ParentSignature` to the webhook signature model, mapped to JSON property `parent-signature`.
- [x] Update `MailgunWebhookParser.TryExtractSignature` to read `parent-signature` when present.
- [x] Update ASP.NET webhook endpoint helpers to support verification against `parent-signature`.
- [x] Add an explicit webhook signature policy option, such as `ChildSignatureOnly`, `ParentSignatureOnly`, or `AcceptEither`.
- [x] Choose and document the default signature verification policy. (Default: `AcceptEither` — backward-compatible and subaccount-aware.)
- [x] Add unit tests for payloads containing only `signature`.
- [x] Add unit tests for payloads containing both `signature` and `parent-signature`.
- [x] Add tests for subaccount webhook verification behavior.
- [x] Add documentation explaining Mailgun subaccount webhook verification and when to use parent vs child signatures.

### HttpClient lifetime and factory behavior

- [x] Review the current singleton SDK client pattern that captures a factory-created `HttpClient`.
- [x] Decide the intended lifetime model for the high-level `MailgunClient`. (Process-lifetime singleton; documented.)
- [x] Either convert the SDK registration to a standard typed-client pattern with appropriate lifetime semantics, or document why the current singleton behavior is safe.
- [x] If keeping a long-lived client, configure a long-lived `SocketsHttpHandler` with `PooledConnectionLifetime`.
- [x] If using `IHttpClientFactory`, ensure handler pooling, DNS refresh, and socket exhaustion behavior are aligned with .NET guidance.
- [x] Add tests or integration checks verifying DI registration does not create stale or unintended `HttpClient` behavior.
- [x] Document recommended production registration patterns for ASP.NET Core.

## Modern .NET readiness

### Trimming and Native AOT

- [x] Inventory all JSON-serialized request, response, and webhook DTOs. (Identified the AOT-blocking surface: `Dictionary<string, object>` blobs — webhook `user-variables`, alert `settings`, etc.)
- [ ] Add a source-generated `System.Text.Json` context for public request DTOs. **(deferred: Native AOT explicitly not a supported goal — see below.)**
- [ ] Add a source-generated `System.Text.Json` context for public response DTOs. **(deferred: same.)**
- [ ] Add a source-generated `System.Text.Json` context for webhook DTOs. **(deferred: same.)**
- [ ] Route SDK serialization and deserialization through generated JSON metadata where possible. **(deferred: same.)**
- [ ] Add a trimming smoke-test project. **(deferred: `TrimMode=full` unsupported by design; partial trimming documented instead.)**
- [ ] Add a Native AOT smoke-test project if full AOT support is a goal. **(deferred: AOT is not a goal — the public surface intentionally exposes `object`-typed JSON.)**
- [ ] Remove or narrow `RequiresUnreferencedCode` only after trimming behavior is verified. **(intentionally retained — condition not met.)**
- [ ] Remove or narrow `RequiresDynamicCode` only after Native AOT behavior is verified. **(intentionally retained — condition not met.)**
- [x] Document the exact trimming and Native AOT support level.

### Target framework policy

- [x] Decide whether the SDK intentionally supports only modern .NET runtimes. (Yes: `net8.0` LTS + `net10.0`.)
- [x] If broad adoption is desired, evaluate adding `netstandard2.0` alongside modern targets. (Evaluated and rejected — modern BCL primitives have no clean polyfill; rationale documented.)
- [x] If staying modern-only, document the rationale for targeting only supported modern .NET versions.
- [x] Add CI coverage for every supported target framework. (Pre-existing `ci.yml`: net8 + net10 on Ubuntu + Windows, plus a .NET 8 SDK-only analyzer-gap job.)
- [x] Add a support matrix to the README.

## Test quality and verification

### Build and test verification

- [x] Run a clean local `dotnet restore`.
- [x] Run a clean local `dotnet build`.
- [x] Run the full test suite locally. (638 tests green on net8.0 + net10.0.)
- [ ] Run the full test suite in CI on Linux. **(workflow present + green locally on both TFMs; live GitHub-hosted run executes on push.)**
- [ ] Run the full test suite in CI on Windows. **(same.)**
- [x] Confirm all analyzer warnings remain treated as errors. (Clean build, 0 warnings.)
- [x] Confirm nullable warnings remain clean.

### Mutation testing

- [x] Re-run mutation testing with the current codebase.
- [x] Update README mutation-test numbers to match current results.
- [x] Reconcile the documented test count with the actual current test count. (Was 445 → now 638.)
- [x] Review `stryker-config.json` exclusions.
- [x] Decide whether excluding all `Models/**/*.cs` is still appropriate. (Yes — pure DTOs; serialization now covered by contract tests below.)
- [x] Add contract-focused tests for model serialization rather than relying only on mutation coverage.

### Serialization and API contract tests

- [x] Add golden JSON tests for key message-send request payloads. (Form/multipart message bodies covered by `MessagesServiceTests` + `MessageMultipartOptionsTests`; JSON-bodied request goldens in `SerializationContractTests`.)
- [x] Add golden JSON tests for domain-management request and response models.
- [x] Add golden JSON tests for suppression-list models.
- [x] Add golden JSON tests for analytics, metrics, and logs models.
- [x] Add golden JSON tests for webhook payloads.
- [x] Add tests proving optional fields are omitted or included as intended.
- [x] Add tests proving enum/string serialization matches Mailgun API expectations.
- [x] Add tests proving multipart and MIME message behavior remains stable.

### Security regression tests

- [x] Add regression tests for unsafe non-HTTPS base URL rejection.
- [x] Add regression tests for the explicit insecure-base-url opt-in path.
- [x] Add regression tests for pagination host validation.
- [x] Add regression tests proving credentials are not sent to server-supplied cross-host pagination URLs.
- [x] Add tests for max response-size enforcement.
- [x] Add tests for webhook timestamp freshness enforcement.
- [x] Add tests for replay-token cache behavior.
- [x] Add tests for fixed-time webhook signature comparison behavior where practical.

## Documentation cleanup

- [x] Update README quality metrics so test counts and mutation scores are current.
- [x] Clarify that telemetry uses redacted or route-template-style URLs rather than raw high-cardinality URLs.
- [x] Document retry behavior and how to tune it.
- [x] Document timeout behavior.
- [x] Document max response-size behavior.
- [x] Document webhook security behavior, including timestamp tolerance and replay protection.
- [x] Add a production ASP.NET Core DI example.
- [x] Add a production webhook endpoint example.
- [x] Add a subaccount webhook example using `parent-signature`.
- [x] Add a troubleshooting section for authentication, region selection, retries, rate limits, and webhook verification.

## Release-readiness gates

- [x] No known behavioral differences between direct construction and DI construction.
- [x] Current Mailgun webhook signature behavior is supported and documented.
- [x] HttpClient lifetime behavior is intentional, documented, and aligned with production .NET usage.
- [x] Serialization contracts are protected by tests.
- [x] Security-sensitive HTTP and webhook behavior is protected by regression tests.
- [x] README examples are current and executable.
- [ ] CI passes on all supported operating systems and target frameworks. **(green locally on net8+net10; the Linux+Windows matrix runs on push.)**
- [x] Trimming and Native AOT support level is explicitly documented.
