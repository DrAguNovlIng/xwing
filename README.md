# Xwing

X-Wing tournament tooling on **.NET Aspire** with a **MudBlazor** Blazor Web App (Interactive Server).

Target framework: **net10.0** (see [Directory.Build.props](Directory.Build.props)).

## Projects

- **Xwing.AppHost** — Aspire orchestration (local dev only)
- **Xwing.ServiceDefaults** — OpenTelemetry, health checks, service discovery
- **Xwing.Web** — Blazor UI (this is what production deploys)
- **Xwing.Tests** — Store integration tests (SQL Server via Testcontainers; requires Docker)

## Run locally

```bash
dotnet run --project Xwing.AppHost
```

Or run the web app alone (requires a SQL connection string; see Azure SQL below):

```bash
dotnet run --project Xwing.Web
```

Store tests:

```bash
dotnet test
```

Docker must be running for `Xwing.Tests` (Testcontainers starts SQL Server 2022).

## Azure deployment (App Service)

Production hosts **Xwing.Web** on a **Linux App Service** (.NET 10). GitHub Actions builds with `dotnet publish` and deploys via OpenID Connect (no publish profile). Workflow: [`.github/workflows/azure-webapp.yml`](.github/workflows/azure-webapp.yml).

- **Pull requests** to `main`: restore, test, and build (no deploy).
- **Push** to `main` or **workflow_dispatch**: build and deploy.

After the new site works, delete the old **Azure Static Web App** and the GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN_NICE_ISLAND_0493C9B03` if it is still present.

### 1. Create Azure resources

1. Create a **resource group** (for example `xwing`).
2. Create an **App Service plan**: **Linux**, your region, **Basic B1** or higher (Free tier sleeps and is a poor fit for Blazor Server SignalR circuits).
3. Create a **Web App** on that plan:
   - Runtime stack: **.NET 10**
   - Operating system: **Linux**
   - Enable **HTTPS only**
4. On the web app, open **Configuration** → **General settings**:
   - **Always on**: **On** if your plan supports it (not available on Free; the app sleeps after idle time without it).
   - **Session affinity**: **On** if you run more than one instance (Blazor Server keeps a SignalR circuit on one server). For a single instance this is optional but reasonable to enable.
   - **Web sockets (Linux)**: On **Linux** plans there is often **no** “Web sockets” toggle. That is expected: WebSockets are **always enabled** at the platform level; the portal setting applies to **Windows** App Service only. Do **not** confuse **WebJobs runtime** with WebSockets—that switch is unrelated.
   - **Web sockets (Windows)**: If you use Windows instead of Linux, set **Web sockets** to **On** under Platform settings.
   - **Free (F1) Linux**: WebSockets are supported, but Azure limits you to about **five concurrent WebSocket connections**; extra connections may receive HTTP 429. Basic B1+ is better for several users at once.
5. Note the **web app name** (used as `AZURE_WEBAPP_NAME`, not the full URL).

### Azure SQL (tournaments database)

Production data lives in **Azure SQL Database**. Local development uses the SQL Server container started by **Xwing.AppHost** (connection name `xwing`).

1. In the same resource group as the web app, create a **SQL server** (logical server) and a database named **`xwing`** (or another name if you update the connection name in code).
2. Configure **SQL authentication** (server admin login and password) or use Entra ID if you prefer that model later.
3. On the SQL server **Networking** blade, allow **Azure services** (and your client IP while testing from SSMS or Azure Data Studio).
4. Build an ADO.NET connection string for the database (`Encrypt=True`, `TrustServerCertificate=False` for Azure SQL).
5. On the **Web App**, open **Configuration** → **Application settings** and add:

   | Name | Value |
   |------|--------|
   | `ConnectionStrings__xwing` | Your Azure SQL connection string |

   Do not commit connection strings. [.gitignore](.gitignore) ignores local `appsettings*.json`.

6. Deploy or restart the web app. On startup, **Xwing.Web** applies EF Core migrations and creates the **`Tournaments`** table if it is missing (columns include `TournamentId`, `Name`, audit timestamps, and `RowVersion`).

Pull requests and pushes run `dotnet test` in GitHub Actions (Docker is available on `ubuntu-latest` runners).

### 2. Entra app registration for GitHub Actions

1. **Microsoft Entra ID** → **App registrations** → **New registration**. No redirect URI. Note the **Application (client) ID** and **Directory (tenant) ID**.
2. **Certificates & secrets** → **Federated credentials** → **Add credential**:
   - Scenario: **GitHub Actions deploying Azure resources**
   - Organization: `DrAguNovlIng`
   - Repository: `xwing`
   - Entity type: **Branch**
   - Branch: `main`
   - Name: for example `github-main`
3. On the **Web App** (not the plan), **Access control (IAM)** → **Add role assignment**:
   - Role: **Website Contributor**
   - Member: the app registration you created
   - Scope: this web app resource
4. Copy your **Subscription ID** from the web app overview or subscription blade.

### 3. GitHub repository configuration

In **Settings** → **Secrets and variables** → **Actions** for `DrAguNovlIng/xwing`:

| Type | Name | Value |
|------|------|--------|
| Secret | `AZURE_CLIENT_ID` | Application (client) ID from step 2 |
| Secret | `AZURE_TENANT_ID` | Directory (tenant) ID |
| Secret | `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| Variable | `AZURE_WEBAPP_NAME` | Web app resource name |

### 4. Verify

Push to `main` or run **Deploy Xwing.Web to Azure App Service** manually from the Actions tab. Confirm **Deploy** succeeds, then open `https://<webapp-name>.azurewebsites.net` and check that the MudBlazor shell loads and the drawer menu works.
