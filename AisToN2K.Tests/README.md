# AIS to NMEA 0183 Converter - Test Suite

This test suite provides comprehensive coverage for the AIS-to-NMEA0183 converter application, ensuring reliability, performance, and compatibility with marine navigation systems like OpenCPN.

## 🧪 Test Structure

### **Test Organization**
```
AisToN2K.Tests/
├── TestData/                    # Realistic test data and samples
│   └── AisTestData.cs          # AIS message samples, coordinate test cases
├── Utilities/                   # Test helper classes and validation utilities
│   ├── NmeaValidator.cs        # NMEA 0183 validation and checksum utilities
│   └── CoordinateTestHelper.cs # Coordinate conversion and validation helpers
├── Unit/                       # Unit tests for individual components
│   ├── AisJsonParsingTests.cs  # AIS JSON parsing and validation
│   ├── Nmea0183ConversionTests.cs # NMEA format conversion and validation
│   ├── CoordinateConversionTests.cs # Geographic coordinate transformations
│   └── ConfigurationTests.cs   # Application configuration and security
├── Integration/                # Integration tests for component interactions
│   ├── AisToNmeaPipelineTests.cs # End-to-end conversion pipeline
│   └── NetworkServicesTests.cs # TCP/UDP network communication
└── Performance/                # Performance and scalability tests
    └── PerformanceTests.cs     # Throughput, memory, and real-time performance
```

## 🎯 Test Coverage Areas

### **1. AIS JSON Parsing Tests** (`Unit/AisJsonParsingTests.cs`)
- **Type 1 Position Reports (Class A)**: Vessel position, speed, course, heading
- **Type 5 Static and Voyage Data**: Vessel name, type, dimensions, destination
- **Type 18 Position Reports (Class B)**: Recreational vessel positioning
- **Type 24 Static Data Reports**: Class B vessel identification (Part A/B)
- **Error Handling**: Malformed JSON, missing fields, invalid data types
- **Field Validation**: MMSI ranges, coordinate bounds, speed/course limits

### **2. NMEA 0183 Conversion Tests** (`Unit/Nmea0183ConversionTests.cs`)
- **Checksum Calculation**: XOR checksum validation per NMEA standard
- **Coordinate Conversion**: Decimal degrees to ddmm.mmmm format with N/S/E/W
- **Sentence Validation**: Format compliance, ASCII encoding, length limits
- **Fragment Handling**: Multi-part message fragmentation for Type 5 messages
- **AIS Payload Validation**: 6-bit ASCII encoding and fill bits
- **OpenCPN Compatibility**: Sentence format and port configuration requirements

### **3. Coordinate Conversion Tests** (`Unit/CoordinateConversionTests.cs`)
- **High Precision Conversion**: Sub-meter accuracy for navigation
- **Edge Cases**: Equator, poles, International Date Line crossings
- **Directional Indicators**: Proper N/S/E/W assignment
- **Format Validation**: NMEA coordinate format compliance
- **Bounding Box Filtering**: Geographic area filtering with dateline handling
- **AIS Special Values**: "Not available" coordinate detection (91.0, 181.0)

### **4. Configuration Tests** (`Unit/ConfigurationTests.cs`)
- **Configuration Loading**: JSON parsing and binding validation
- **Bounding Box Validation**: Geographic bounds and dateline crossing
- **Network Configuration**: TCP/UDP settings and port validation
- **API Key Security**: Secure storage validation and masking
- **WebSocket URL Validation**: Secure connection requirements (WSS only)
- **Application Logging**: Statistics reporting interval validation

### **5. Integration Pipeline Tests** (`Integration/AisToNmeaPipelineTests.cs`)
- **End-to-End Conversion**: Complete AIS JSON to NMEA sentence pipeline
- **Multi-Message Types**: Type 1, 5, 18, and 24 message processing
- **Data Integrity**: Round-trip coordinate conversion precision
- **Error Propagation**: Graceful handling of invalid input data
- **Geographic Filtering**: Bounding box filtering integration
- **Performance Integration**: Multi-message processing efficiency

### **6. Network Services Tests** (`Integration/NetworkServicesTests.cs`)
- **TCP Server Functionality**: Multi-client connections and broadcasting
- **UDP Broadcasting**: Message transmission and network compatibility
- **NMEA Message Integrity**: Network transmission without corruption
- **OpenCPN Compatibility**: Port configuration and message format requirements
- **Error Handling**: Client disconnection and connection recovery
- **Concurrent Access**: Thread safety and multi-client scenarios

### **7. Performance Tests** (`Performance/PerformanceTests.cs`)
- **Throughput Targets**: 1000+ AIS messages/sec, 10000+ coordinate conversions/sec
- **Memory Usage**: Bounded memory consumption, leak detection
- **Scalability**: Concurrent processing and high-volume scenarios
- **Real-Time Simulation**: Pacific Northwest traffic patterns (50 messages/min)
- **Traffic Spikes**: High-volume port simulation with burst handling
- **Long-Running Stability**: Performance consistency over time

## 🔬 Test Data and Validation

### **Realistic AIS Test Data**
- **Real-World JSON Samples**: Based on aisstream.io WebSocket API format
- **Geographic Coverage**: Pacific Northwest, International Date Line, global coordinates
- **Message Variety**: All supported AIS message types with realistic field values
- **Edge Cases**: Invalid coordinates, missing fields, boundary conditions
- **Error Scenarios**: Malformed JSON, data type mismatches, out-of-range values

### **NMEA 0183 Validation**
- **Industry Standards**: ITU-R M.1371 AIS specification compliance
- **Checksum Accuracy**: XOR checksum calculation and validation
- **Format Compliance**: ASCII encoding, sentence length, and structure
- **OpenCPN Testing**: Real navigation software compatibility validation
- **Fragment Handling**: Multi-sentence message reconstruction

### **Coordinate Test Cases**
```csharp
// Example test cases from AisTestData.cs
new CoordinateTestCase
{
    Description = "High precision Pacific Northwest",
    DecimalLatitude = 48.123456,
    DecimalLongitude = -122.987654,
    ExpectedNmeaLatitude = "4807.407,N",
    ExpectedNmeaLongitude = "12259.259,W"
}
```

## 🚀 Running Tests

### **Command Line Execution**
```bash
# Run all tests
dotnet test

# Run specific test categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Performance"

# Run tests with detailed output
dotnet test --verbosity normal

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### **Test Categories and Execution Time**
- **Unit Tests**: ~5-10 seconds (fast feedback)
- **Integration Tests**: ~30-60 seconds (network operations)
- **Performance Tests**: ~2-5 minutes (throughput validation)

## 📊 Performance Targets

### **Throughput Requirements**
- **AIS JSON Parsing**: ≥1,000 messages/second
- **Coordinate Conversion**: ≥10,000 conversions/second  
- **NMEA Validation**: ≥5,000 validations/second
- **End-to-End Pipeline**: ≥500 complete conversions/second

### **Real-Time Performance**
- **Message Latency**: <100ms average, <500ms maximum
- **Memory Usage**: <1KB per message, <50MB total application
- **CPU Utilization**: <50% during normal operation
- **Network Throughput**: Support 100+ TCP connections

### **Marine Navigation Compatibility**
- **OpenCPN Integration**: TCP port 2002, UDP broadcast support
- **NMEA Compliance**: Full NMEA 0183 standard adherence
- **Coordinate Precision**: Sub-meter accuracy for navigation
- **Message Integrity**: Zero data corruption during transmission

## 🛡️ Security and Validation Testing

### **API Key Security**
- **Secure Storage Validation**: User Secrets and Environment Variables
- **Key Masking**: Sensitive data protection in logs
- **Configuration Security**: Prevention of plaintext key storage

### **Input Validation**
- **Coordinate Bounds**: Valid latitude (-90 to +90) and longitude (-180 to +180)
- **AIS Special Values**: Proper handling of "not available" indicators
- **Network Security**: WSS-only WebSocket connections, safe port bindings

### **Data Integrity**
- **Checksum Validation**: NMEA sentence integrity verification
- **Round-Trip Accuracy**: Coordinate conversion precision maintenance
- **Error Detection**: Malformed data identification and handling

## 🔧 Test Utilities and Helpers

### **NmeaValidator Class**
```csharp
// NMEA sentence validation
var result = NmeaValidator.ValidateAisSentence(sentence);
result.IsValid.Should().BeTrue();

// Checksum calculation
var checksum = NmeaValidator.CalculateChecksum(data);
var formatted = NmeaValidator.FormatChecksum(checksum);
```

### **CoordinateTestHelper Class**
```csharp
// Coordinate conversion
var nmea = CoordinateTestHelper.ConvertToNmeaFormat(48.123, true);

// Validation with tolerance
var isValid = CoordinateTestHelper.ValidateCoordinateConversion(
    expected, actual, toleranceMinutes: 0.001);

// Bounding box testing
var isInside = CoordinateTestHelper.IsWithinBoundingBox(
    lat, lon, north, south, east, west);
```

## 📝 Test Documentation and Reporting

### **Test Results**
Each test includes detailed assertions and descriptive failure messages:
```csharp
result.IsValid.Should().BeTrue($"NMEA sentence should be valid: {result.ErrorSummary}");
actualThroughput.Should().BeGreaterThan(targetThroughput,
    $"Should process at least {targetThroughput} messages/sec. Actual: {actualThroughput:F0}/sec");
```

### **Performance Metrics**
Performance tests provide detailed metrics:
- Messages per second throughput
- Memory usage per operation
- Latency percentiles (average, maximum)
- Resource utilization ratios

This comprehensive test suite ensures the AIS-to-NMEA0183 converter meets all functional, performance, and compatibility requirements for marine navigation systems.

## 🚀 Test Execution Guide

### **Test Categories**

#### **Unit and Integration Tests (Default)**
Fast-running tests that validate core functionality without performance overhead.

```bash
# Run all tests except performance tests (recommended for development)
dotnet test --filter "Category!=Performance"

# Or simply (performance tests are now categorized separately)
dotnet test
```

#### **Performance Tests**
Long-running tests that validate throughput, memory usage, and scalability.
These tests can take 40-60 seconds to complete.

```bash
# Run only performance tests
dotnet test --filter "Category=Performance"
```

#### **Complete Test Suite**
Run all tests including performance tests (for CI/CD or comprehensive validation).

```bash
# Run all tests including performance tests
dotnet test --no-filter

# Or explicitly include all categories
dotnet test --filter "Category=Performance|Category!=Performance"
```

### **Test Execution Times**

- **Unit/Integration Tests**: ~5-15 seconds
- **Performance Tests**: ~40-60 seconds  
- **Complete Suite**: ~50-75 seconds

### **Recommended Usage**

- **Development**: Use `dotnet test --filter "Category!=Performance"` for fast feedback
- **CI/CD Pipeline**: Use complete test suite for comprehensive validation
- **Performance Monitoring**: Use `dotnet test --filter "Category=Performance"` for benchmark validation

### **Performance Test Details**

The performance tests validate:
- **Throughput**: JSON parsing (10K ops), coordinate conversion (100K ops), NMEA validation (50K ops)
- **Memory Usage**: Bounded memory consumption and leak detection
- **Scalability**: Concurrent processing and real-world traffic simulation
- **Resource Usage**: Long-running stability and traffic spike handling