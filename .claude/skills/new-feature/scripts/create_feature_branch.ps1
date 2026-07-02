# Create a new feature branch off integration
# Usage: ./create_feature_branch.ps1 [-featureName "name"]

param(
    [string]$featureName
)

# If feature name not provided, prompt for it
if (-not $featureName) {
    Write-Host ""
    Write-Host "=== New Feature Branch ===" -ForegroundColor Cyan
    Write-Host ""
    $featureName = Read-Host "Enter feature name (e.g., add-login-tests)"
}

# Validate feature name
if ([string]::IsNullOrWhiteSpace($featureName)) {
    Write-Host "Error: Feature name cannot be empty" -ForegroundColor Red
    exit 1
}

# Check if feature name is valid (alphanumeric and hyphens only)
if ($featureName -notmatch '^[a-z0-9-]+$') {
    Write-Host "Error: Feature name must be lowercase alphanumeric with hyphens (e.g., add-login-tests)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Fetching latest integration branch..." -ForegroundColor Yellow

# Fetch latest integration
git fetch origin integration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to fetch integration branch" -ForegroundColor Red
    exit 1
}

Write-Host "Creating feature branch: feature/$featureName" -ForegroundColor Yellow

# Create and checkout feature branch off integration
git checkout -b feature/$featureName origin/integration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to create feature branch" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Feature branch created and checked out!" -ForegroundColor Green
Write-Host ""
Write-Host "Branch: feature/$featureName" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Make your changes and commit: git commit -m 'your message'"
Write-Host "2. Push to GitHub: git push origin feature/$featureName"
Write-Host "3. Create a PR from feature/$featureName → integration"
Write-Host ""
