# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY AI.MedicareAssistant.slnx .
COPY AI.MedicareAssistant.Api/AI.MedicareAssistant.Api.csproj AI.MedicareAssistant.Api/
COPY AI.MedicareAssistant.Application/AI.MedicareAssistant.Application.csproj AI.MedicareAssistant.Application/
COPY AI.MedicareAssistant.Domain/AI.MedicareAssistant.Domain.csproj AI.MedicareAssistant.Domain/
COPY AI.MedicareAssistant.Infrastructure/AI.MedicareAssistant.Infrastructure.csproj AI.MedicareAssistant.Infrastructure/
COPY AI.MedicareAssistant.Tests/AI.MedicareAssistant.Tests.csproj AI.MedicareAssistant.Tests/

RUN dotnet restore AI.MedicareAssistant.slnx

# Copy everything and publish
COPY . .
RUN dotnet publish AI.MedicareAssistant.Api/AI.MedicareAssistant.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user for security
#RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

# Logs directory (writable by appuser)
#RUN mkdir -p /app/Logs && chown -R appuser:appuser /app/Logs
RUN mkdir -p /app/Logs

#USER appuser

EXPOSE 5024

ENV ASPNETCORE_URLS=http://+:5024
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AI.MedicareAssistant.Api.dll"]
