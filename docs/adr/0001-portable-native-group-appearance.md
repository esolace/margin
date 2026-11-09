# Store GH1 appearance on the native group

GH1 appearance now travels in the native group's serialised object values, rather than depending solely on the definition's lookup by instance identity. Native copying changes identity; object-owned values survive that operation, save/reopen and native undo without replacing GH_Group or guessing which source group a copy came from.

The documented string GetValue/SetValue methods are protected. A small GH1 adapter binds them once as typed delegates, keeping this host-specific dependency out of the shared model and GH2 adapter. Native Rhino 9 copy and round-trip checks verify the integration; unsupported accessor signatures fail explicitly. A custom group subclass was rejected because it would replace the native object type and complicate files opened without Margin.

When attaching an older definition, promote legacy appearance onto each group before its first copy. Object-owned values take precedence. Writes also update the old definition entry for older Margin versions; this is read compatibility, not bidirectional synchronisation with edits made after downgrading. Revisit this adapter if Grasshopper exposes a public object-metadata API.

Existing native groups without Margin metadata also receive a snapshot of their current appearance when attached. This prevents destination defaults from changing their first copy. New groups receive defaults when they arrive uncoloured or with GH1's standard group colour; the native Group command preassigns that colour. Incoming metadata-free groups from a source never encountered by Margin cannot be distinguished from new groups when they use that standard colour. Open the source with Margin before copying when its original appearance must be retained.
