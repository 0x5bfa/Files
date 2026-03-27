---
name: files-codebase-map
description: Use when you need to understand the Files repository before making changes, including which project or directory owns a feature, where new code should usually live, how the codebase is currently structured, and what the preferred long-term architectural direction is for areas such as AOT, trimming, UI composition, native interop, controls, and storage.
---

# Files Codebase Map

Use this skill when the task requires understanding the repository layout before making changes.

This is a shared orientation skill, not a feature workflow.

## When to read references

Read `../../../docs/ai/codebase-overview.md` when you need:

- a project-by-project map of the repository
- a tree-style summary of major directories
- guidance on where feature work usually belongs
- a quick list of high-risk or cross-cutting areas

Read `../../../docs/ai/architecture-direction.md` when you need:

- the intended long-term direction of the codebase rather than just the current layout
- migration guidance for AOT, trimming, UI composition, interop, controls, or storage
- clarification on which existing patterns are legacy versus preferred for new work

## How to use it

1. Start with the top-level layout and identify the project that owns the change
2. Read only the relevant project section instead of loading the whole repository into context
3. If the task is about future direction, migration, or preferred patterns, read `../../../docs/ai/architecture-direction.md`
4. If the task spans commands, localization, packaging, controls, or tests, check the cross-cutting notes

## Scope

This skill explains repository structure. It does not replace implementation-specific skills such as feature workflow, commands, localization, or packaging guidance.
