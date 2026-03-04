# Architecture Diagram

## Expense Management System - Azure Services

```mermaid
graph TB
    User["👤 User / Browser"]
    Manager["👔 Manager / Browser"]

    subgraph Azure["☁️ Azure (UKSOUTH)"]
        subgraph AppService["App Service (Standard S1)"]
            WebApp["🌐 ExpenseApp\n(ASP.NET Core .NET 8\nRazor Pages + REST API)"]
            ChatUI["💬 ChatApp\n(Chat UI\nASP.NET Core .NET 8)"]
        end

        MI["🔑 User-Assigned\nManaged Identity\nmid-AppModAssist-020317"]

        subgraph SQL["Azure SQL Database"]
            SQLServer["🗄️ SQL Server\n(Entra ID Auth Only)"]
            DB["📦 Northwind DB\n(Basic Tier)"]
        end

        subgraph GenAI["GenAI Resources (Sweden Central)"]
            AOAI["🤖 Azure OpenAI\nGPT-4o (capacity 8)"]
            Search["🔍 Azure AI Search\n(S0 SKU)"]
        end
    end

    %% User interactions
    User -->|"HTTPS"| WebApp
    Manager -->|"HTTPS (Approve View)"| WebApp
    User -->|"HTTPS (Chat)"| ChatUI

    %% App to DB
    WebApp -->|"Managed Identity Auth\n(Stored Procedures)"| SQLServer
    SQLServer --- DB

    %% App to GenAI
    WebApp -.->|"Optional: OpenAI calls"| AOAI
    ChatUI -->|"OpenAI API calls\n(function calling)"| AOAI
    ChatUI -->|"Expense REST APIs"| WebApp
    AOAI -.->|"RAG queries"| Search

    %% Managed Identity assignments
    MI -->|"db_datareader\ndb_datawriter\nexecute"| DB
    MI -->|"Cognitive Services\nOpenAI User role"| AOAI
    MI -->|"Search Index\nData Contributor"| Search
    MI -->|"Assigned to"| WebApp
    MI -->|"Assigned to"| ChatUI

    %% Styling
    classDef azure fill:#0078D4,stroke:#005a9e,color:#fff
    classDef app fill:#1e3a5f,stroke:#0e2040,color:#fff
    classDef sql fill:#cc2936,stroke:#a01020,color:#fff
    classDef ai fill:#7c3aed,stroke:#5b21b6,color:#fff
    classDef identity fill:#16a34a,stroke:#14532d,color:#fff
    classDef user fill:#374151,stroke:#1f2937,color:#fff

    class WebApp,ChatUI app
    class SQLServer,DB sql
    class AOAI,Search ai
    class MI identity
    class User,Manager user
```

## Component Details

| Component | Azure Service | SKU | Region | Purpose |
|-----------|--------------|-----|--------|---------|
| ExpenseApp | App Service | Standard S1 | UK South | Main Razor Pages web app + REST API |
| ChatApp | App Service | Standard S1 | UK South | AI Chat UI (optional) |
| Managed Identity | User-Assigned MI | - | UK South | Passwordless auth for all Azure services |
| SQL Server | Azure SQL Server | - | UK South | Database server (Entra ID auth only) |
| Northwind DB | Azure SQL Database | Basic | UK South | Expense data storage |
| Azure OpenAI | Cognitive Services | S0 | Sweden Central | GPT-4o model for AI chat |
| AI Search | Azure Cognitive Search | Standard | UK South | RAG / search capabilities |

## Authentication Flow

```
App Service (with Managed Identity)
    → Uses Managed Identity Client ID
    → Azure AD issues token automatically
    → SQL Server validates token (no passwords)
    → Azure OpenAI validates token (no API keys)
    → AI Search validates token (no API keys)
```

## Deployment Commands

```bash
# Deploy without GenAI (app + database only):
bash deploy.sh

# Deploy with GenAI (full deployment including OpenAI + AI Search):
bash deploy-with-chat.sh
```

## Notes
- All resources use lowercase names (Azure requirement)
- Resource names are unique using `uniqueString(resourceGroup().id)`
- Azure OpenAI deployed to Sweden Central for GPT-4o quota availability
- GenAI resources are optional - the app works without them (shows dummy chat response)
- Navigate to `<app-url>/Index` to access the app (not the root URL)
