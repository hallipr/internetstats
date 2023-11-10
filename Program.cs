namespace PingTest;

public class Program
{
    public static async Task Main(string[] args)
    {
        CancellationTokenSource cts = new ();

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Cancel event triggered");
            cts.Cancel();
            eventArgs.Cancel = true;
        };

        string logFileLocation = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        string[] hostnames = { "google.com", "microsoft.com", "cloudflare.com" };


        var tester = new Tester(logFileLocation, hostnames);
        await tester.RunAsync(cts.Token);
    }
}
