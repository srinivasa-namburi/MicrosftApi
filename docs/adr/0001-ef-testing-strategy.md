# 1. Entity Framework Unit Testing Strategy

Date: 2025-01-09

## Status

Accepted

## Context

To unit test code that interacts with Entity Framework (EF), we need to be able to use some sort of test double for the real database or otherwise abstract away the EF layer. We considered the following options:
- Repository Pattern
- SQLite as a database fake

The repository pattern would introduce another layer between the application code and EF. This is a good solution that would allow for easy mocking of the data access layer, however the codebase relies heavily on Entity Framework
and implementing a repository pattern would require a significant refactor of the codebase, so this is not a feasible option at this time.

SQLite provides an in-memory database that can be used as a database fake. During test setup, we would create a new SQLite database seeded with test data and use it to configure a DbContext needed to test application code. This would
require few to no changes to the existing application code. However, there is no guarantee that the SQLite queries will behave like the real database. For example, SQL Server does case-insensitive string comparison by default, whereas SQLite is case-sensitive.

## Decision

Given the options considered, we have decided to use SQLite in-memory to configure Entity Framework for unit testing. This will allow us to test the application code with minimal changes to the existing codebase.

## Consequences

Since SQLite query behavior is not guaranteed to be the same as SQL Server, we will need to be careful when writing tests to ensure that they are testing the correct behavior and attempt to configure SQLite in a way that matches SQL Server as much as possible.
We should also ensure that the test data is representative of the real data in the database.