# ─────────────────────────────────────────────────
# AI Medicare Assistant — One-command deploy (Windows)
# Usage: .\deploy.ps1 [-Action build|down|logs|restart]
# ─────────────────────────────────────────────────
param(
    [ValidateSet("up", "build", "down", "logs", "restart")]
    [string]$Action = "build"
)

$composeFile = "docker-compose.yml"

switch ($Action) {
    "build" {
        Write-Host "Building and starting containers..." -ForegroundColor Cyan
        docker compose -f $composeFile up -d --build
    }
    "down" {
        Write-Host "Stopping containers..." -ForegroundColor Yellow
        docker compose -f $composeFile down
    }
    "logs" {
        docker compose -f $composeFile logs -f
    }
    "restart" {
        Write-Host "Restarting containers..." -ForegroundColor Cyan
        docker compose -f $composeFile down
        docker compose -f $composeFile up -d --build
    }
    "up" {
        Write-Host "Starting containers (use -Action build to rebuild)..." -ForegroundColor Cyan
        docker compose -f $composeFile up -d
    }
}

if ($Action -notin @("down", "logs")) {
    Write-Host ""
    Write-Host "  UI:  http://localhost:9600" -ForegroundColor Green
    Write-Host "  API: http://localhost:5024" -ForegroundColor Green
    Write-Host ""
}
