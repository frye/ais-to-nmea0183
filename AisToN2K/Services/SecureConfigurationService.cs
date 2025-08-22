using Microsoft.Extensions.Configuration;

namespace AisToN2K.Services
{
    public class SecureConfigurationService
    {
        private readonly IConfiguration _configuration;
        
        public SecureConfigurationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        /// <summary>
        /// Gets API key from the most secure available source
        /// Priority: User Secrets > Environment Variables > Configuration File
        /// </summary>
        public string? GetApiKey()
        {
            // Try User Secrets first (most secure for development)
            var userSecretKey = _configuration["AisApi:ApiKey"];
            if (!string.IsNullOrEmpty(userSecretKey) && userSecretKey != "YOUR_API_KEY_HERE")
            {
                return userSecretKey;
            }
            
            // Try environment variable (good for production)
            var envKey = Environment.GetEnvironmentVariable("AIS_API_KEY") 
                        ?? Environment.GetEnvironmentVariable("AisApi__ApiKey");
            if (!string.IsNullOrEmpty(envKey))
            {
                return envKey;
            }
            
            return null;
        }
        
        /// <summary>
        /// Validates that we have a secure API key configuration
        /// </summary>
        public (bool IsValid, string ErrorMessage, string RecommendedAction) ValidateApiKeyConfiguration()
        {
            var apiKey = GetApiKey();
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return (false, 
                    "No API key found in any secure location", 
                    "Set API key using User Secrets or Environment Variable");
            }
            
            if (apiKey == "YOUR_API_KEY_HERE" || apiKey.Length < 10)
            {
                return (false, 
                    "API key appears to be a placeholder or too short", 
                    "Set a valid API key using User Secrets or Environment Variable");
            }
            
            // Check if API key is stored in appsettings.json (not recommended)
            var configFileKey = _configuration.GetSection("AisApi:ApiKey").Value;
            if (!string.IsNullOrEmpty(configFileKey) && configFileKey == apiKey)
            {
                return (true, 
                    "Warning: API key is stored in appsettings.json (not recommended for production)", 
                    "Consider moving to User Secrets for development or Environment Variables for production");
            }
            
            return (true, "API key configuration is secure", "");
        }
        
        /// <summary>
        /// Provides instructions for setting up secure API key storage
        /// </summary>
        public void PrintSecurityInstructions()
        {
            Console.WriteLine("\n=== API Key Security Instructions ===");
            Console.WriteLine();
            Console.WriteLine("🔒 RECOMMENDED METHODS (in order of preference):");
            Console.WriteLine();
            Console.WriteLine("1. USER SECRETS (Development):");
            Console.WriteLine("   dotnet user-secrets set \"AisApi:ApiKey\" \"your-actual-api-key\"");
            Console.WriteLine("   • Stored securely outside project directory");
            Console.WriteLine("   • Never committed to source control");
            Console.WriteLine("   • Only works in development environment");
            Console.WriteLine();
            Console.WriteLine("2. ENVIRONMENT VARIABLES (Production):");
            Console.WriteLine("   Linux/macOS: export AIS_API_KEY=\"your-actual-api-key\"");
            Console.WriteLine("   Windows: set AIS_API_KEY=your-actual-api-key");
            Console.WriteLine("   Docker: -e AIS_API_KEY=\"your-actual-api-key\"");
            Console.WriteLine("   • Secure for production deployments");
            Console.WriteLine("   • Managed by deployment platform");
            Console.WriteLine();
            Console.WriteLine("❌ NOT RECOMMENDED:");
            Console.WriteLine("3. Configuration File (appsettings.json):");
            Console.WriteLine("   • Risk of committing secrets to source control");
            Console.WriteLine("   • Only use for non-sensitive default values");
            Console.WriteLine();
            Console.WriteLine("=== Current Status ===");
            
            var (isValid, message, action) = ValidateApiKeyConfiguration();
            Console.WriteLine($"Status: {(isValid ? "✅" : "❌")} {message}");
            if (!string.IsNullOrEmpty(action))
            {
                Console.WriteLine($"Action: {action}");
            }
            Console.WriteLine();
        }
    }
}
