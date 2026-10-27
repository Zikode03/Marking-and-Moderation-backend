using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using ProsperaAssess;

namespace ProsperaAssess.Api;

// No uploaded file is persisted or sent to the model until the scanner returns OK.
public sealed class ClamUploadSafety(IConfiguration configuration, IHostEnvironment environment) : IUploadSafety
{
    public async Task CheckAsync(byte[] content, CancellationToken ct)
    {
        var host = configuration["Uploads:ScannerHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return;
            throw new PortalBusinessException(503, new { error = "Upload protection is unavailable. No files were saved. Try again later." });
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, configuration.GetValue("Uploads:ScannerPort", 3310), timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var size = new byte[4];
            for (var offset = 0; offset < content.Length; offset += 65536)
            {
                var length = Math.Min(65536, content.Length - offset); BinaryPrimitives.WriteInt32BigEndian(size, length);
                await stream.WriteAsync(size, timeout.Token); await stream.WriteAsync(content.AsMemory(offset, length), timeout.Token);
            }
            await stream.WriteAsync(new byte[4], timeout.Token);
            var response = new List<byte>(); var buffer = new byte[1];
            while (response.Count < 4096 && await stream.ReadAsync(buffer, timeout.Token) > 0 && buffer[0] != 0) response.Add(buffer[0]);
            var message = Encoding.UTF8.GetString(response.ToArray());
            if (message.EndsWith(": OK", StringComparison.Ordinal)) return;
            if (message.EndsWith(" FOUND", StringComparison.Ordinal)) throw new PortalBusinessException(400, new { error = "A file failed the security scan. No files were saved. Ask your administrator to check the original file." });
            throw new IOException("Scanner did not return a clean result.");
        }
        catch (Exception ex) when (ex is SocketException or IOException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new PortalBusinessException(503, new { error = "Upload protection is unavailable. No files were saved. Try again later." }); }
    }
}
