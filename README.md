# ERP Inventory Module

A modern microservices-based ERP inventory management system built with .NET 9, Angular 19, and Apache Kafka.

## Architecture Overview

This system consists of:

- **Frontend**: Angular 19 SPA with Angular Material
- **API Gateway**: YARP reverse proxy with JWT authentication
- **Microservices**: Auth, Inventory, Order, and Supplier services
- **Databases**: PostgreSQL (Auth/Orders) and Oracle 19c (Inventory/Suppliers)
- **Messaging**: Apache Kafka for async communication
- **Infrastructure**: Docker Compose for local development

## Quick Start

### Prerequisites

- Docker Desktop
- .NET 9 SDK
- Node.js 20+ and npm
- Git

### Development Setup

1. **Clone and setup the repository:**

   ```bash
   git clone <repository-url>
   cd erp-inventory-module
   ```

2. **Start infrastructure services:**

   ```bash
   docker-compose up -d postgres oracle kafka zookeeper
   ```

3. **Wait for services to be healthy:**

   ```bash
   docker-compose ps
   ```

4. **Build and run the .NET services:**

   ```bash
   # Build all services
   dotnet build ERPInventoryModule.sln

   # Or run individual services during development
   dotnet run --project src/Gateway.API
   dotnet run --project src/Auth.Service
   dotnet run --project src/Inventory.Service
   dotnet run --project src/Order.Service
   dotnet run --project src/Supplier.Service
   ```

5. **Setup and run the Angular frontend:**
   ```bash
   cd frontend/erp-inventory-angular
   npm install
   ng serve
   ```

### Alternative: Run Everything with Docker

To run all services including the .NET applications:

```bash
docker-compose --profile services up --build
```

## Service Endpoints

| Service           | URL                   | Swagger                       |
| ----------------- | --------------------- | ----------------------------- |
| Gateway API       | http://localhost:5010 | http://localhost:5010/swagger |
| Auth Service      | http://localhost:5006 | http://localhost:5006/swagger |
| Inventory Service | http://localhost:5007 | http://localhost:5007/swagger |
| Order Service     | http://localhost:5008 | http://localhost:5008/swagger |
| Supplier Service  | http://localhost:5009 | http://localhost:5009/swagger |
| Angular App       | http://localhost:4200 | -                             |

## Infrastructure Services

| Service    | URL            | Credentials          |
| ---------- | -------------- | -------------------- |
| PostgreSQL | localhost:5432 | postgres/postgres123 |
| Oracle XE  | localhost:1521 | sys/Oracle123!       |
| Kafka      | localhost:9092 | -                    |
| Zookeeper  | localhost:2181 | -                    |

## Database Information

### PostgreSQL Databases

- `erp_auth` - Authentication and user management
- `erp_orders` - Order processing and management

### Oracle Schemas

- `INVENTORY` - Inventory items and stock management
- `SUPPLIER` - Supplier information and relationships

## Kafka Topics

- `inventory.alerts` - Low stock notifications
- `order.created` - New order events
- `order.completed` - Completed order events

## Development Workflow

1. Check current tasks: `task-master list`
2. Get next task: `task-master next`
3. Update task status: `task-master set-status <id> in-progress`
4. Work on implementation
5. Update subtasks: `task-master update-subtask <id> "<progress notes>"`
6. Complete task: `task-master set-status <id> done`

## Testing

### Backend Tests

```bash
# Run unit tests
dotnet test

# Run integration tests (requires Docker)
dotnet test --filter Category=Integration
```

### Frontend Tests

```bash
cd frontend/erp-inventory-angular
npm test
npm run e2e
```

## Environment Variables

Copy `.env.example` to `.env` and configure:

- Database connection strings
- JWT signing keys
- Kafka configuration
- External service URLs

## Project Structure

```
├── src/                     # .NET microservices
│   ├── Gateway.API/         # YARP API Gateway
│   ├── Auth.Service/        # Authentication service
│   ├── Inventory.Service/   # Inventory management
│   ├── Order.Service/       # Order processing
│   ├── Supplier.Service/    # Supplier management
│   └── Shared/              # Shared libraries
├── tests/                   # Test projects
├── frontend/                # Angular SPA
│   └── erp-inventory-angular/
├── scripts/                 # Database initialization
├── docker-compose.yml       # Docker orchestration
└── .taskmaster/             # Task management
```

## Contributing

1. Follow the existing code style and patterns
2. Write tests for new functionality
3. Update documentation as needed
4. Use Task Master for tracking work progress

## License

This project is licensed under the MIT License.
