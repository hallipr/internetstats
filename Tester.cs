using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using SpeedTest.Net;

namespace PingTest;

public class Tester
{
    private static readonly byte[] _buffer = Encoding.ASCII.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    private readonly PingOptions _pingOptions = new() { DontFragment = true };
    private readonly Ping _pingSender = new();
    private readonly string _logFileLocation;
    private readonly string[] _hostnames;
    private static readonly TimeSpan pingEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan speedTestEvery = TimeSpan.FromHours(1);

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

            if (!speedTestStopwatch.IsRunning || speedTestStopwatch.Elapsed > speedTestEvery)
            {
                await SpeedTestAsync(speedTestLogFile);
                speedTestStopwatch.Restart();
            }

            var remaining = pingEvery - pingStopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }
    }

private async Task SpeedTestAsync(string logFile)
{
    var url = "http://speedtest.tele2.net/1MB.zip"; // URL of a test file
    var stopwatch = Stopwatch.StartNew();
    using var client = new HttpClient();
    using var steam = await client.GetStreamAsync(url);

    {
        stopwatch.Start();

        await client.DownloadFileTaskAsync(new Uri(url), "./testfile.zip"); // Download the file

        stopwatch.Stop();
    }

    File.Delete("./testfile.zip"); // Delete the file

    var speed = (1024 / stopwatch.Elapsed.TotalSeconds); // Calculate speed in KB/s

    Console.WriteLine($"Speed: {speed} KB/s");

    // Log the speed
    using (StreamWriter sw = File.AppendText(logFile))
    {
        await sw.WriteLineAsync($"{DateTimeOffset.UtcNow:s} - speed test - {speed} KB/s");
    }
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
        PingReply reply = await _pingSender.SendPingAsync(hostname, timeout: 1000, _buffer, _pingOptions);
        if (reply.Status != IPStatus.Success)
        {
            throw new Exception("Ping failed");
        }

        return reply.RoundtripTime;
    }
}
