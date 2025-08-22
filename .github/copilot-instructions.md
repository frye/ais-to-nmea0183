# AIS to NMEA 0183 Converter - Copilot Instructions

This is a .NET 9.0 console application that converts real-time AIS (Automatic Identification System) data from maritime APIs to NMEA 0183 format for marine navigation systems like OpenCPN.

## 🚢 Project Overview

A high-performance maritime data converter that streams vessel position and static data from AIS Stream's WebSocket API, converts it to NMEA 0183 messages, and broadcasts to navigation software via TCP/UDP. Supports vessel positioning (Type 1, 18), static data (Type 5), and vessel type information (Type 24 Part A/B) with precise coordinate encoding.

## 📁 Project Architecture

```
├── Configuration/           # Application configuration models
│   └── AppConfig.cs        # Main configuration class with validation
├── Models/                 # AIS data and NMEA message models
│   └── AisData.cs         # AIS message structure definitions
├── Services/              # Core business logic services
│   ├── AisWebSocketService.cs     # Real-time AIS data streaming
│   ├── Nmea0183Converter.cs       # AIS to NMEA conversion engine
│   ├── SecureConfigurationService.cs # API key management
│   ├── StatisticsService.cs       # Performance monitoring
│   ├── TcpServer.cs              # TCP server for OpenCPN
│   └── UdpServer.cs              # UDP broadcast server
├── Program.cs             # Application entry point with command line parsing
├── appsettings.json       # Configuration file
└── README.md             # Project documentation
```

## 🎯 Key Features & Capabilities

### **Real-time Data Processing**
- **WebSocket streaming** from AIS Stream API with automatic reconnection
- **Geographic bounding box filtering** for targeted vessel monitoring
- **Multi-message type support**: Position Reports (Type 1, 18), Static Data (Type 5), Vessel Type Data (Type 24 A/B)
- **Precise coordinate encoding** with validation and error detection

### **Network Communication**
- **TCP server** (port 2002) for OpenCPN and marine software integration
- **UDP broadcast** (port 2003) for multiple client support
- **Dual-mode operation**: Primary WebSocket + HTTP polling fallback
- **Error resilience** with automatic retry and graceful degradation

### **Security & Configuration**
- **Secure API key management** via User Secrets and Environment Variables
- **JSON-based configuration** with runtime validation
- **Configurable bounding boxes** for different geographic regions
- **Pacific Northwest region** preconfigured for testing

## 🔧 Development Guidelines

### **Command Line Interface**
- **Normal Mode**: `dotnet run` - Clean output with progress indicators only
- **Debug Mode**: `dotnet run -- --debug` - Detailed technical logging
- **Help**: `dotnet run -- --help` - Display usage information

### **Debug Output Rules** ⚠️ CRITICAL
- **NO debug information** should EVER be logged to console without the `--debug` flag
- All diagnostic output must be wrapped in `if (_debugMode)` conditionals
- Normal mode shows ONLY: startup info, configuration summary, and progress indicators (`📊 Processed X messages`)
- Debug mode shows: coordinate conversion details, JSON parsing, NMEA generation, TCP broadcasts

### **.NET Best Practices**
- **Target Framework**: .NET 9.0 for latest performance optimizations
- **Async/Await**: Use throughout for I/O operations (WebSocket, TCP, UDP)
- **Dependency Injection**: Constructor injection for all services
- **Configuration**: Use `IConfiguration` and Options pattern
- **Logging**: Implement conditional logging based on debug flag
- **Error Handling**: Comprehensive try-catch with specific exception types
- **Resource Disposal**: Proper `using` statements and `IDisposable` implementation

### **Code Architecture Patterns**
- **Service Layer**: Separate concerns (WebSocket, Conversion, Network)
- **Single Responsibility**: Each service has one clear purpose  
- **Immutable Models**: AIS data structures should be readonly where possible
- **Validation**: Input validation for all external data (API responses, config)
- **Separation of Concerns**: Clear boundaries between data access, business logic, and presentation

### **Testing & Debugging**
- **Unit Tests**: Test coordinate conversion algorithms independently
- **Integration Tests**: Verify NMEA message format compliance
- **Debug Mode**: Use `--debug` flag for troubleshooting coordinate issues
- **Performance Monitoring**: Built-in statistics service tracks message rates
- **Error Logging**: Capture and report WebSocket disconnections and API errors

### **NMEA 0183 Compliance**
- **Message Format**: Strict adherence to NMEA 0183 standard
- **Checksum Validation**: All messages include proper checksums
- **Coordinate Encoding**: Use proper scaling factors for latitude/longitude
- **Type 24 Support**: Implement both Part A (vessel name) and Part B (vessel type/dimensions)
- **OpenCPN Compatibility**: Test with actual navigation software

### **Configuration Management**
- **appsettings.json**: Default configuration and bounding boxes
- **User Secrets**: API keys for development (`dotnet user-secrets set "AisStream:ApiKey" "your-key"`)
- **Environment Variables**: Production API key management
- **Validation**: Runtime validation of all configuration values

## 🚀 Quick Start Commands

```bash
# Development setup
dotnet user-secrets set "AisStream:ApiKey" "your-api-key"

# Normal operation (clean output)
dotnet run

# Debug mode (detailed logging)
dotnet run -- --debug

# Build and test
dotnet build
dotnet test
```

## 📊 Performance Expectations

- **Message Rate**: 30-50 AIS messages per minute in Pacific Northwest region
- **Latency**: <100ms from AIS reception to NMEA broadcast
- **Memory Usage**: <50MB typical, <100MB peak
- **Network**: TCP connections for precise delivery, UDP for broadcast
- **Reliability**: Automatic reconnection on WebSocket failures

## 🔍 Debugging Guidelines

When working on this codebase:

1. **Always use `--debug` flag** when investigating issues
2. **Check coordinate conversion** first for positioning problems  
3. **Verify NMEA checksums** for message format issues
4. **Monitor WebSocket connection** for data flow problems
5. **Test with OpenCPN** for end-to-end validation

## 🛡️ Security Considerations

- **API Keys**: Never commit API keys to version control
- **Network Binding**: Default TCP server binds to all interfaces (0.0.0.0)
- **Input Validation**: All external data is validated before processing
- **Error Messages**: Debug information excluded from normal output to prevent information leakage
