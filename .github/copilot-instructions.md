# Copilot Instructions for ERP Inventory Module

## Project Overview

This is an Angular + .NET microservices ERP inventory management system with the following architecture:

- **Frontend**: Angular 20 + Angular Material SPA
- **Backend**: .NET 9 microservices (Auth, Inventory, Order, Supplier services)
- **API Gateway**: YARP reverse proxy with JWT validation, CORS, rate limiting
- **Databases**: Oracle 19c (Inventory, Supplier), PostgreSQL (Auth, Orders)
- **Messaging**: Apache Kafka 4.0 for async communication
- **Local Dev**: Docker Compose orchestration

## Core Commands

### Development

- `docker-compose up --build` - Start all services and dependencies
- `ng serve` - Start Angular development server
- `dotnet build` - Build all .NET projects
- `dotnet test` - Run backend tests (xUnit + Testcontainers)
- `ng test` - Run frontend tests (Karma + Jasmine)
- `ng build` - Build Angular for production

### Database & Migration

- Database initialization handled by `DbInitializer` classes in each service
- Seed data: 10 inventory items, 3 suppliers, 5 orders
- No explicit migration commands - services auto-initialize on startup

### Task Management (Taskmaster)

- `task-master list` - View current tasks
- `task-master next` - Get next available task
- `task-master show <id>` - View task details
- `task-master expand <id>` - Break down complex tasks
- `task-master set-status <id> <status>` - Update task status

### Linting & Quality

- Follow .NET coding conventions
- Use Angular ESLint for frontend code quality
- Structured logging with Serilog (JSON format)

## Architecture Components

### Microservices

- **Auth.Service** (Port 5006): User auth, JWT tokens, PostgreSQL
- **Inventory.Service** (Port 5007): Inventory CRUD, stock alerts, Oracle 19c
- **Order.Service** (Port 5008): Order management, PostgreSQL
- **Supplier.Service** (Port 5009): Supplier CRUD, Oracle 19c
- **Gateway.API** (Port 5010): YARP routing, JWT validation, CORS

### Data Stores

- **Oracle 19c** (Port 1521): Inventory and Supplier data
- **PostgreSQL** (Port 5432): Auth and Order data
- **Kafka** (Port 9092): Async messaging (inventory.alerts, order.created, order.completed)

### External APIs

- Kafka topics for inter-service communication
- SignalR hub in Gateway for real-time alerts
- JWT-based authentication across all services

## Repository-Specific Style Rules

### .NET Services

- **Architecture**: Domain-Driven Design with Application/Domain/Infrastructure layers
- **Error Handling**: Use Polly for resilience (retry, circuit breaker, fallback)
- **Logging**: Structured logging with Serilog, include correlation IDs
- **Testing**: xUnit + Testcontainers for integration tests
- **API Documentation**: Swagger/OpenAPI with XML comments
- **Observability**: OpenTelemetry tracing, Prometheus metrics at `/metrics`

### Angular Frontend

- **Components**: Use Angular Material exclusively for UI
- **Forms**: ReactiveFormsModule for all user inputs
- **State**: Store JWT in localStorage, use services for data management
- **Navigation**: Route guards for protected routes
- **Notifications**: MatSnackBar for alerts and low-stock warnings
- **Testing**: Karma + Jasmine with mocked HTTP clients

### Database Patterns

- **Entity Framework**: Use Oracle.EntityFrameworkCore for Oracle, Npgsql for PostgreSQL
- **Connection Strings**: Configure in appsettings.json, use Docker service names
- **Seeding**: DbInitializer classes run on startup if database is empty
- **Authorization**: `[Authorize]` attributes on protected endpoints

### Kafka Integration

- **Client**: Use Confluent.Kafka library
- **Patterns**: Producer/Consumer services injected via DI
- **Topics**: inventory.alerts, order.created, order.completed
- **Configuration**: Bootstrap servers at `kafka:9092` in Docker

### Docker Configuration

- **Orchestration**: Single docker-compose.yml for all services
- **Networks**: Shared network for inter-service communication
- **Volumes**: Persistent storage for databases
- **Environment**: Use .env file for sensitive configuration

## Taskmaster Integration Rules

This project uses Taskmaster AI for task-driven development:

### Task Management Workflow

- Always check current tasks with `get_tasks` before starting work
- Use `next_task` to identify priority work
- Break down complex tasks with `expand_task --research`
- Log implementation progress with `update_subtask`
- Mark completed work with `set_task_status --status=done`

### Task Context (Tags)

- **master**: Main development tasks
- Use tags for feature branches: `add_tag feature-[name] --from-branch`
- Switch contexts with `use_tag <tagname>` when working on different features

### AI-Powered Features

- Use `research` tool for up-to-date technology guidance
- Leverage `--research` flag for informed task breakdown
- Apply complexity analysis with `analyze_project_complexity`

### Implementation Logging

- Document discoveries in subtasks using `update_subtask`
- Include specific code changes, patterns that worked/failed
- Reference actual files and line numbers where relevant
- Update task dependencies when implementation approach changes

## Development Workflow

1. **Start Session**: Check `task-master list` and `task-master next`
2. **Plan Work**: Use `task-master show <id>` for task details
3. **Break Down**: Use `task-master expand <id> --research` for complex tasks
4. **Research**: Use `task-master research` for current best practices
5. **Implement**: Follow DDD patterns, use appropriate tech stack
6. **Log Progress**: Use `update_subtask` to document findings
7. **Test**: Write xUnit/Karma tests, verify via Swagger UI
8. **Complete**: Set status to done, commit with descriptive messages

## Error Handling Patterns

### Resilience (Polly)

- Exponential backoff retry (max 3 attempts)
- Circuit breaker (5 failures in 30s)
- Fallback responses for critical paths

### Compensation Patterns

- Optimistic order approval when inventory service unavailable
- Dead letter queues for failed Kafka messages
- Graceful degradation in Angular SPA

### Monitoring

- Health checks at `/health` endpoints
- Structured logging with correlation IDs
- Distributed tracing with OpenTelemetry
- Prometheus metrics for SLO monitoring

## Security Requirements

- JWT-based authentication with role claims (Admin, Manager, Clerk)
- CORS configuration in API Gateway
- Rate limiting for API protection
- Environment variables for sensitive configuration (use .env.example template)
- OWASP Top 10 mitigation strategies

## Performance Targets

## If in Doubt: Taskmaster Guidance

If you encounter any issue, uncertainty, or are unsure how to proceed at any point, fetch the current tasks from the Taskmaster MCP task list (`get_tasks`) and follow the guidance or priorities indicated there before continuing development or making decisions.
