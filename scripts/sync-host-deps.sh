#!/usr/bin/env bash
#
# Checks or syncs the host dependency list in the module packaging targets file
# against the full (direct + transitive) dependency tree of the host app.
#
# Uses a temporary project with the same PackageReferences as Desktop.csproj and
# Shared.props, then runs `dotnet list package --include-transitive` to resolve
# the complete dependency graph.
#
# Usage:
#   ./scripts/sync-host-deps.sh          # Check only (CI mode, exits 1 if out of sync)
#   ./scripts/sync-host-deps.sh --fix    # Update the targets file to match
#
set -eo pipefail

FIX=false
if [[ "${1:-}" == "--fix" ]]; then
  FIX=true
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DESKTOP_CSPROJ="$REPO_ROOT/Desktop/Desktop.csproj"
SHARED_PROPS="$REPO_ROOT/Shared.props"
TARGETS_FILE="$REPO_ROOT/ModuleBase/build/OpenShock.Desktop.ModuleBase.targets"
MODULEBASE_CSPROJ="$REPO_ROOT/ModuleBase/ModuleBase.csproj"

for f in "$DESKTOP_CSPROJ" "$SHARED_PROPS" "$TARGETS_FILE" "$MODULEBASE_CSPROJ"; do
  if [[ ! -f "$f" ]]; then
    echo "ERROR: File not found: $f" >&2
    exit 1
  fi
done

# Packages that don't produce runtime assemblies (build tools, analyzers, etc.)
SKIP_PACKAGES=(
  "AspNetCore.SassCompiler"
)

is_skipped() {
  local name="$1"
  for skip in "${SKIP_PACKAGES[@]}"; do
    [[ "$name" == "$skip" ]] && return 0
  done
  # Skip analyzers
  if [[ "$name" == *".Analyzers" || "$name" == *".Analyzer" ]]; then
    return 0
  fi
  return 1
}

# Extract PackageReference Include="name" Version="ver" and Update="name" Version="ver"
extract_package_refs() {
  local file="$1"
  grep -Eo 'PackageReference\s+(Include|Update)="[^"]+"\s+Version="[^"]+"' "$file" 2>/dev/null | \
    sed -E 's/PackageReference\s+(Include|Update)="([^"]+)"\s+Version="([^"]+)"/\2=\3/' || true
}

# Extract OpenShockHostAssembly Include="name" Version="ver"
extract_host_assemblies() {
  grep -Eo 'OpenShockHostAssembly\s+Include="[^"]+"\s+Version="[^"]+"' "$TARGETS_FILE" 2>/dev/null | \
    sed -E 's/OpenShockHostAssembly\s+Include="([^"]+)"\s+Version="([^"]+)"/\1=\2/' || true
}

# Get version from Shared.props
get_shared_version() {
  grep -Eo '<Version>[^<]+' "$SHARED_PROPS" | head -1 | sed 's/<Version>//'
}

# Collect direct PackageReferences from Desktop.csproj and Shared.props
declare -A DIRECT_REFS

while IFS='=' read -r name version; do
  [[ -z "$name" ]] && continue
  DIRECT_REFS["$name"]="$version"
done < <(extract_package_refs "$SHARED_PROPS")

while IFS='=' read -r name version; do
  [[ -z "$name" ]] && continue
  DIRECT_REFS["$name"]="$version"
done < <(extract_package_refs "$DESKTOP_CSPROJ")

while IFS='=' read -r name version; do
  [[ -z "$name" ]] && continue
  DIRECT_REFS["$name"]="$version"
done < <(extract_package_refs "$MODULEBASE_CSPROJ")

# Create a temporary project to resolve transitive dependencies.
# We can't run `dotnet list` on Desktop.csproj directly because it may
# require workloads (Maui) that aren't installed in all environments.
TMPDIR=$(mktemp -d)
trap "rm -rf '$TMPDIR'" EXIT

# Build PackageReference items for the temp project
PKG_REFS=""
for name in "${!DIRECT_REFS[@]}"; do
  version="${DIRECT_REFS[$name]}"
  # Skip platform-specific packages that need workloads
  case "$name" in
    Microsoft.Maui.*|Microsoft.AspNetCore.Components.WebView.Maui|Photino.*)
      continue ;;
  esac
  PKG_REFS="$PKG_REFS    <PackageReference Include=\"$name\" Version=\"$version\" />"$'\n'
done

cat > "$TMPDIR/HostDeps.csproj" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
$PKG_REFS  </ItemGroup>
</Project>
CSPROJ

echo "Resolving transitive dependencies..."
RESTORE_OUTPUT=$(dotnet restore "$TMPDIR/HostDeps.csproj" 2>&1) || {
  echo "ERROR: dotnet restore failed:" >&2
  echo "$RESTORE_OUTPUT" >&2
  exit 1
}

LIST_OUTPUT=$(dotnet list "$TMPDIR/HostDeps.csproj" package --include-transitive --format json 2>/dev/null) || {
  echo "ERROR: dotnet list package failed" >&2
  exit 1
}

# Parse the JSON output to build the full dependency map
declare -A HOST_DEPS

# Add ModuleBase itself
MODULEBASE_VERSION="$(get_shared_version)"
HOST_DEPS["OpenShock.Desktop.ModuleBase"]="$MODULEBASE_VERSION"

# Add platform-specific packages we skipped (they're still host-provided)
for name in "${!DIRECT_REFS[@]}"; do
  case "$name" in
    Microsoft.Maui.*|Microsoft.AspNetCore.Components.WebView.Maui|Photino.*)
      HOST_DEPS["$name"]="${DIRECT_REFS[$name]}" ;;
  esac
done

# Parse top-level and transitive packages from JSON
# Format: "id": "PackageName" followed by "resolvedVersion": "X.Y.Z"
# We use a simple line-by-line state machine to avoid needing jq
current_id=""
while IFS= read -r line; do
  if [[ "$line" =~ \"id\":[[:space:]]*\"([^\"]+)\" ]]; then
    current_id="${BASH_REMATCH[1]}"
  elif [[ "$line" =~ \"resolvedVersion\":[[:space:]]*\"([^\"]+)\" && -n "$current_id" ]]; then
    HOST_DEPS["$current_id"]="${BASH_REMATCH[1]}"
    current_id=""
  fi
done <<< "$LIST_OUTPUT"

# Collect current targets entries
declare -A TARGETS_DEPS

while IFS='=' read -r name version; do
  [[ -z "$name" ]] && continue
  TARGETS_DEPS["$name"]="$version"
done < <(extract_host_assemblies)

# Compare
declare -A MISSING
declare -A MISMATCH_OLD
declare -A MISMATCH_NEW
EXTRA=()

has_issues=false

for name in "${!HOST_DEPS[@]}"; do
  version="${HOST_DEPS[$name]}"

  is_skipped "$name" && continue

  if [[ -z "${TARGETS_DEPS[$name]+x}" ]]; then
    MISSING["$name"]="$version"
    has_issues=true
  elif [[ "${TARGETS_DEPS[$name]}" != "$version" ]]; then
    MISMATCH_OLD["$name"]="${TARGETS_DEPS[$name]}"
    MISMATCH_NEW["$name"]="$version"
    has_issues=true
  fi
done

for name in "${!TARGETS_DEPS[@]}"; do
  if [[ -z "${HOST_DEPS[$name]+x}" ]]; then
    EXTRA+=("$name=${TARGETS_DEPS[$name]}")
  fi
done

# Report
if [[ ${#MISSING[@]} -gt 0 ]]; then
  echo "Missing from targets file:"
  for name in $(echo "${!MISSING[@]}" | tr ' ' '\n' | sort); do
    echo "  + $name Version=\"${MISSING[$name]}\""
  done
fi

if [[ ${#MISMATCH_OLD[@]} -gt 0 ]]; then
  echo "Version mismatches:"
  for name in $(echo "${!MISMATCH_OLD[@]}" | tr ' ' '\n' | sort); do
    echo "  ~ $name: targets has ${MISMATCH_OLD[$name]}, Desktop has ${MISMATCH_NEW[$name]}"
  done
fi

if [[ ${#EXTRA[@]} -gt 0 ]]; then
  echo "In targets but not in Desktop (may be transitive or removed):"
  for entry in $(printf '%s\n' "${EXTRA[@]}" | sort); do
    IFS='=' read -r name version <<< "$entry"
    echo "  ? $name Version=\"$version\""
  done
fi

if [[ "$has_issues" == false ]]; then
  echo "Host dependency list is in sync."
  exit 0
fi

if [[ "$FIX" == false ]]; then
  echo ""
  echo "Run with --fix to update the targets file."
  exit 1
fi

# Apply fixes
echo ""
echo "Updating targets file..."

# Fix version mismatches
for name in "${!MISMATCH_OLD[@]}"; do
  old_ver="${MISMATCH_OLD[$name]}"
  new_ver="${MISMATCH_NEW[$name]}"
  escaped_name=$(printf '%s' "$name" | sed 's/[.[\*^$/]/\\&/g')
  escaped_old=$(printf '%s' "$old_ver" | sed 's/[.[\*^$/]/\\&/g')
  sed -i "s/\(OpenShockHostAssembly\s\+Include=\"${escaped_name}\"\s\+Version=\"\)${escaped_old}\"/\1${new_ver}\"/" "$TARGETS_FILE"
done

# Add missing entries after the last OpenShockHostAssembly Include= line
for name in $(echo "${!MISSING[@]}" | tr ' ' '\n' | sort); do
  version="${MISSING[$name]}"
  last_line=$(grep -n 'OpenShockHostAssembly Include=' "$TARGETS_FILE" | tail -1 | cut -d: -f1)
  if [[ -n "$last_line" ]]; then
    sed -i "${last_line}a\\    <OpenShockHostAssembly Include=\"${name}\" Version=\"${version}\" />" "$TARGETS_FILE"
  fi
done

echo "Done. Review the changes and commit."
