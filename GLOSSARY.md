# Margin

Margin gives Grasshopper component labels and groups a considered visual appearance.

## Language

**Group appearance**:
The visual treatment of a Grasshopper group, including its fill, frame and title styling.

**Native outline**:
The boundary Grasshopper uses to enclose group members. Grasshopper 1 offers None, Box, Blob and Rectangles. The tested Grasshopper 2 build uses a rectangular frame, with a draggable border and an interior available for window selection.

**Title casing**:
The way a group's name is displayed using uppercase, lowercase or other capitalisation, without changing the name itself.

**Copied group**:
A duplicate of an existing group that retains its appearance and can be edited independently of the original.

**Ordinary group**:
A native group with no saved Margin appearance, either on the group or in older definition metadata. On first attachment of an opened definition, it adopts explicitly saved local group defaults; without that saved choice it retains its native appearance.

**Saved group defaults**:
The complete fill, opacity, design and title appearance explicitly saved on this computer. They apply to ordinary groups in newly attached definitions and eligible new groups, without changing groups already initialized by Margin.

**Group appearance session**:
The shared module that loads saved defaults before native attachment, protects existing appearance, and remembers initialized groups through repeated attachment, copying and undo. Native adapters own host events and storage.

**Unnamed group**:
A group without a user-supplied name. Studio identifies it as “Group”, and the canvas displays “Group” when its title is visible.
_Avoid_: Untitled group


**Title visibility**:
Whether the canvas displays a group’s title. A hidden title preserves the group’s name and appearance.
