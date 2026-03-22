# FoodyBackend API Reference

## Update Summary

- Version 1.0, March 22, 2026: first release of the API documentation.
- Version 1.1, March 22, 2026: added `UserGroup` endpoints so users can now be linked to groups, listed per user or group, and removed again.

## Overview

FoodyBackend is an ASP.NET Core Web API with Swagger and a PostgreSQL database.

| Item | Value |
| --- | --- |
| Base API path | `/api` |
| Local HTTP URL | `http://localhost:5066` |
| Local HTTPS URL | `https://localhost:7270` |
| Swagger UI | `/swagger` |
| Authentication | None |
| Response format | JSON |

## Runtime Behavior

- The application applies Entity Framework migrations automatically during startup.
- The backend currently exposes no authentication or authorization checks.
- User passwords are currently stored and returned as plain text.
- Recipe endpoints first query the database and fall back to the CSV source if the database does not contain matching recipes.

## Root Endpoints

### GET `/`

Returns a basic service status payload.

```json
{
  "message": "FoodyBackend is running",
  "database": "PostgreSQL",
  "environment": "Production"
}
```

### GET `/swagger`

Returns the Swagger UI page for interactive API exploration.

## Health API

### GET `/api/Health`

Confirms that the API process is running.

Response:

```json
{
  "status": "ok",
  "service": "FoodyBackend"
}
```

### GET `/api/Health/db`

Checks the database connection and reports the number of pending migrations.

Success response:

```json
{
  "status": "ok",
  "database": "connected",
  "pendingMigrations": 0
}
```

Failure response example:

```json
{
  "status": "error",
  "database": "disconnected",
  "error": "Connection failure details..."
}
```

## Resource Models

### User

```json
{
  "id": 1,
  "username": "robin",
  "password": "secret"
}
```

### Group

```json
{
  "id": 1,
  "name": "Family dinners",
  "description": "Recipes and meals for the family"
}
```

### Dinner

```json
{
  "id": 1,
  "groupId": 1,
  "group": {
    "id": 1,
    "name": "Family dinners",
    "description": "Recipes and meals for the family"
  },
  "description": "Friday pasta night",
  "date": "2026-03-22T18:00:00Z"
}
```

### UserGroup

```json
{
  "id": 1,
  "userId": 1,
  "username": "robin",
  "groupId": 2,
  "groupName": "Family dinners",
  "groupDescription": "Recipes and meals for the family"
}
```

### Recipe Response

Database-backed recipe response:

```json
{
  "recipe": "Pasta Primavera",
  "ingredients": "pasta, tomato, basil",
  "directions": "Boil pasta and mix the ingredients.",
  "link": "https://example.com/recipe",
  "source": "Database",
  "data": null
}
```

CSV-backed recipe response may also contain `ner` and raw `data`:

```json
{
  "recipe": "Pasta Primavera",
  "ingredients": "pasta, tomato, basil",
  "directions": "Boil pasta and mix the ingredients.",
  "link": "https://example.com/recipe",
  "source": "Recipes1M",
  "ner": "[\"pasta\",\"tomato\",\"basil\"]",
  "data": "full,csv,row,data"
}
```

## User Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/User` | List all users |
| GET | `/api/User/{id}` | Get one user by id |
| POST | `/api/User` | Create a user |
| PUT | `/api/User/{id}` | Update a user |
| DELETE | `/api/User/{id}` | Delete a user |

### GET `/api/User`

Returns all users.

### GET `/api/User/{id}`

Returns a single user.

Responses:
- `200 OK`
- `404 Not Found`

### POST `/api/User`

Request body:

```json
{
  "username": "robin",
  "password": "secret"
}
```

Responses:
- `201 Created`
- Returns the created user object

### PUT `/api/User/{id}`

Request body:

```json
{
  "id": 1,
  "username": "robin",
  "password": "new-secret"
}
```

Responses:
- `204 No Content`
- `400 Bad Request` when route id and body id differ
- `404 Not Found`

### DELETE `/api/User/{id}`

Responses:
- `204 No Content`
- `404 Not Found`

## Group Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/Group` | List all groups |
| GET | `/api/Group/{id}` | Get one group by id |
| POST | `/api/Group` | Create a group |
| PUT | `/api/Group/{id}` | Update a group |
| DELETE | `/api/Group/{id}` | Delete a group |

### POST `/api/Group`

Request body:

```json
{
  "name": "Family dinners",
  "description": "Recipes and meals for the family"
}
```

### PUT `/api/Group/{id}`

Request body:

```json
{
  "id": 1,
  "name": "Family dinners",
  "description": "Updated description"
}
```

Responses for single-group routes:
- `200 OK` or `201 Created` where applicable
- `204 No Content` for successful update/delete
- `400 Bad Request` when route id and body id differ on update
- `404 Not Found`

## Dinner Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/Dinner` | List all dinners with group details |
| GET | `/api/Dinner/{id}` | Get one dinner with group details |
| POST | `/api/Dinner` | Create a dinner |
| PUT | `/api/Dinner/{id}` | Update a dinner |
| DELETE | `/api/Dinner/{id}` | Delete a dinner |

### POST `/api/Dinner`

Accepted request body:

```json
{
  "groupId": 1,
  "description": "Friday pasta night",
  "date": "2026-03-22T18:00:00Z"
}
```

Alternative accepted body:

```json
{
  "group": {
    "id": 1
  },
  "description": "Friday pasta night",
  "date": "2026-03-22T18:00:00Z"
}
```

Responses:
- `201 Created`
- `400 Bad Request` if the referenced group does not exist

### PUT `/api/Dinner/{id}`

Request body:

```json
{
  "id": 1,
  "groupId": 1,
  "description": "Updated dinner description",
  "date": "2026-03-23T18:00:00Z"
}
```

Responses:
- `204 No Content`
- `400 Bad Request` when route id and body id differ
- `400 Bad Request` if the referenced group does not exist
- `404 Not Found`

## UserGroup Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/UserGroup` | List all user-group links |
| GET | `/api/UserGroup/{id}` | Get one user-group link by id |
| GET | `/api/UserGroup/user/{userId}` | List all groups linked to one user |
| GET | `/api/UserGroup/group/{groupId}` | List all users linked to one group |
| POST | `/api/UserGroup` | Create a user-group link |
| DELETE | `/api/UserGroup/{id}` | Delete a user-group link |

### POST `/api/UserGroup`

Request body:

```json
{
  "userId": 1,
  "groupId": 2
}
```

Success response:

```json
{
  "id": 1,
  "userId": 1,
  "username": "robin",
  "groupId": 2,
  "groupName": "Family dinners",
  "groupDescription": "Recipes and meals for the family"
}
```

Responses:
- `201 Created`
- `400 Bad Request` if `UserId` or `GroupId` is missing
- `400 Bad Request` if the user does not exist
- `400 Bad Request` if the group does not exist
- `409 Conflict` if the user is already connected to the group

### GET `/api/UserGroup/user/{userId}`

Returns all group memberships for a single user.

Responses:
- `200 OK`
- `404 Not Found` if the user does not exist

### GET `/api/UserGroup/group/{groupId}`

Returns all user memberships for a single group.

Responses:
- `200 OK`
- `404 Not Found` if the group does not exist

### DELETE `/api/UserGroup/{id}`

Responses:
- `204 No Content`
- `404 Not Found`

## Recipe Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/Recipe/random` | Get a random recipe |
| GET | `/api/Recipe/by-ingredient?ingredient=value` | Get up to 50 matching recipes |
| GET | `/api/Recipe/one-by-ingredient?ingredient=value` | Get the first matching recipe |
| GET | `/api/Recipe/by-number/{number}` | Get recipe by 1-based position |
| POST | `/api/Recipe/import-csv?limit=100` | Import recipes from CSV into the database |

### GET `/api/Recipe/random`

Returns a random recipe from the database, or from the CSV file if the database has no recipes.

### GET `/api/Recipe/by-ingredient?ingredient={ingredient}`

Returns up to 50 matches.

Responses:
- `200 OK`
- `400 Bad Request` if `ingredient` is missing or empty
- `404 Not Found`

### GET `/api/Recipe/one-by-ingredient?ingredient={ingredient}`

Returns the first match.

Responses:
- `200 OK`
- `400 Bad Request` if `ingredient` is missing or empty
- `404 Not Found`

### GET `/api/Recipe/by-number/{number}`

Returns the recipe at a 1-based index.

Responses:
- `200 OK`
- `400 Bad Request` if `number < 1`
- `404 Not Found`

### POST `/api/Recipe/import-csv`

Imports recipes from the configured CSV file into the `Recipes` table.

Optional query parameter:
- `limit`

Success response:

```json
{
  "imported": 100,
  "source": "foodnetwork_recipes.csv"
}
```

Responses:
- `200 OK`
- `400 Bad Request` if `limit <= 0`
- `404 Not Found` if the CSV file is missing
- `409 Conflict` if recipes already exist in the table

## Example Requests

### Health check

```bash
curl http://localhost:5066/api/Health
```

### Create user

```bash
curl -X POST http://localhost:5066/api/User \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"robin\",\"password\":\"secret\"}"
```

### Create group

```bash
curl -X POST http://localhost:5066/api/Group \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Family dinners\",\"description\":\"Recipes and meals for the family\"}"
```

### Create dinner

```bash
curl -X POST http://localhost:5066/api/Dinner \
  -H "Content-Type: application/json" \
  -d "{\"groupId\":1,\"description\":\"Friday pasta night\",\"date\":\"2026-03-22T18:00:00Z\"}"
```

### Link user to group

```bash
curl -X POST http://localhost:5066/api/UserGroup \
  -H "Content-Type: application/json" \
  -d "{\"userId\":1,\"groupId\":2}"
```

### Search recipes by ingredient

```bash
curl "http://localhost:5066/api/Recipe/by-ingredient?ingredient=tomato"
```

### Import CSV recipes

```bash
curl -X POST "http://localhost:5066/api/Recipe/import-csv?limit=100"
```
