# AIS to NMEA 0183 Converter

A .NET Core console application that fetches real-time AIS (Automatic Identification System) data from APIs and displays vessel information with geographic filtering.

## Overview

This application implements a simplified real-time AIS monitoring system based on the [MVP plan](https://github.com/frye/AIS-API-to-N2K/blob/main/MVP_plan.md), focusing on WebSocket streaming with geographic bounding box filtering.

## Features

- **Real-time AIS Streaming**: WebSocket connection to AIS Stream for live vessel data
- **Geographic Filtering**: Configurable bounding box for area-specific monitoring
- **Secure Configuration**: Multiple secure methods for API key storage
- **Real-time Display**: Live vessel data including position, speed, course, and heading

## Architecture

### Core Components

- **AisWebSocketService**: Real-time WebSocket streaming with geographic filtering
- **SecureConfigurationService**: Secure API key management
- **Nmea0183Converter**: Converts AIS data to NMEA 0183 format
- **TcpServer/UdpServer**: Network broadcasting for marine navigation software
- **Models**: Data structures for AIS vessel data

## Prerequisites

- .NET 9.0 or later
- AIS Stream API access (API key required)

## Installation

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd ais-to-n2k-net
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

## Configuration

⚠️ **IMPORTANT**: Never store API keys directly in `appsettings.json` for production use!

### Quick Setup (Recommended)

Use the provided setup scripts to configure your API key securely:

**Linux/macOS:**
```bash
./setup-apikey.sh
```

**Windows (PowerShell):**
```powershell
.\setup-apikey.ps1
```

### Manual Configuration

Update `appsettings.json` with your basic settings:

```json
{
  "ApiKey": "",
  "WebSocketUrl": "wss://stream.aisstream.io/v0/stream",
  "BoundingBox": {
    "North": 48.8000,
    "South": 48.0000,
    "East": -122.1900,
    "West": -123.3550
  },
  "Network": {
    "EnableTcp": true,
    "EnableUdp": true,
    "Tcp": {
      "Host": "0.0.0.0",
      "Port": 2000
    },
    "Udp": {
      "Host": "127.0.0.1",
      "Port": 2001
    }
  }
}
```

### 🔒 Secure API Key Configuration

The application supports multiple secure methods for storing your API key:

#### 1. User Secrets (Development - Recommended)
```bash
dotnet user-secrets set "AisApi:ApiKey" "your-actual-api-key"
```
- ✅ Stored outside project directory
- ✅ Never committed to source control
- ✅ Perfect for local development

#### 2. Environment Variables (Production - Recommended)
```bash
# Linux/macOS
export AIS_API_KEY="your-actual-api-key"

# Windows
set AIS_API_KEY=your-actual-api-key

# Docker
docker run -e AIS_API_KEY="your-actual-api-key" your-image
```
- ✅ Secure for production
- ✅ Managed by deployment platform
- ✅ Works across environments

#### 3. Configuration File (Not Recommended)
Only use `appsettings.json` for non-sensitive defaults.
- ❌ Risk of committing secrets
- ❌ Not suitable for production

### Configuration Options

- **ApiKey**: Your AIS Stream API key (use secure methods above)
- **WebSocketUrl**: WebSocket URL for real-time streaming
- **BoundingBox**: Geographic bounding box for WebSocket filtering
  - **North/South**: Latitude boundaries
  - **East/West**: Longitude boundaries
- **Network**: TCP/UDP server configuration
  - **EnableTcp/EnableUdp**: Enable network servers
  - **Tcp.Host/Port**: TCP server binding (default: 0.0.0.0:2000)
  - **Udp.Host/Port**: UDP broadcast target (default: 127.0.0.1:2001)
- **ApplicationLogging**: Detailed logging configuration
  - **EnableDetailedLogging**: Verbose output
  - **LogNmeaMessages**: Show NMEA sentence output
  - **StatisticsReportingIntervalMinutes**: Statistics frequency

## Usage

### First Time Setup

1. **Configure API Key Securely:**
   ```bash
   # Use the setup script (recommended)
   ./setup-apikey.sh
   
   # Or manually with User Secrets
   dotnet user-secrets set "AisApi:ApiKey" "your-actual-api-key"
   ```

2. **Run the Application:**
   ```bash
   dotnet run
   ```

### Real-time WebSocket Mode (Default)

The application automatically connects to the AIS Stream WebSocket for real-time vessel data.

**Features:**
- ✅ Real-time vessel position updates
- ✅ Geographic bounding box filtering
- ✅ Low latency data processing
- ✅ Automatic reconnection on failure
- ✅ Event-driven processing

- ✅ Low latency data processing
- ✅ Automatic reconnection on failure
- ✅ Event-driven processing

## Data Processing

The application processes AIS data in real-time, converting it to NMEA 0183 format and broadcasting via TCP/UDP servers for marine navigation software integration.

**Key Processing Features:**
- ✅ Real-time AIS to NMEA 0183 conversion
- ✅ TCP server for multiple client connections (port 2000)
- ✅ UDP broadcasting for simple integration (port 2001)
- ✅ Comprehensive statistics and logging
- ✅ Message type classification and tracking

## Usage

### Simulation Mode

The application runs in simulation mode and outputs NMEA 0183 messages to the console and broadcasts them via TCP/UDP servers.

### Production Mode

With marine navigation software:
1. Connect to the TCP server (default port 2002) or UDP broadcast (default port 2003)
2. Configure your navigation software to receive NMEA 0183 data from these ports
3. The application will stream live AIS data converted to NMEA 0183 format

      }
    }
  }
}
```

This example shows the coordinates for New York Harbor.

## Development

### Project Structure

```
├── Configuration/          # Configuration models
├── Models/                 # Data models for AIS
├── Services/              # Core business logic
│   ├── AisWebSocketService.cs # Real-time WebSocket streaming
│   ├── Nmea0183Converter.cs   # AIS to NMEA 0183 conversion
│   ├── TcpServer.cs           # TCP server for client connections
│   ├── UdpServer.cs           # UDP broadcasting service
│   ├── StatisticsService.cs   # Metrics and logging
│   └── SecureConfigurationService.cs # Secure API key management
├── Program.cs             # Main application entry point
└── appsettings.json       # Configuration file
```

### Adding New Features

1. **New AIS Data Fields**: Update `AisData` model in Models/
2. **NMEA Conversion**: Extend `Nmea0183Converter` for additional message types
3. **Additional Filtering**: Extend WebSocket subscription logic

## Troubleshooting

### Common Issues

1. **API Key Error**: Ensure your AIS Stream API key is valid and has sufficient quota
2. **WebSocket Connection Failed**: Check internet connectivity and API key validity
3. **No Vessels Found**: Verify bounding box coordinates cover an active shipping area

### Security Issues

1. **API Key Not Found**: Run `./setup-apikey.sh` or use `dotnet user-secrets` to set your key
2. **Configuration Warnings**: The app will warn if your API key is stored insecurely
3. **Permission Errors**: Ensure proper file permissions for User Secrets

### Logging

The application provides console output for:
- Configuration loading status and security warnings
- WebSocket connection status
- Real-time vessel data processing
- Error messages and warnings

## Security Best Practices

🔒 **Follow these security guidelines:**

1. **Never commit API keys** to source control
2. **Use User Secrets** for local development
3. **Use Environment Variables** for production deployments
4. **Rotate API keys** regularly
5. **Monitor API usage** for unauthorized access
6. **Use WSS/HTTPS** for all API communications (enabled by default)
7. **Limit API key permissions** to minimum required scope

### Security Features

- ✅ **Multi-source configuration**: User Secrets → Environment Variables → Config File
- ✅ **API key masking**: Keys are masked in logs and console output
- ✅ **Configuration validation**: Warns about insecure storage methods
- ✅ **Setup scripts**: Guided secure configuration
- ✅ **No default keys**: Empty API key in configuration file

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

[Add your license information here]

## Acknowledgments

Based on the MVP plan from the [AIS-API-to-N2K](https://github.com/frye/AIS-API-to-N2K) project.
