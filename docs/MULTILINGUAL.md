# Multilingual Margin

Status: scope confirmed; implementation pending. This is not a claim that the
current binaries are translated.

## Confirmed requirement

Support every language offered by Rhino across Margin's Rhino 7, 8 and 9
adapters and Grasshopper 1/2 where the host supports them. The user explicitly
selected the complete Rhino language set on 2026-11-16.

| Language | Catalog |
| --- | --- |
| English | en |
| Czech | cs |
| German | de |
| Spanish | es |
| French | fr |
| Italian | it |
| Japanese | ja |
| Korean | ko |
| Polish | pl |
| Portuguese | pt |
| Russian | ru |
| Simplified Chinese | zh-Hans |
| Traditional Chinese | zh-Hant |

Source: [McNeel's deployment language options](https://www.rhino3d.com/en/docs/guides/deployment/).
This is the desktop product's language set, not the larger website region list.

## Intended experience

- Follow Rhino's interface language by default. Studio offers a persistent
  manual language override, with language names written in their own language.
- Translate Studio, Margin-owned menus, the native group editor, tooltips,
  accessibility labels, empty states, validation and action feedback.
- Preserve user-authored group names, preset names, font family names, saved
  component names and technical identifiers. Translate the generated unnamed
  group label only at display time; do not write translated defaults into files.
- Switching language must preserve unapplied group edits, the selected group,
  preset selection, preview state and saved styles. It must not require a Rhino
  restart or change Rhino's own language setting.
- Bundle catalogs locally. Normal use must require no network, translation
  service, account or API key. Missing translations fall back to English.

## Implementation boundaries

Use a shared catalog and locale-resolution policy in Margin.Core, compatible
with net48, net8 and net10. Studio and native adapters must consume the same
catalog rather than maintain separate translations. Persist the language choice
as an application preference; do not include it in exported themes or group
appearance metadata. Existing preference files migrate to automatic selection.

Read Rhino's actual locale using
`Rhino.ApplicationSettings.AppearanceSettings.LanguageIdentifier`.
Do not assume the operating system language matches Rhino. Resolve regional
variants deliberately, including Brazilian Portuguese and both Chinese scripts.

Use stable message keys and whole-sentence templates with named placeholders.
Do not translate by searching and replacing rendered DOM text or serialized
enum values. Escape inserted values; translations must not become HTML or code.
Retain diagnostic details where useful beneath translated error explanations.

The GH2 adapter currently finds its host menu by the English word `Display`
(`src/Margin.Grasshopper2/Plugin.cs`). Replace that dependency with an inspected
stable host identifier or a verified host-language-aware lookup. Inspect the
GH2 group-command name matching as well; do not assume its verbs/nouns are
localized or invariant without checking. GH1 already uses the menu identifier
`mnuDisplay`.

Keep the existing typography and visual hierarchy. Allow translated labels to
wrap or grow without squeezing colour controls, clipping actions or hiding the
language selector. Check CJK glyph coverage and longer European-language text.

## Verification and release conditions

1. Catalog checks cover all keys in all 13 languages, placeholder consistency,
   valid locale aliases, English fallback and safe inserted values.
2. Preference tests cover old files, saving/reopening, invalid language choices,
   and preservation of independent labels, defaults and user content.
3. Browser tests exercise each language at the supported minimum window size,
   both Studio sections, dialogs and keyboard navigation. Switching language
   during unsaved edits must preserve the draft and selection.
4. Build all adapters, run the shared tests on net48/net8/net10 and retain
   existing appearance, casing, preset and group-workflow regressions.
5. Native checks verify menu discovery, automatic Rhino-language selection,
   manual override, group editing, Studio reopen and restart persistence.
   Record each tested host/language combination honestly.
6. Translation wording needs review in context; automated completeness is not
   proof of linguistic quality. Document any unreviewed translations.

No live installation or publication is part of this scope confirmation. Native
testing must respect the user's working session. Rhino 7 Mac currently cannot
launch on this machine because Intel execution is unavailable; Windows Rhino 7
is still excluded from the candidate. Localization does not remove either
compatibility limitation. GH2 remains a Rhino 9 target.

## Suggested implementation sequence

1. Shared catalog, locale selection, preference migration and tests.
2. Studio and native string extraction; fix host-menu/command dependencies.
3. Complete all catalogs and adapt layouts, then exercise language switching.
4. Build and native verification, translation review, release evidence.
