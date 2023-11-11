using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;

namespace PingTest;

public class Tester
{
    private static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SpeedTestEvery = TimeSpan.FromMinutes(30);
    private static readonly string SpeedTestDataUrl = "https://github.com/hallipr/internetstats/raw/main/testdata/100.bin"; // URL of a test file

    private static readonly byte[] PingData = Encoding.ASCII.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    private readonly PingOptions _pingOptions = new() { DontFragment = true };
    private readonly Ping _pingSender = new();
    private readonly string _logFileLocation;
    private readonly string[] _hostnames;

    public Tester(string logFileLocation, string[] hostnames)
    {
        _logFileLocation = logFileLocation;
        _hostnames = hostnames;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var speedTestStopwatch = new Stopwatch();
        while (true)
        {
            var pingStopwatch = Stopwatch.StartNew();

            if (!Directory.Exists(_logFileLocation))
            {
                Directory.CreateDirectory(_logFileLocation);
            }

            var pingLogFile = $"{_logFileLocation}/{DateTimeOffset.UtcNow:yyyy-MM-dd}-ping.log";
            var speedTestLogFile = $"{_logFileLocation}/{DateTimeOffset.UtcNow:yyyy-MM-dd}-speed.log";
            if (!File.Exists(pingLogFile))
            {
                File.AppendAllLines(pingLogFile, _hostnames);
            }

            await PingHostsAsync(pingLogFile);

            if (!speedTestStopwatch.IsRunning || speedTestStopwatch.Elapsed > SpeedTestEvery)
            {
                await SpeedTestAsync(speedTestLogFile);
                speedTestStopwatch.Restart();
            }

            var remaining = PingEvery - pingStopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }
    }

    private async Task SpeedTestAsync(string logFile)
    {
        var stopwatch = Stopwatch.StartNew();
        using var client = new HttpClient();
        using var stream = await client.GetStreamAsync(SpeedTestDataUrl);

        var buffer = new byte[1024 * 10];
        var totalRead = 0;

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer);
            totalRead += bytesRead;

            if (bytesRead == 0)
            {
                break;
            }
        }

        var speed = totalRead / stopwatch.Elapsed.TotalSeconds / 1024 / 1024; // Calculate speed in MB/s

        Console.WriteLine($"{DateTimeOffset.UtcNow:s} - speed - {speed:f3} MB/s");
        File.AppendAllLines(logFile, new[] { $"{DateTimeOffset.UtcNow:s} - {speed:f3} MB/s" });
    }

    private async Task PingHostsAsync(string logFile)
    {
        var results = new List<string>();
        foreach (string hostname in _hostnames)
        {
            try
            {
                var result = await Test(hostname);
                Console.WriteLine($"{DateTimeOffset.UtcNow:s} - ping {hostname} - {result}ms");
                results.Add($"{hostname[0]}:{result}");
            }
            catch (Exception ex)
            {
                results.Add($"{hostname[0]}:{ex.Message}");
                Console.WriteLine($"{DateTimeOffset.UtcNow:s} - ping {hostname} - {ex.Message}");
            }
        }

        File.AppendAllLines(logFile, new[] { $"{DateTimeOffset.UtcNow:s} - {string.Join(", ", results)}" });
    }

    private async Task<long> Test(string hostname)
    {
        PingReply reply = await _pingSender.SendPingAsync(hostname, timeout: 1000, PingData, _pingOptions);
        if (reply.Status != IPStatus.Success)
        {
            throw new Exception("Ping failed");
        }

        return reply.RoundtripTime;
    }
}
