### 🐳 Docker Compose: Developer's Cheat Sheet

This document contains key information, essential commands, and best practices for managing the local infrastructure of the project. 

### 🧱 Architecture Components

Our docker-compose.yml deploys 3 core services, isolated inside a custom bridge network named logistics-network: 

1. **logistics-db (PostgreSQL 16):** 

  * **Purpose:** Relational database for transaction data storage across microservices.
  * **Internal Host (within Docker network):** logistics-db
  * **External Port (for IDE/Host machine):** 5432
2. **logistics-cache (Redis 7):** 

  * **Purpose:** Fast In-Memory caching for hot data (e.g., live tracking, active statuses). Password protected.
  * **Internal Host:** logistics-cache
  * **External Port:** 6379
3. **logistics-pgadmin (pgAdmin 4):** 

  * **Purpose:** Web-based GUI client for visual PostgreSQL database management.
  * **Browser Access URL:** http://localhost:5050

### 💻 Essential CLI Commands

All commands must be executed from the docker/ folder where your docker-compose.yml file is located. 

### 🚀 Start & Stop

* **docker-compose up -d** – Starts all containers in the background (detached mode). *This will be your most frequently used command.*
* **docker-compose down** – Stops and removes containers and networks but **preserves** all data stored in Volumes.
* **docker-compose down -v** – Stops containers and **completely deletes** all data volumes. Use this when you want to reset your database and cache states to blank slate.

### 📊 Monitoring & Logs

* **docker-compose ps** – Lists all running containers managed by the current compose file along with their status.
* **docker-compose logs** – Displays aggregated logs from all running services.
* **docker-compose logs -f [service_name]** – Streams live logs for a specific container (e.g., docker-compose logs -f logistics-db).

### 🛠️ Interactive Shell

* **docker exec -it logistics-redis-cache redis-cli** – Drops you directly into the interactive Redis CLI client inside the running container.

### 💡 Key Best Practices

### 🔑 Security & .env File

* **Never commit the .env file**. It has been added to .gitignore.
* If you modify any value inside the .env file, you must run docker-compose up -d again so Docker Compose can reload the updated environment variables.

### 💾 Data Persistence (Volumes)

* PostgreSQL and Redis data persists even if you restart your machine or run docker-compose down, because the files are mapped into dedicated named docker volumes: postgres_data and redis_data.

### 🔄 Recreating Databases

* The init.sql initialization script executes **only once**—when the PostgreSQL container is built for the very first time. If you decide to add a new database to that initialization script later on, you need to wipe the existing volume using docker-compose down -v and start over.