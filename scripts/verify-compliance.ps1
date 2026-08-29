<#
.SYNOPSIS
    Architecture & Quality Standards Compliance Verification Script for EricksonLopez.Transaction.
.DESCRIPTION
    Validates architectural invariants:
    1. Kebab-case naming for all markdown documentation (allowing canonical reserved files).
    2. Zero [Obsolete] usages in production code (src/).
    3. Presence of canonical MIT copyright header across all source files.
    4. Single top-level type per file in src/.
    5. Valid GitHub repository links referencing ericksonlopezf/dotnet-transaction and project URL.
    6. Official support and security email normalization (ericksonlopezf@gmail.com).
    7. Zero prohibited <NoWarn> suppressions across all projects.
    8. NuGet package metadata consistency (PackageIcon, PackageReadmeFile, TreatWarningsAsErrors).
    9. Roslyn Analyzer configuration in .editorconfig.
#>

[CmdletBinding()]
param (
    [string]$RootDirectory = "."
)

$ErrorActionPreference = "Stop"
$violations = 0

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  REPOSITORY COMPLIANCE & ARCHITECTURE AUDITOR    " -ForegroundColor Cyan
Write-Host "  Repository: EricksonLopez.Transaction           " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Kebab-case documentation verification across entire repo
Write-Host "`n[1/9] Checking documentation file naming (kebab-case)..." -ForegroundColor Yellow
$reservedNames = @(
    "README.md", "CHANGELOG.md", "CODE_OF_CONDUCT.md", "CONTRIBUTING.md",
    "GOVERNANCE.md", "SECURITY.md", "SUPPORT.md", "PULL_REQUEST_TEMPLATE.md"
)
$allMdFiles = Get-ChildItem -Path $RootDirectory -Recurse -Filter "*.md" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\(obj|bin|TestResults|\.git)\\" }
$badDocNames = 0
if ($allMdFiles) {
    foreach ($doc in $allMdFiles) {
        $filename = $doc.Name
        # Allow reserved standard GitHub files and issue templates
        if ($reservedNames -contains $filename -or $doc.FullName -match "\\\.github\\ISSUE_TEMPLATE\\") {
            continue
        }
        if ($filename -cne $filename.ToLower() -or $filename -match "_") {
            Write-Host "  ❌ Non-kebab-case document: $($doc.FullName)" -ForegroundColor Red
            $violations++
            $badDocNames++
        }
    }
}
if ($badDocNames -eq 0) { Write-Host "  ✅ All documentation files use valid kebab-case naming." -ForegroundColor Green }

# 2. Zero Obsolete APIs in src/
Write-Host "`n[2/9] Checking for [Obsolete] attribute usages in src/..." -ForegroundColor Yellow
$srcCsFiles = Get-ChildItem -Path (Join-Path $RootDirectory "src") -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" }
$obsoleteCount = 0
if ($srcCsFiles) {
    foreach ($cs in $srcCsFiles) {
        $lines = Get-Content $cs.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^\s*\[Obsolete\b" -and $lines[$i] -notmatch "^\s*//") {
                Write-Host "  ❌ [Obsolete] found in $($cs.FullName):$($i + 1)" -ForegroundColor Red
                $violations++
                $obsoleteCount++
            }
        }
    }
}
if ($obsoleteCount -eq 0) { Write-Host "  ✅ Zero [Obsolete] attributes in production code." -ForegroundColor Green }

# 3. Canonical MIT Copyright Header
Write-Host "`n[3/9] Checking canonical MIT copyright headers..." -ForegroundColor Yellow
$missingHeaderCount = 0
$allCsFiles = Get-ChildItem -Path $RootDirectory -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" }
if ($allCsFiles) {
    foreach ($cs in $allCsFiles) {
        $firstLine = (Get-Content $cs.FullName -TotalCount 1)
        if ($firstLine -notmatch "^// Copyright © Erickson Lopez\. MIT License\.") {
            Write-Host "  ❌ Missing canonical copyright header in: $($cs.FullName)" -ForegroundColor Red
            $violations++
            $missingHeaderCount++
        }
    }
}
if ($missingHeaderCount -eq 0) { Write-Host "  ✅ All production C# files contain the required MIT copyright header." -ForegroundColor Green }

# 4. One Type Per File Invariant
Write-Host "`n[4/9] Checking 'One Type Per File' rule in src/..." -ForegroundColor Yellow
$multiTypeCount = 0
if ($srcCsFiles) {
    foreach ($cs in $srcCsFiles) {
        $lines = Get-Content $cs.FullName | Where-Object { $_ -notmatch "^\s*//" }
        $typeDeclarations = $lines | Where-Object { $_ -match "^\s*(public|internal|private|protected)?\s*(sealed|abstract|static|readonly)?\s*(class|struct|record|interface|enum)\s+[A-Za-z0-9_]+" }
        if (@($typeDeclarations).Count -gt 1) {
            $hasMultipleTopLevels = ($typeDeclarations | Where-Object { $_ -notmatch "^\s{4,}" }).Count -gt 1
            if ($hasMultipleTopLevels) {
                Write-Host "  ❌ Multiple types declared in: $($cs.FullName)" -ForegroundColor Red
                $violations++
                $multiTypeCount++
            }
        }
    }
}
if ($multiTypeCount -eq 0) { Write-Host "  ✅ Every production file satisfies the 'One Type Per File' invariant." -ForegroundColor Green }

# 5. GitHub Repository Identity & Project URL
Write-Host "`n[5/9] Checking GitHub identity links (ericksonlopezf/dotnet-transaction)..." -ForegroundColor Yellow
$wrongRepoLinks = 0
$propsPath = Join-Path $RootDirectory "Directory.Build.props"
if (Test-Path $propsPath) {
    $propsContent = Get-Content $propsPath -Raw
    if ($propsContent -notmatch "ericksonlopezf/dotnet-transaction") {
        Write-Host "  ❌ Directory.Build.props does not reference ericksonlopezf/dotnet-transaction" -ForegroundColor Red
        $violations++
        $wrongRepoLinks++
    }
    if ($propsContent -notmatch "https://ericksonlopez\.dev/transaction") {
        Write-Host "  ❌ Directory.Build.props does not reference https://ericksonlopez.dev/transaction" -ForegroundColor Red
        $violations++
        $wrongRepoLinks++
    }
}
if ($wrongRepoLinks -eq 0) { Write-Host "  ✅ All GitHub & project URLs correctly target ericksonlopezf/dotnet-transaction." -ForegroundColor Green }

# 6. Normalized Support/Security Contact Email
Write-Host "`n[6/9] Checking contact and security email normalization (ericksonlopezf@gmail.com)..." -ForegroundColor Yellow
$wrongEmailCount = 0
$secDoc = Join-Path $RootDirectory "SECURITY.md"
if (Test-Path $secDoc) {
    $secContent = Get-Content $secDoc -Raw
    if ($secContent -notmatch "ericksonlopezf@gmail\.com") {
        Write-Host "  ❌ SECURITY.md does not reference canonical email ericksonlopezf@gmail.com" -ForegroundColor Red
        $violations++
        $wrongEmailCount++
    }
}
if ($wrongEmailCount -eq 0) { Write-Host "  ✅ Official contact emails normalized to ericksonlopezf@gmail.com." -ForegroundColor Green }

# 7. Zero Prohibited NoWarn Suppressions
Write-Host "`n[7/9] Checking prohibited <NoWarn> suppressions..." -ForegroundColor Yellow
$badNoWarnCount = 0
$allCsproj = Get-ChildItem -Path $RootDirectory -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\(obj|bin)\\" }
if ($allCsproj) {
    foreach ($proj in $allCsproj) {
        $content = Get-Content $proj.FullName -Raw
        if ($content -match "<NoWarn>(?!.*\$\(NoWarn\)).*</NoWarn>" -or $content -match "<NoWarn>.*(CS1591|1591|CS0618|CS0619).*</NoWarn>") {
            Write-Host "  ❌ Prohibited <NoWarn> found in $($proj.FullName)" -ForegroundColor Red
            $violations++
            $badNoWarnCount++
        }
    }
}
if ($badNoWarnCount -eq 0) { Write-Host "  ✅ Zero prohibited <NoWarn> diagnostic suppressions in project files." -ForegroundColor Green }

# 8. NuGet Package Metadata & Compiler Settings
Write-Host "`n[8/9] Checking package metadata and compiler quality settings..." -ForegroundColor Yellow
$badMetadata = 0
if (Test-Path $propsPath) {
    $propsContent = Get-Content $propsPath -Raw
    if ($propsContent -notmatch "<PackageIcon>icon\.png</PackageIcon>") {
        Write-Host "  ❌ Directory.Build.props missing <PackageIcon>icon.png</PackageIcon>" -ForegroundColor Red
        $violations++
        $badMetadata++
    }
    if ($propsContent -notmatch "<PackageReadmeFile>README\.md</PackageReadmeFile>") {
        Write-Host "  ❌ Directory.Build.props missing <PackageReadmeFile>README.md</PackageReadmeFile>" -ForegroundColor Red
        $violations++
        $badMetadata++
    }
    if ($propsContent -notmatch "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>") {
        Write-Host "  ❌ Directory.Build.props missing <TreatWarningsAsErrors>true</TreatWarningsAsErrors>" -ForegroundColor Red
        $violations++
        $badMetadata++
    }
}
if ($badMetadata -eq 0) { Write-Host "  ✅ Package metadata, icon packaging, and TreatWarningsAsErrors verified." -ForegroundColor Green }

# 9. Analyzer Matrix Configuration (.editorconfig)
Write-Host "`n[9/9] Checking mandatory analyzer severities in .editorconfig..." -ForegroundColor Yellow
$editorConfigPath = Join-Path $RootDirectory ".editorconfig"
$badAnalyzers = 0
if (Test-Path $editorConfigPath) {
    $ec = Get-Content $editorConfigPath -Raw
    $requiredRules = @("CS0618", "CS0619", "CS1591", "CS0159", "IDE1006", "CA1707", "CA1852", "CA1305", "xUnit1051")
    foreach ($rule in $requiredRules) {
        if ($ec -notmatch "dotnet_diagnostic\.$rule\.severity") {
            Write-Host "  ❌ Missing diagnostic rule $rule in .editorconfig" -ForegroundColor Red
            $violations++
            $badAnalyzers++
        }
    }
} else {
    Write-Host "  ❌ .editorconfig file not found!" -ForegroundColor Red
    $violations++
    $badAnalyzers++
}
if ($badAnalyzers -eq 0) { Write-Host "  ✅ All mandatory analyzer diagnostics explicitly mapped in .editorconfig." -ForegroundColor Green }

Write-Host "`n==================================================" -ForegroundColor Cyan
if ($violations -eq 0) {
    Write-Host "  SUCCESS: 100% Governance & Compliance Verified. Zero violations. " -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "  FAILED: $violations compliance violation(s) detected. " -ForegroundColor Red
    Write-Host "==================================================" -ForegroundColor Cyan
    exit 1
}
