#!/bin/bash

# AIS to N2K - Secure API Key Setup Script
# This script helps you set up your AIS API key securely

echo "🔒 AIS to N2K - Secure API Key Setup"
echo "===================================="
echo

# Check if we're in a .NET project
if [ ! -f "AisToN2K.csproj" ]; then
    echo "❌ Error: This script must be run from the AisToN2K project directory"
    exit 1
fi

echo "This script will help you set up your AIS API key securely."
echo "Choose your preferred method:"
echo
echo "1. User Secrets (Recommended for Development)"
echo "   - Stored securely outside project directory"
echo "   - Never committed to source control"
echo "   - Only works in development environment"
echo
echo "2. Environment Variable (Recommended for Production)"
echo "   - Set as environment variable"
echo "   - Managed by your deployment platform"
echo "   - Works across different environments"
echo
echo "3. Show Current Configuration Status"
echo
echo "4. Exit"
echo

read -p "Enter your choice (1-4): " choice

case $choice in
    1)
        echo
        echo "📝 Setting up User Secrets..."
        echo
        
        # Check if user secrets are already initialized
        if ! dotnet user-secrets list > /dev/null 2>&1; then
            echo "Initializing user secrets..."
            dotnet user-secrets init
        fi
        
        read -p "Enter your AIS API key: " -s api_key
        echo
        
        if [ -z "$api_key" ]; then
            echo "❌ Error: API key cannot be empty"
            exit 1
        fi
        
        # Set the user secret
        dotnet user-secrets set "AisApi:ApiKey" "$api_key"
        
        if [ $? -eq 0 ]; then
            echo "✅ API key successfully stored in User Secrets!"
            echo "💡 You can now run the application with: dotnet run"
        else
            echo "❌ Error: Failed to store API key in User Secrets"
            exit 1
        fi
        ;;
        
    2)
        echo
        echo "🌍 Setting up Environment Variable..."
        echo
        
        read -p "Enter your AIS API key: " -s api_key
        echo
        
        if [ -z "$api_key" ]; then
            echo "❌ Error: API key cannot be empty"
            exit 1
        fi
        
        # Determine shell type and provide appropriate instructions
        if [ -n "$ZSH_VERSION" ]; then
            shell_config="~/.zshrc"
        elif [ -n "$BASH_VERSION" ]; then
            shell_config="~/.bashrc"
        else
            shell_config="your shell configuration file"
        fi
        
        echo "Add this line to your $shell_config:"
        echo
        echo "export AIS_API_KEY=\"$api_key\""
        echo
        echo "Or run this command for the current session:"
        echo "export AIS_API_KEY=\"$api_key\""
        echo
        
        # Set for current session
        export AIS_API_KEY="$api_key"
        echo "✅ Environment variable set for current session"
        echo "💡 Don't forget to add it to your shell configuration for persistence"
        ;;
        
    3)
        echo
        echo "📋 Current Configuration Status:"
        echo
        dotnet run --no-build -- --check-config 2>/dev/null || echo "Run 'dotnet run' to see configuration status"
        ;;
        
    4)
        echo "👋 Goodbye!"
        exit 0
        ;;
        
    *)
        echo "❌ Invalid choice. Please run the script again and select 1-4."
        exit 1
        ;;
esac

echo
echo "🎉 Setup complete! You can now run the application safely."
echo "💡 Remember: Never commit API keys to source control!"
echo
