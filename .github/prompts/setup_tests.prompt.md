As an experienced .NET test engineer, Using .NET 8 create a comprehensive test suite for my AIS-to-NMEA0183 converter application. Before generating any tests, use perplexity MCP server to thoroughly research:

1. AIS stream message formats and specifications - focusing on Types 1, 5, 18, 24A, and 24B
2. NMEA0183 message structure, checksums, and encoding standards
3. Technical requirements for proper coordinate conversion between formats
4. OpenCPN compatibility requirements for NMEA0183 messages

Use this research to ensure test cases accurately reflect real-world message formats and industry standards.

## Key Testing Areas (in priority order):

1. AIS JSON Parsing Tests:
   - Create extensive tests for parsing all supported AIS message types (1, 5, 18, 24A, 24B)
   - Test with valid JSON samples from the aisstream API (use actual examples from documentation)
   - Test with malformed/incomplete JSON data
   - Verify correct handling of all vessel properties (MMSI, position, course, speed, name, etc.)
   - Test geographic bounding box filtering logic

2. NMEA0183 Conversion Tests:
   - Verify conversion accuracy from AIS data model to NMEA0183 format
   - Test proper checksum calculation for all message types
   - Validate coordinate encoding precision (proper scaling factors)
   - Test message formatting against NMEA0183 standard requirements
   - Generate test cases for each supported message type with known inputs/outputs

3. Network Services Tests:
   - Test TCP server message broadcasting functionality on port 2002
   - Validate UDP broadcast capabilities on port 2003
   - Test connection handling, client management and error recovery
   - Mock WebSocket service for testing reconnection logic
   - Verify thread safety in multi-client scenarios

4. End-to-End Tests:
   - Test full pipeline from sample AIS JSON to final NMEA output
   - Verify configuration loading and validation
   - Test command-line argument processing (especially --debug flag)
   - Validate debug vs. normal mode output differences
   - Test API key configuration via User Secrets and Environment Variables

## Requirements:

- Use xUnit for test framework
- Implement Moq for service mocking
- Include realistic sample AIS JSON data covering all message types
- Create test utilities for NMEA message validation
- Add performance tests for high-volume message processing
- Write tests for error handling and recovery scenarios
- Include tests for API authentication and security features

Generate a complete test project structure with organized test classes for each major component, emphasizing test clarity, documentation, and maintainability.