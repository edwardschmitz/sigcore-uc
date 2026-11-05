# SigCore UC AI Knowledge Base

**Status: INTERNAL DEVELOPMENT DRAFT. Two source-grounded chapters are drafted; neither has passed engineering review or blind AI validation. Not ready for customer distribution.**

## Purpose

This is a collection of technical source documents intended to be ingested by a customer's chosen AI model. It prioritizes complete, precise, version-specific product knowledge over prose optimized for direct human reading. Customers should use the existing human-oriented SigCore UC documentation when seeking a manual or tutorial.

The customer supplies the AI service. SigCore supplies files, not a hosted chatbot or AI inference service.

## Navigation

- [INDEX.md](INDEX.md) is the master table of contents with direct links to every subject file.
- [VERSION.md](VERSION.md) tracks the knowledge base's release status and applicability.
- `Architecture/`, `Hardware/`, `Core-Software/`, `User-Interfaces/`, `Programming/`, `Operations/`, `Applied-Engineering/`, and `Reference/` contain modular knowledge documents.
- `Images/` contains the planned shared image library, with an [image inventory](Images/IMAGE-INVENTORY.md).
- `Project/` contains **development-only** project management and verification records. Do not include `Project/` in a customer release.

## Authoring rules

1. **Never invent a specification.** Derive technical claims from revision-specific schematics, source code, tests or verified records.
2. **Separate confirmed product behavior from engineering guidance and hypothetical designs.** Label planned features explicitly.
3. **Treat image data as supporting evidence.** Record electrical connections, labels, values and important relationships in text even when there is a diagram.
4. **Use one source of truth per specification.** Cross-reference related files instead of maintaining contradictory definitions.
5. **Record applicability.** Specify the hardware revision and software release where a behavior applies.
6. **Use relative image paths.** Images are kept in `Images/` and referenced from documents via `../Images/<group>/<name>`; the same approved files can later be published at stable image URLs. No public URLs have been assigned or verified yet.
7. **Keep the project modular.** Do not flatten separate Markdown sources during editing.

## Readiness rule for an AI

Most subject files still carry the `placeholder` marker. `Architecture/System-Architecture.md` and `Hardware/Analog-Inputs.md` now have `source-draft-review-required` status: they contain evidence-linked technical information and explicitly marked conflicts, but are **not approved for customer use**. A model must not infer product capabilities from placeholders or silently resolve contradictions in drafts.

## Release intent

The planned customer artifact is a ZIP of the completed Markdown collection and approved images, plus `README.md`, `INDEX.md` and `VERSION.md`. AI service compatibility with ZIP ingestion and images needs testing; a generated single-file alternative can be added if required. **This scaffold ZIP is not the customer release.**
