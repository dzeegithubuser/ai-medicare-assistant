#!/bin/bash
# Post-edit hook: scans the edited file for Medicare Assistant standard violations.
# Runs after Edit / Write / MultiEdit on .cs files (API) and .ts files (UI).
# Exits 2 with stderr feedback when violations are found, so Claude sees and fixes them on the next pass.

set -uo pipefail

INPUT=$(cat)

# Extract file_path from the hook JSON input (Edit / Write / MultiEdit all have tool_input.file_path)
FILE=$(printf '%s' "$INPUT" | grep -o '"file_path":[[:space:]]*"[^"]*"' | head -1 | sed 's/.*"file_path":[[:space:]]*"//;s/"$//')

# Normalize backslashes (Windows paths) to forward slashes for matching
FILE_NORM="${FILE//\\//}"

[[ -z "$FILE_NORM" ]] && exit 0
[[ -f "$FILE_NORM" ]] || exit 0

# Determine file kind by path
case "$FILE_NORM" in
  */api-ai-medicare-assistant/*.cs)
    KIND="cs"
    ;;
  */ui-ai-medicare-assistant/*.ts)
    KIND="ts"
    ;;
  *) exit 0 ;;
esac

# Skip generated / build output
case "$FILE_NORM" in
  */obj/*|*/bin/*|*/node_modules/*|*/dist/*) exit 0 ;;
  *.Designer.cs|*.g.cs|*.AssemblyInfo.cs) exit 0 ;;
esac

IS_TEST_FILE=0
case "$FILE_NORM" in
  *.Tests/*|*Tests.cs|*Test.cs|*.spec.ts) IS_TEST_FILE=1 ;;
esac

WARNINGS=""

# ========== C# (.NET 10 Clean Architecture) checks ==========
if [[ "$KIND" == "cs" ]]; then

  # --- Synchronous blocking on async (deadlock risk) ---
  if grep -nE '\.Result[^a-zA-Z_0-9]|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - Synchronous blocking on async (.Result / .Wait() / .GetAwaiter().GetResult()) — use await instead.\n"
  fi

  # --- async void (only allowed for event handlers) ---
  if grep -nE '(public|private|protected|internal)[[:space:]]+(static[[:space:]]+)?async[[:space:]]+void' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - async void — use 'async Task' so exceptions and lifetime are observable.\n"
  fi

  # --- Console.WriteLine — production code should use ILogger<T> via Serilog ---
  if [[ $IS_TEST_FILE -eq 0 ]]; then
    if grep -nE 'Console\.(Write|WriteLine|Error\.WriteLine)' "$FILE_NORM" >/dev/null 2>&1; then
      WARNINGS+="  - Console.WriteLine / Console.Error.WriteLine — use ILogger<T> via constructor DI (Serilog).\n"
    fi
  fi

  # --- new HttpClient() instead of IHttpClientFactory ---
  if grep -nE 'new[[:space:]]+HttpClient\(' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - new HttpClient() — register via AddInfrastructureHttpClients() / AddHttpClient<T>() and inject.\n"
  fi

  # --- Application services should use interfaces ---
  # If we see "public class XService" without "I<X>Service" anywhere in the file, flag it.
  case "$FILE_NORM" in
    */AI.MedicareAssistant.Application/Services/*Service.cs)
      CLASS_NAME=$(grep -oE 'public[[:space:]]+(sealed[[:space:]]+|abstract[[:space:]]+)?class[[:space:]]+[A-Za-z_][A-Za-z_0-9]*Service' "$FILE_NORM" | head -1 | awk '{print $NF}')
      if [[ -n "$CLASS_NAME" ]] && ! grep -nE "I${CLASS_NAME}\b|: I[A-Z][A-Za-z_0-9]*Service" "$FILE_NORM" >/dev/null 2>&1; then
        WARNINGS+="  - Application service '${CLASS_NAME}' does not implement an I${CLASS_NAME} interface — every Application service must have an interface (see feedback_application_services_use_interfaces memory).\n"
      fi
      ;;
  esac

  # --- Reaching for OpenAI / Anthropic / Gemini SDK directly from Application/Domain ---
  case "$FILE_NORM" in
    */AI.MedicareAssistant.Application/*|*/AI.MedicareAssistant.Domain/*)
      if grep -nE 'using[[:space:]]+OpenAI|using[[:space:]]+Anthropic\.|using[[:space:]]+Google\.GenerativeAI' "$FILE_NORM" >/dev/null 2>&1; then
        WARNINGS+="  - Application/Domain layer imports an AI provider SDK directly — route through IChatClient (Microsoft.Extensions.AI) only.\n"
      fi
      ;;
  esac

  # --- Domain depending on Application/Infrastructure (Clean Architecture inversion) ---
  case "$FILE_NORM" in
    */AI.MedicareAssistant.Domain/*)
      if grep -nE 'using[[:space:]]+Application|using[[:space:]]+Infrastructure' "$FILE_NORM" >/dev/null 2>&1; then
        WARNINGS+="  - Domain layer must not reference Application or Infrastructure — Clean Architecture dependency direction is inward-only.\n"
      fi
      ;;
  esac

  # --- EF Core / DbContext / SQL drift (this project is MongoDB.Driver only) ---
  if grep -nE 'using[[:space:]]+Microsoft\.EntityFrameworkCore|DbContext|using[[:space:]]+System\.Data\.SqlClient' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - EF Core / SQL drift detected — this project uses MongoDB.Driver only. No EF / SqlClient.\n"
  fi

  # --- Returning Result<T> / error tuples instead of throwing AppException ---
  if grep -nE 'Result<' "$FILE_NORM" >/dev/null 2>&1 \
    && grep -nE 'Application|Api' <<<"$FILE_NORM" >/dev/null 2>&1; then
    if grep -nE 'class[[:space:]]+[A-Za-z_][A-Za-z_0-9]*Result' "$FILE_NORM" >/dev/null 2>&1; then
      :  # The file *defines* a Result type — that's fine
    elif grep -nE 'return[[:space:]]+(new[[:space:]]+)?Result<' "$FILE_NORM" >/dev/null 2>&1; then
      WARNINGS+="  - Returning Result<T> — throw an AppException subtype (NotFoundException / ValidationException / UnauthorizedException / ConflictException). The GlobalExceptionMiddleware maps them to HTTP responses.\n"
    fi
  fi

  # --- Empty catch block ---
  if grep -nE 'catch[[:space:]]*\([^)]*\)[[:space:]]*\{[[:space:]]*\}' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - Empty catch block — log via ILogger and either rethrow or throw a typed AppException.\n"
  fi

  # --- Injected service field not readonly ---
  if grep -nE '^[[:space:]]*private[[:space:]]+(I[A-Z][A-Za-z0-9_]+|MongoDbContext|IMongoCollection<)[[:space:]]+_' "$FILE_NORM" \
    | grep -v 'readonly' >/dev/null 2>&1; then
    WARNINGS+="  - Injected dependency field not 'readonly' — use 'private readonly IFoo _foo;'.\n"
  fi

  # --- Hardcoded prompt strings in C# (must live under Api/Prompts/) ---
  case "$FILE_NORM" in
    */AI.MedicareAssistant.Application/*|*/AI.MedicareAssistant.Infrastructure/AI/*)
      if grep -nE '"You are .{20,}|"Your task is .{20,}|"<\|system\|>' "$FILE_NORM" >/dev/null 2>&1; then
        WARNINGS+="  - Looks like an inline prompt string — prompts live under Api/Prompts/*.txt and are loaded via PromptBuilder.\n"
      fi
      ;;
  esac

  # --- ProviderSDK-style provider switch in Application (must be in Infrastructure/AI) ---
  case "$FILE_NORM" in
    */AI.MedicareAssistant.Application/*)
      if grep -nE 'switch.+AiProvider|AiProvider ==' "$FILE_NORM" >/dev/null 2>&1; then
        WARNINGS+="  - AiProvider-switching logic detected in Application — provider selection belongs in Api/Extensions/AiExtensions.AddAiProvider().\n"
      fi
      ;;
  esac

  # --- Hardcoded secrets ---
  if grep -niE '(connectionString=|password=|api[_-]?key=)[^[:space:]"]+[^"\$]' "$FILE_NORM" 2>/dev/null \
    | grep -v 'Configuration\[' \
    | grep -v '\$\{' \
    | grep -v '^[[:space:]]*//' >/dev/null 2>&1; then
    WARNINGS+="  - Possible hardcoded secret — read via IConfiguration / User Secrets / env vars only.\n"
  fi
fi

# ========== TypeScript (Angular 21) checks ==========
if [[ "$KIND" == "ts" ]]; then
  case "$FILE_NORM" in
    *.spec.ts) exit 0 ;;  # tests have looser rules
  esac

  # --- NgModule in new code ---
  if grep -nE '@NgModule\(' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - @NgModule detected — Angular 21 uses standalone components only. Drop the NgModule and add the imports to the component's 'imports:' array.\n"
  fi

  # --- @Input() / @Output() decorators in new code ---
  if grep -nE '@Input\(|@Output\(' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - @Input() / @Output() decorators — use the signal-based input() / output() functions instead.\n"
  fi

  # --- Constructor DI ---
  if grep -nE 'constructor[[:space:]]*\([[:space:]]*(public|private|protected|readonly)[[:space:]]+[a-zA-Z_]+:[[:space:]]*[A-Z]' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - Constructor DI parameter detected — use 'inject()' inside the class body, not constructor parameters.\n"
  fi

  # --- Missing ChangeDetectionStrategy.OnPush ---
  if grep -nE '@Component\(' "$FILE_NORM" >/dev/null 2>&1 \
     && ! grep -nE 'changeDetection:[[:space:]]*ChangeDetectionStrategy\.OnPush' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - @Component missing 'changeDetection: ChangeDetectionStrategy.OnPush' — every component must opt in to OnPush.\n"
  fi

  # --- localStorage for JWT (must be sessionStorage) ---
  if grep -nE 'localStorage\.(setItem|getItem).*token' "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - localStorage used for token — JWTs live in sessionStorage with a 1-hour expiry timestamp (see auth.service.ts).\n"
  fi

  # --- Manually adding Authorization header (interceptor handles it) ---
  if grep -nE "Authorization[\"\\']:[[:space:]]*['\\\"]Bearer" "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - Manual Authorization header — auth.interceptor.ts attaches the JWT automatically. Don't duplicate it.\n"
  fi

  # --- Path alias import (project uses relative imports only) ---
  if grep -nE "from[[:space:]]+['\"]@app/" "$FILE_NORM" >/dev/null 2>&1; then
    WARNINGS+="  - Path-alias import (@app/...) — this project uses relative imports only. No tsconfig path aliases.\n"
  fi

  # --- Service not in src/app/services/ ---
  case "$FILE_NORM" in
    */ui-ai-medicare-assistant/src/app/*/*.service.ts)
      case "$FILE_NORM" in
        */ui-ai-medicare-assistant/src/app/services/*) ;;
        *)
          WARNINGS+="  - Service file outside src/app/services/ — services are centralized in services/, never co-located with the component that uses them.\n"
          ;;
      esac
      ;;
  esac
fi

if [[ -n "$WARNINGS" ]]; then
  printf "Medicare Assistant standards check on %s:\n" "$FILE_NORM" >&2
  printf "%b" "$WARNINGS" >&2
  printf "Fix these before reporting the task complete. See .claude/skills/api-standards or ui-standards for full rules.\n" >&2
  exit 2
fi

exit 0
