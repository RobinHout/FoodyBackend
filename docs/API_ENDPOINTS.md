# FoodyBackend API Reference

## Update Summary

- Version 1.0, March 22, 2026: first release of the API documentation.
- Version 1.1, March 22, 2026: added `UserGroup` endpoints so users can now be linked to groups, listed per user or group, and removed again.
- Version 1.2, March 27, 2026: added session-based authentication with `register`, `login`, `logout`, `refresh-token`, and `me`, started hashing stored passwords, removed passwords from `/api/User` responses, and required authorization for group, dinner, and membership-changing write operations.

## Overview

FoodyBackend is an ASP.NET Core Web API with Swagger and a PostgreSQL database.

| Item | Value |
| --- | --- |
| Base API path | `/api` |
| Local HTTP URL | `http://localhost:5066` |
| Local HTTPS URL | `https://localhost:7270` |
| Swagger UI | `/swagger` |
| Authentication | Bearer access token for protected endpoints |
| Response format | JSON |

## Runtime Behavior

- The application applies Entity Framework migrations automatically during startup.
- Authentication is handled with access tokens plus refresh tokens backed by server-side auth sessions.
- Existing plain-text passwords are upgraded to hashed values during startup, and new passwords are hashed before storage.
- `/api/User` responses no longer include any password field.
- Group, dinner, and user-group membership write operations now require an authenticated user with the appropriate group access.
- Recipe endpoints first query the database and fall back to the CSV source if the database does not contain matching recipes.

## Authentication

- Protected endpoints expect `Authorization: Bearer {accessToken}`.
- `POST /api/Auth/register` and `POST /api/Auth/login` both return the current user plus session tokens.
- `POST /api/Auth/refresh-token` rotates the refresh token and returns a new session payload.
- `POST /api/Auth/logout` revokes the current access-token session.

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
  "username": "robin"
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

### Auth Session

```json
{
  "tokenType": "Bearer",
  "accessToken": "7E8E0C0D...",
  "accessTokenExpiresAtUtc": "2026-03-27T15:30:00Z",
  "refreshToken": "9F0A1B2C...",
  "refreshTokenExpiresAtUtc": "2026-04-26T15:00:00Z"
}
```

### Me Response

```json
{
  "id": 1,
  "username": "robin",
  "groups": [
    {
      "id": 2,
      "name": "Family dinners",
      "description": "Recipes and meals for the family"
    }
  ]
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

## Auth Endpoints

| Method | Route | Description |
| --- | --- | --- |
| POST | `/api/Auth/register` | Create a user account and return a session |
| POST | `/api/Auth/login` | Authenticate a user and return a session |
| POST | `/api/Auth/logout` | Revoke the current session |
| POST | `/api/Auth/refresh-token` | Exchange a refresh token for a new session |
| GET | `/api/Auth/me` | Return the current authenticated user plus group memberships |

### POST `/api/Auth/register`

Request body:

```json
{
  "username": "robin",
  "password": "secret"
}
```

Success response:

```json
{
  "user": {
    "id": 1,
    "username": "robin"
  },
  "session": {
    "tokenType": "Bearer",
    "accessToken": "7E8E0C0D...",
    "accessTokenExpiresAtUtc": "2026-03-27T15:30:00Z",
    "refreshToken": "9F0A1B2C...",
    "refreshTokenExpiresAtUtc": "2026-04-26T15:00:00Z"
  }
}
```

Responses:
- `201 Created`
- `400 Bad Request` if username or password is missing
- `409 Conflict` if the username already exists

### POST `/api/Auth/login`

Request body:

```json
{
  "username": "robin",
  "password": "secret"
}
```

Responses:
- `200 OK`
- Returns the same payload shape as `register`
- `400 Bad Request` if username or password is missing
- `401 Unauthorized` if the credentials are invalid

### POST `/api/Auth/logout`

Headers:
- `Authorization: Bearer {accessToken}`

Responses:
- `204 No Content`
- `401 Unauthorized` if the access token is missing or invalid

### POST `/api/Auth/refresh-token`

Request body:

```json
{
  "refreshToken": "9F0A1B2C..."
}
```

Responses:
- `200 OK`
- Returns a new auth session payload
- `400 Bad Request` if `refreshToken` is missing
- `401 Unauthorized` if the refresh token is invalid or expired

### GET `/api/Auth/me`

Headers:
- `Authorization: Bearer {accessToken}`

Responses:
- `200 OK`
- Returns the current user with their linked groups
- `401 Unauthorized` if the access token is missing or invalid
- `404 Not Found` if the authenticated user no longer exists

## User Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/User` | List all users without passwords |
| GET | `/api/User/{id}` | Get one user by id without a password |
| POST | `/api/User` | Create a user without returning a session |
| PUT | `/api/User/{id}` | Update the current authenticated user |
| DELETE | `/api/User/{id}` | Delete the current authenticated user |

### GET `/api/User`

Returns all users without password information.

### GET `/api/User/{id}`

Returns a single user without password information.

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
- Returns the created user object without password information
- `400 Bad Request` if username or password is missing
- `409 Conflict` if the username already exists

### PUT `/api/User/{id}`

Headers:
- `Authorization: Bearer {accessToken}`

Request body:

```json
{
  "id": 1,
  "username": "robin",
  "password": "new-secret"
}
```

Notes:
- `password` is optional. When omitted or empty, the existing password hash is kept.
- A user can only update their own record.

Responses:
- `204 No Content`
- `400 Bad Request` when route id and body id differ
- `400 Bad Request` if `username` is missing
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the token belongs to a different user
- `404 Not Found`
- `409 Conflict` if the username already exists

### DELETE `/api/User/{id}`

Headers:
- `Authorization: Bearer {accessToken}`

Responses:
- `204 No Content`
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the token belongs to a different user
- `404 Not Found`

## Group Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/Group` | List all groups |
| GET | `/api/Group/{id}` | Get one group by id |
| POST | `/api/Group` | Create a group and add the current user as a member |
| PUT | `/api/Group/{id}` | Update a group if the current user belongs to it |
| DELETE | `/api/Group/{id}` | Delete a group if the current user belongs to it |

### POST `/api/Group`

Headers:
- `Authorization: Bearer {accessToken}`

Request body:

```json
{
  "name": "Family dinners",
  "description": "Recipes and meals for the family"
}
```

Responses:
- `201 Created`
- `401 Unauthorized` if no access token is provided

### PUT `/api/Group/{id}`

Headers:
- `Authorization: Bearer {accessToken}`

Request body:

```json
{
  "id": 1,
  "name": "Family dinners",
  "description": "Updated description"
}
```

Responses:
- `204 No Content`
- `400 Bad Request` when route id and body id differ on update
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is not a member of the group
- `404 Not Found`

### DELETE `/api/Group/{id}`

Headers:
- `Authorization: Bearer {accessToken}`

Responses:
- `204 No Content`
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is not a member of the group
- `404 Not Found`

## Dinner Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/Dinner` | List all dinners with group details |
| GET | `/api/Dinner/{id}` | Get one dinner with group details |
| POST | `/api/Dinner` | Create a dinner in a group the current user belongs to |
| PUT | `/api/Dinner/{id}` | Update a dinner if the current user belongs to the involved group |
| DELETE | `/api/Dinner/{id}` | Delete a dinner if the current user belongs to the dinner's group |

### POST `/api/Dinner`

Headers:
- `Authorization: Bearer {accessToken}`

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
- `400 Bad Request` if `groupId` is missing
- `400 Bad Request` if the referenced group does not exist
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is not a member of the target group

### PUT `/api/Dinner/{id}`

Headers:
- `Authorization: Bearer {accessToken}`

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
- `400 Bad Request` if `groupId` is missing
- `400 Bad Request` if the referenced group does not exist
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is not a member of the current dinner group or the target group
- `404 Not Found`

### DELETE `/api/Dinner/{id}`

Headers:
- `Authorization: Bearer {accessToken}`

Responses:
- `204 No Content`
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is not a member of the dinner's group
- `404 Not Found`

## UserGroup Endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/api/UserGroup` | List all user-group links |
| GET | `/api/UserGroup/{id}` | Get one user-group link by id |
| GET | `/api/UserGroup/user/{userId}` | List all groups linked to one user |
| GET | `/api/UserGroup/group/{groupId}` | List all users linked to one group |
| POST | `/api/UserGroup` | Create a user-group link if the current user belongs to that group |
| DELETE | `/api/UserGroup/{id}` | Delete a user-group link if the current user belongs to the group or owns the link |

### POST `/api/UserGroup`

Headers:
- `Authorization: Bearer {accessToken}`

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
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is not already a member of the target group
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

Headers:
- `Authorization: Bearer {accessToken}`

Responses:
- `204 No Content`
- `401 Unauthorized` if no access token is provided
- `403 Forbidden` if the current user is neither the linked user nor a member of the group
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

### Register

```bash
curl -X POST http://localhost:5066/api/Auth/register \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"robin\",\"password\":\"secret\"}"
```

### Login

```bash
curl -X POST http://localhost:5066/api/Auth/login \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"robin\",\"password\":\"secret\"}"
```

### Get current user

```bash
curl http://localhost:5066/api/Auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### Create group

```bash
curl -X POST http://localhost:5066/api/Group \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d "{\"name\":\"Family dinners\",\"description\":\"Recipes and meals for the family\"}"
```

### Create dinner

```bash
curl -X POST http://localhost:5066/api/Dinner \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d "{\"groupId\":1,\"description\":\"Friday pasta night\",\"date\":\"2026-03-22T18:00:00Z\"}"
```

### Link user to group

```bash
curl -X POST http://localhost:5066/api/UserGroup \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
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
