# Decision Log

This document is used to track key decisions that are made during the course of the project.
This can be used to understand why decisions were made and by whom.

| Decision | Date | Alternatives Considered | Reasoning | Detailed doc | Made By | Work Required |
| -- | -- | -- | -- | -- | -- | -- |
| Use SQLite in-memory to configure Entity Framework for unit testing. | 2025-01-09 | Repository Pattern | SQLite in-memory is the best choice, as alternatives cannot reasonably be implemented at this time | [Entity Framework Unit Testing Strategy](./adr/0001-ef-testing-strategy.md) | Delta-V Crew & Henning Kilset | [User Story](https://dev.azure.com/ipsenergy/industry-permitting/_workitems/edit/132) |
