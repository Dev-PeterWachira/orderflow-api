# OrderFlow API

> A production-style .NET order management API built with **ASP.NET Core**, **Entity Framework Core**, **Microsoft SQL Server**, raw SQL, stored procedures, authentication, and automated tests.

⚠️ **This repository contains no implementation code — by design.** It contains this README and a set of **GitHub issues** that make up the full project backlog. Implementation is built in working forks by the team members assigned to each ticket.

---

## 1. The project's goal

OrderFlow is the backend for a company that **sells products online**. The goal is to build a dependable order management service that behaves like production software, not a demo:

- Correct, consistent data — even under concurrent load and failures
- Clean API design and a well-structured codebase
- Performance and reliability that hold up as the data grows
- Security and auditability of every important operation
- A real automated test suite that proves the above

## 2. The business scenario

The system must support:

- Customers register and authenticate (email/password, token-based).
- Customers view **their own** orders.
- Employees manage products and stock.
- Customers create orders containing multiple products.
- The system validates stock **before** completing an order.
- Orders are processed safely and atomically.
- Payments are recorded as part of order processing.
- Sales reports are generated **using SQL Server** (including stored procedures).
- Important operations are audited.
- Concurrent purchases are handled correctly — two customers must never buy the same last item.
- Errors and database failures are handled safely, with no partial/corrupt state.
- Automated tests cover the important business behavior.

## 3. Expected technology

| Area | Expectation |
|---|---|
| Language / framework | C# and **ASP.NET Core Web API** |
| Data access | **Entity Framework Core** |
| Database | **Microsoft SQL Server** (use a real SQL Server database for integration tests where database-specific behavior matters) |
| Raw SQL | Used where appropriate (reporting, stored procedures) |
| Stored procedures | SQL Server stored procedures for reporting and order processing |
| Security | Authentication via email/password + tokens; role-based authorization |
| Testing | Automated **unit** and **integration** tests in a project named **`OrderFlow.Api.Tests`** |
| Optional final ticket | **Redis** for session management |
| Optional tooling | Docker may be used if helpful, but is **not** required |

## 4. What is in this repository

- `README.md` — this file. Explains the project and the working process.
- **GitHub issues** — the complete backlog, one ticket per feature area, written with explicit **scope** and **acceptance criteria**.

Everything else — the solution (`OrderFlow.Api`), the test project (`OrderFlow.Api.Tests`), EF Core migrations, SQL scripts, configuration — is built by the team as part of the tickets.

## 5. How to work through the tickets — the process

1. **Fork this repository** to your own GitHub account and clone **your fork**. All implementation happens in forks — this repository is the source of truth for the tickets.
2. **Work through the issues in order.** Each ticket builds on the previous ones (for example, authentication in Issue 5 assumes working orders from Issue 1 and safe processing from Issues 3–4). Do not skip ahead.
3. For each issue, **read the whole ticket first**, then implement the scope. The **acceptance criteria** are the definition of done — check them off as you go.
4. Work like it is production code: **commit small and often**, write meaningful commit messages, keep business rules out of controllers, handle failure paths, and write tests as you go rather than at the end.
5. **Do not complete the acceptance criteria mechanically.** Every technical decision should be understood and documented — the design must be defensible on its own merits.

## 6. The tickets, in order

Complete them in this exact sequence:

| Order | Issue | Focus |
|:--:|---|---|
| 1 | **Build the OrderFlow API Foundation** | Domain model, EF Core, endpoints, pagination, validation |
| 2 | **Add Real SQL Server Reporting** | Raw SQL, stored procedures, indexes, aggregation in the database |
| 3 | **Process Orders Safely** | Transactions, atomic order processing, payments, rollback on failure |
| 4 | **Fix Concurrent Order Processing** | Reproducing and fixing the oversell race, EF Core + SQL Server concurrency |
| 5 | **Add Authentication, Authorization and Auditing** | Registration/login, tokens, roles, 401/403, audit records |
| 6 | **Make OrderFlow Production Ready** | Hardening, global error handling, logging, health checks, performance, full test coverage |
| 7 | *(Optional)* **Add Redis Session Management** | Redis-backed, revocable sessions once the main work is complete |

## 7. Suggested pacing

- **Days 1–3:** Issue 1 — foundation, domain model, endpoints, migrations.
- **Days 4–6:** Issue 2 — SQL Server reporting, stored procedures, indexes.
- **Days 7–9:** Issues 3–4 — safe order processing, then concurrency.
- **Days 10–12:** Issues 5–6 — auth/authorization/auditing, then production readiness.
- **Final days:** Optional Issue 7, polish, and a full clean-checkout test run.

This is a guide, not a rule — the acceptance criteria are what count.

## 8. Final expectations

The completed project should represent a **realistic backend service rather than a tutorial CRUD application**. It should demonstrate:

- C# and ASP.NET Core development, and API design
- EF Core, Microsoft SQL Server, raw SQL, stored procedures
- Transactions and concurrency handling
- Authentication, authorization, password handling, token authentication
- Database design, query performance, and indexes
- Error handling and logging
- Unit testing and integration testing
- Production considerations

The delivered system should be one the team can confidently: **explain the design of, demonstrate, test, investigate a database query on, and make a small change to** — with a clear understanding of *why* it is built the way it is.