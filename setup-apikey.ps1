# AIS to N2K - Secure API Key Setup Script (PowerShell)
# This script helps you set up your AIS API key securely on Windows

Write-Host "🔒 AIS to N2K - Secure API Key Setup" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host

# Check if we're in a .NET project
if (-not (Test-Path "AisToN2K.csproj")) {
    Write-Host "❌ Error: This script must be run from the AisToN2K project directory" -ForegroundColor Red
    exit 1
}

Write-Host "This script will help you set up your AIS API key securely."
Write-Host "Choose your preferred method:" -ForegroundColor Yellow
Write-Host
Write-Host "1. User Secrets (Recommended for Development)"
Write-Host "   - Stored securely outside project directory"
Write-Host "   - Never committed to source control"
Write-Host "   - Only works in development environment"
Write-Host
Write-Host "2. Environment Variable (Recommended for Production)"
Write-Host "   - Set as environment variable"
Write-Host "   - Managed by your deployment platform"
Write-Host "   - Works across different environments"
Write-Host
Write-Host "3. Show Current Configuration Status"
Write-Host
Write-Host "4. Exit"
Write-Host

$choice = Read-Host "Enter your choice (1-4)"

switch ($choice) {
    1 {
        Write-Host
        Write-Host "📝 Setting up User Secrets..." -ForegroundColor Green
        Write-Host
        
        # Check if user secrets are already initialized
        $secretsList = dotnet user-secrets list 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Initializing user secrets..."
            dotnet user-secrets init
        }
        
        $apiKey = Read-Host "Enter your AIS API key" -MaskInput
        
        if ([string]::IsNullOrWhiteSpace($apiKey)) {
            Write-Host "❌ Error: API key cannot be empty" -ForegroundColor Red
            exit 1
        }
        
        # Set the user secret
        dotnet user-secrets set "AisApi:ApiKey" $apiKey
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ API key successfully stored in User Secrets!" -ForegroundColor Green
            Write-Host "💡 You can now run the application with: dotnet run" -ForegroundColor Blue
        } else {
            Write-Host "❌ Error: Failed to store API key in User Secrets" -ForegroundColor Red
            exit 1
        }
    }
    
    2 {
        Write-Host
        Write-Host "🌍 Setting up Environment Variable..." -ForegroundColor Green
        Write-Host
        
        $apiKey = Read-Host "Enter your AIS API key" -MaskInput
        
        if ([string]::IsNullOrWhiteSpace($apiKey)) {
            Write-Host "❌ Error: API key cannot be empty" -ForegroundColor Red
            exit 1
        }
        
        # Set environment variable for current session
        $env:AIS_API_KEY = $apiKey
        
        Write-Host "To make this permanent, add this to your system environment variables:"
        Write-Host "Variable Name: AIS_API_KEY"
        Write-Host "Variable Value: [your-api-key]"
        Write-Host
        Write-Host "Or use PowerShell to set it permanently:"
        Write-Host "[Environment]::SetEnvironmentVariable('AIS_API_KEY', '$apiKey', 'User')" -ForegroundColor Blue
        Write-Host
        Write-Host "✅ Environment variable set for current session" -ForegroundColor Green
        Write-Host "💡 Don't forget to set it permanently for persistence" -ForegroundColor Blue
    }
    
    3 {
        Write-Host
        Write-Host "📋 Current Configuration Status:" -ForegroundColor Yellow
        Write-Host
        try {
            dotnet run --no-build -- --check-config 2>$null
        } catch {
            Write-Host "Run 'dotnet run' to see configuration status"
        }
    }
    
    4 {
        Write-Host "👋 Goodbye!" -ForegroundColor Green
        exit 0
    }
    
    default {
        Write-Host "❌ Invalid choice. Please run the script again and select 1-4." -ForegroundColor Red
        exit 1
    }
}

Write-Host
Write-Host "🎉 Setup complete! You can now run the application safely." -ForegroundColor Green
Write-Host "💡 Remember: Never commit API keys to source control!" -ForegroundColor Yellow
