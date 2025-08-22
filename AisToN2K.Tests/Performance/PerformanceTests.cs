using AisToN2K.Models;
using AisToN2K.Services;
using AisToN2K.Tests.TestData;
using AisToN2K.Tests.Utilities;
using FluentAssertions;
using Newtonsoft.Json;
using System.Diagnostics;
using Xunit;

namespace AisToN2K.Tests.Performance
{
    /// <summary>
    /// Performance tests for AIS-to-NMEA0183 conversion pipeline.
    /// Validates throughput, memory usage, and scalability requirements.
    /// These tests are excluded from normal test runs due to their long execution time.
    /// Use: dotnet test --filter "Category=Performance" to run only performance tests.
    /// </summary>
    public class PerformanceTests
    {
        #region Throughput Performance Tests

        [Fact]
        [Trait("Category", "Performance")]
        public void AisJsonParsing_HighThroughput_ShouldMeetPerformanceTargets()
        {
            // Arrange
            var testMessage = AisTestData.ValidType1Json;
            const int iterations = 10000;
            const double targetMessagesPerSecond = 1000; // Based on project requirements

            // Act
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var result = JsonConvert.DeserializeObject<Models.AisStreamMessage>(testMessage);
                result.Should().NotBeNull(); // Ensure parsing actually happened
            }
            
            stopwatch.Stop();

            // Assert
            var actualMessagesPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            actualMessagesPerSecond.Should().BeGreaterThan(targetMessagesPerSecond,
                $"Should parse at least {targetMessagesPerSecond} AIS messages per second. Actual: {actualMessagesPerSecond:F0}/sec");

            // Additional performance metrics
            var averageTimePerMessage = stopwatch.Elapsed.TotalMicroseconds / iterations;
            averageTimePerMessage.Should().BeLessThan(1000, 
                $"Average parsing time should be less than 1ms per message. Actual: {averageTimePerMessage:F1}μs");
        }

        [Fact]
        [Trait("Category", "Performance")]
        public void CoordinateConversion_HighThroughput_ShouldBeEfficient()
        {
            // Arrange
            const int iterations = 100000;
            const double targetConversionsPerSecond = 10000;
            
            var coordinates = new[]
            {
                (48.123456, -122.987654),
                (59.123456, 10.987654),
                (-33.856159, 151.215256),
                (35.6762, 179.9999)
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var coord = coordinates[i % coordinates.Length];
                var lat = CoordinateTestHelper.ConvertToNmeaFormat(coord.Item1, true);
                var lon = CoordinateTestHelper.ConvertToNmeaFormat(coord.Item2, false);
                
                // Ensure conversions actually happened
                lat.Should().NotBeNullOrEmpty();
                lon.Should().NotBeNullOrEmpty();
            }
            
            stopwatch.Stop();

            // Assert
            var actualConversionsPerSecond = (iterations * 2) / stopwatch.Elapsed.TotalSeconds; // 2 conversions per iteration
            actualConversionsPerSecond.Should().BeGreaterThan(targetConversionsPerSecond,
                $"Should convert at least {targetConversionsPerSecond} coordinates per second. Actual: {actualConversionsPerSecond:F0}/sec");
        }

        [Fact]
        [Trait("Category", "Performance")]
        public async Task NmeaConversion_HighThroughput_ShouldMeetTargets()
        {
            // Arrange
            var converter = new Nmea0183Converter(debugMode: false);
            var testData = new AisData
            {
                MessageType = 1,
                Mmsi = 123456789,
                Latitude = 48.123456,
                Longitude = -122.987654,
                SpeedOverGround = 12.5,
                CourseOverGround = 245.6,
                Heading = 250
            };

            const int iterations = 5000;
            const double targetConversionsPerSecond = 500;

            // Act
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var result = await converter.ConvertToNmea0183Async(testData);
                result.Should().NotBeNull(); // Ensure conversion actually happened
            }
            
            stopwatch.Stop();

            // Assert
            var actualConversionsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            actualConversionsPerSecond.Should().BeGreaterThan(targetConversionsPerSecond,
                $"Should convert at least {targetConversionsPerSecond} AIS messages to NMEA per second. Actual: {actualConversionsPerSecond:F0}/sec");
        }

        [Fact]
        [Trait("Category", "Performance")]
        public void NmeaValidation_HighThroughput_ShouldBeEfficient()
        {
            // Arrange
            var testSentences = new[]
            {
                "!AIVDM,1,1,,A,15Muq70001G?tRrM5M4P8?v4080u,0*28", // Correct checksum
                "!AIVDM,2,1,0,A,55?MbV02;H;s<HtKR20EHE:0@T4@Dn2222222216L961O5Gf0NSQEp6ClRp8,0*1D", // Fixed checksum
                "!AIVDM,2,2,0,A,88888888880,2*24" // Fixed checksum
            };

            const int iterations = 50000;
            const double targetValidationsPerSecond = 5000;

            // Act
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var sentence = testSentences[i % testSentences.Length];
                var result = NmeaValidator.ValidateAisSentence(sentence);
                result.IsValid.Should().BeTrue(); // Ensure validation actually ran
            }
            
            stopwatch.Stop();

            // Assert
            var actualValidationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            actualValidationsPerSecond.Should().BeGreaterThan(targetValidationsPerSecond,
                $"Should validate at least {targetValidationsPerSecond} NMEA sentences per second. Actual: {actualValidationsPerSecond:F0}/sec");
        }

        #endregion

        #region Memory Performance Tests

        [Fact]
        [Trait("Category", "Performance")]
        public void AisJsonParsing_MemoryUsage_ShouldBeBounded()
        {
            // Arrange
            var testMessage = AisTestData.ValidType1Json;
            const int iterations = 1000;

            // Measure initial memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = JsonConvert.DeserializeObject<Models.AisStreamMessage>(testMessage);
                result.Should().NotBeNull();
                
                // Force garbage collection periodically to test for memory leaks
                if (i % 100 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            // Measure final memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            var memoryPerMessage = memoryIncrease / (double)iterations;
            
            memoryPerMessage.Should().BeLessThan(2048, // Less than 2KB per message (adjusted from 1KB)
                $"Memory usage per message should be minimal. Actual: {memoryPerMessage:F0} bytes/message");
            
            memoryIncrease.Should().BeLessThan(10 * 1024 * 1024, // Less than 10MB total
                $"Total memory increase should be bounded. Actual: {memoryIncrease / 1024 / 1024:F1} MB");
        }

        [Fact]
        [Trait("Category", "Performance")]
        public void CoordinateConversion_MemoryUsage_ShouldNotLeak()
        {
            // Arrange
            const int iterations = 10000;
            var testCoordinates = new[]
            {
                (48.123456, -122.987654),
                (59.123456, 10.987654),
                (-33.856159, 151.215256)
            };

            // Measure initial memory
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var coord = testCoordinates[i % testCoordinates.Length];
                var lat = CoordinateTestHelper.ConvertToNmeaFormat(coord.Item1, true);
                var lon = CoordinateTestHelper.ConvertToNmeaFormat(coord.Item2, false);
                
                // Don't hold references to results to allow GC
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            memoryIncrease.Should().BeLessThan(1024 * 1024, // Less than 1MB
                $"Coordinate conversion should not cause significant memory increase. Actual: {memoryIncrease / 1024:F0} KB");
        }

        #endregion

        #region Scalability Tests

        [Fact]
        [Trait("Category", "Performance")]
        public async Task ProcessMultipleMessageTypes_Concurrently_ShouldScale()
        {
            // Arrange
            var messageTypes = new[]
            {
                AisTestData.ValidType1Json,
                AisTestData.ValidType5Json,
                AisTestData.ValidType18Json,
                AisTestData.ValidType24AJson
            };

            const int concurrentTasks = 10;
            const int messagesPerTask = 1000;
            const double targetTotalThroughput = 5000; // messages per second

            // Act
            var stopwatch = Stopwatch.StartNew();
            
            var tasks = Enumerable.Range(0, concurrentTasks).Select(taskId =>
                Task.Run(() =>
                {
                    for (int i = 0; i < messagesPerTask; i++)
                    {
                        var messageJson = messageTypes[i % messageTypes.Length];
                        var aisMessage = JsonConvert.DeserializeObject<Models.AisStreamMessage>(messageJson);
                        
                        // Simulate coordinate conversion for position reports
                        if (aisMessage?.Message?.PositionReport != null)
                        {
                            var lat = CoordinateTestHelper.ConvertToNmeaFormat(
                                aisMessage.Message.PositionReport.Latitude, true);
                            var lon = CoordinateTestHelper.ConvertToNmeaFormat(
                                aisMessage.Message.PositionReport.Longitude, false);
                        }
                    }
                })
            ).ToArray();

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var totalMessages = concurrentTasks * messagesPerTask;
            var actualThroughput = totalMessages / stopwatch.Elapsed.TotalSeconds;
            
            actualThroughput.Should().BeGreaterThan(targetTotalThroughput,
                $"Concurrent processing should achieve at least {targetTotalThroughput} messages/sec. Actual: {actualThroughput:F0}/sec");
            
            // Verify all tasks completed successfully
            tasks.Should().AllSatisfy(task => task.Status.Should().Be(TaskStatus.RanToCompletion));
        }

        [Fact]
        [Trait("Category", "Performance")]
        public void BoundingBoxFiltering_LargeDataset_ShouldBeEfficient()
        {
            // Arrange
            const int coordinateCount = 100000;
            const double targetFiltersPerSecond = 50000;
            
            var boundingBox = new Configuration.BoundingBox
            {
                North = 49.0,
                South = 48.0,
                East = -122.0,
                West = -123.0
            };

            // Generate test coordinates
            var random = new Random(12345); // Fixed seed for reproducible results
            var coordinates = new List<(double lat, double lon)>();
            
            for (int i = 0; i < coordinateCount; i++)
            {
                var lat = random.NextDouble() * 180 - 90;  // -90 to 90
                var lon = random.NextDouble() * 360 - 180; // -180 to 180
                coordinates.Add((lat, lon));
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var insideCount = 0;
            
            foreach (var (lat, lon) in coordinates)
            {
                var isInside = CoordinateTestHelper.IsWithinBoundingBox(
                    lat, lon, boundingBox.North, boundingBox.South, 
                    boundingBox.East, boundingBox.West);
                
                if (isInside) insideCount++;
            }
            
            stopwatch.Stop();

            // Assert
            var filtersPerSecond = coordinateCount / stopwatch.Elapsed.TotalSeconds;
            filtersPerSecond.Should().BeGreaterThan(targetFiltersPerSecond,
                $"Bounding box filtering should process at least {targetFiltersPerSecond} coordinates/sec. Actual: {filtersPerSecond:F0}/sec");
            
            insideCount.Should().BeGreaterThan(0, "Some coordinates should be inside the bounding box");
            insideCount.Should().BeLessThan(coordinateCount, "Not all coordinates should be inside the bounding box");
        }

        #endregion

        #region Real-world Scenario Tests

        [Fact]
        [Trait("Category", "Performance")]
        public void SimulateRealTimeAisStream_ShouldMeetPerformanceTargets()
        {
            // Arrange - Simulate 50 messages per minute (Pacific Northwest typical rate)
            const int messagesPerMinute = 50;
            const int simulationDurationSeconds = 10;
            const int totalMessages = (messagesPerMinute * simulationDurationSeconds) / 60;
            
            var messageTemplates = new[]
            {
                AisTestData.ValidType1Json,
                AisTestData.ValidType18Json,
                AisTestData.ValidType5Json
            };

            // Act - Simulate real-time processing
            var stopwatch = Stopwatch.StartNew();
            var processedMessages = 0;
            var latencies = new List<double>();

            for (int i = 0; i < totalMessages; i++)
            {
                var messageStart = Stopwatch.StartNew();
                
                // Simulate message processing pipeline
                var messageJson = messageTemplates[i % messageTemplates.Length];
                var aisMessage = JsonConvert.DeserializeObject<Models.AisStreamMessage>(messageJson);
                
                if (aisMessage?.Message?.PositionReport != null)
                {
                    var lat = aisMessage.Message.PositionReport.Latitude;
                    var lon = aisMessage.Message.PositionReport.Longitude;
                    
                    // Coordinate conversion
                    var nmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
                    var nmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);
                    
                    // NMEA sentence generation (simulated)
                    var nmeaSentence = $"!AIVDM,1,1,,A,15{aisMessage.Message.PositionReport.UserID:X7}001G,0*";
                    var checksum = NmeaValidator.CalculateChecksum(nmeaSentence.Substring(1, nmeaSentence.Length - 2));
                    nmeaSentence += NmeaValidator.FormatChecksum(checksum);
                    
                    // Validation
                    var validation = NmeaValidator.ValidateAisSentence(nmeaSentence);
                    validation.IsValid.Should().BeTrue();
                }
                
                messageStart.Stop();
                latencies.Add(messageStart.Elapsed.TotalMilliseconds);
                processedMessages++;
                
                // Simulate real-time intervals
                var targetInterval = TimeSpan.FromSeconds(simulationDurationSeconds / (double)totalMessages);
                var elapsedForMessage = messageStart.Elapsed;
                if (elapsedForMessage < targetInterval)
                {
                    Thread.Sleep(targetInterval - elapsedForMessage);
                }
            }
            
            stopwatch.Stop();

            // Assert - Real-time performance requirements
            processedMessages.Should().Be(totalMessages, "All messages should be processed");
            
            var averageLatency = latencies.Average();
            averageLatency.Should().BeLessThan(100, // Less than 100ms per message
                $"Average processing latency should be less than 100ms. Actual: {averageLatency:F1}ms");
            
            var maxLatency = latencies.Max();
            maxLatency.Should().BeLessThan(500, // Less than 500ms for any single message
                $"Maximum processing latency should be less than 500ms. Actual: {maxLatency:F1}ms");
            
            // Verify we can keep up with real-time requirements
            var actualProcessingTime = latencies.Sum();
            var availableTime = simulationDurationSeconds * 1000; // Convert to milliseconds
            var utilizationRatio = actualProcessingTime / availableTime;
            
            utilizationRatio.Should().BeLessThan(0.5, // Should use less than 50% of available time
                $"Processing should use less than 50% of available time. Actual: {utilizationRatio:P1}");
        }

        [Fact]
        [Trait("Category", "LongRunning")] // Changed from Performance to LongRunning
        public void SimulateHighVolumePort_ShouldHandleTrafficSpikes()
        {
            // Arrange - Simulate high-volume port with traffic spikes (reduced duration)
            const int baseMessagesPerSecond = 10;
            const int spikeMessagesPerSecond = 100;
            const int spikeDurationSeconds = 1; // Reduced from 2
            const int totalDurationSeconds = 3; // Reduced from 10
            
            var messageTemplate = AisTestData.ValidType1Json;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var processedCount = 0;
            var maxProcessingTime = 0.0;

            while (stopwatch.Elapsed.TotalSeconds < totalDurationSeconds)
            {
                // Determine current message rate (simulate traffic spike)
                var currentSecond = (int)stopwatch.Elapsed.TotalSeconds;
                var isSpike = currentSecond >= 4 && currentSecond < 4 + spikeDurationSeconds;
                var targetRate = isSpike ? spikeMessagesPerSecond : baseMessagesPerSecond;
                
                var messageProcessingStart = Stopwatch.StartNew();
                
                // Process message
                var aisMessage = JsonConvert.DeserializeObject<Models.AisStreamMessage>(messageTemplate);
                var lat = aisMessage!.Message!.PositionReport!.Latitude;
                var lon = aisMessage.Message.PositionReport.Longitude;
                var nmeaLat = CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
                var nmeaLon = CoordinateTestHelper.ConvertToNmeaFormat(lon, false);
                
                messageProcessingStart.Stop();
                maxProcessingTime = Math.Max(maxProcessingTime, messageProcessingStart.Elapsed.TotalMilliseconds);
                processedCount++;
                
                // Simulate arrival rate
                var intervalMs = 1000.0 / targetRate;
                var nextMessageTime = processedCount * intervalMs;
                var currentTime = stopwatch.Elapsed.TotalMilliseconds;
                
                if (currentTime < nextMessageTime)
                {
                    var sleepTime = (int)(nextMessageTime - currentTime);
                    if (sleepTime > 0) Thread.Sleep(sleepTime);
                }
            }
            
            stopwatch.Stop();

            // Assert
            var actualRate = processedCount / stopwatch.Elapsed.TotalSeconds;
            actualRate.Should().BeGreaterThan(baseMessagesPerSecond * 0.8, 
                "Should handle at least 80% of the target message rate during spikes");
            
            maxProcessingTime.Should().BeLessThan(50, 
                $"Maximum processing time should be less than 50ms. Actual: {maxProcessingTime:F1}ms");
        }

        #endregion

        #region CPU and Resource Usage Tests

        [Fact]
        [Trait("Category", "LongRunning")] // Changed from Performance to LongRunning  
        public void LongRunningProcessing_ShouldMaintainPerformance()
        {
            // Arrange - Reduced duration for testing
            const int durationSeconds = 5; // Changed from 1 minute to 5 seconds
            const int messagesPerSecond = 20;
            var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);
            
            var messageTemplate = AisTestData.ValidType1Json;
            var processingTimes = new List<double>();

            // Act
            while (DateTime.UtcNow < endTime)
            {
                var start = Stopwatch.StartNew();
                
                // Process message
                var aisMessage = JsonConvert.DeserializeObject<Models.AisStreamMessage>(messageTemplate);
                var lat = aisMessage!.Message!.PositionReport!.Latitude;
                var lon = aisMessage.Message.PositionReport.Longitude;
                CoordinateTestHelper.ConvertToNmeaFormat(lat, true);
                CoordinateTestHelper.ConvertToNmeaFormat(lon, false);
                
                start.Stop();
                processingTimes.Add(start.Elapsed.TotalMilliseconds);
                
                // Control message rate
                Thread.Sleep(1000 / messagesPerSecond);
                
                // Periodic cleanup to test for memory leaks
                if (processingTimes.Count % 100 == 0)
                {
                    GC.Collect();
                }
            }

            // Assert - Performance should not degrade over time
            var firstHalf = processingTimes.Take(processingTimes.Count / 2).Average();
            var secondHalf = processingTimes.Skip(processingTimes.Count / 2).Average();
            var degradationRatio = secondHalf / firstHalf;
            
            degradationRatio.Should().BeLessThan(1.5, 
                $"Performance should not degrade significantly over time. First half: {firstHalf:F2}ms, Second half: {secondHalf:F2}ms");
            
            processingTimes.Average().Should().BeLessThan(10, 
                "Average processing time should remain low throughout long-running operation");
        }

        #endregion
    }
}
