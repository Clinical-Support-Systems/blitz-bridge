# Squad Decisions

## Active Decisions

### Decision 001: v1 PRD Bootstrap (2026-04-23)

**Author:** Verbal  
**Status:** Active  

Five core MCP tools chosen for MVP: azure_sql_target_capabilities, azure_sql_blitz_cache, azure_sql_blitz_index, azure_sql_health_check, azure_sql_current_incident. Read-only enforcement enforced at connection validation. Procedures and databases controlled via allowlists per profile. AiMode (0/1/2) configuration lever for AI participation cost control. 5-week milestone plan with parallelizable work: Week 1–2 core MCP + sp_BlitzCache, Week 2–3 remaining FRK tools + auth, Week 3–4 observability + health, Week 4 Aspire, Week 5 testing + release. Open questions assigned to owners. Full spec in `docs/PRD.md`.

**Related:** `docs/PRD.md`, `.squad/decisions/inbox/verbal-prd-bootstrap.md` (merged)

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
