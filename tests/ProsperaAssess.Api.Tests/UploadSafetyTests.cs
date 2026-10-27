using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using ProsperaAssess.Api;
using Xunit;
namespace ProsperaAssess.Api.Tests;
public sealed class UploadSafetyTests
{
    private sealed class Environment : IHostEnvironment { public string EnvironmentName {get;set;}="Staging"; public string ApplicationName {get;set;}="Test"; public string ContentRootPath {get;set;}=".";public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();}
    [Theory]
    [InlineData("stream: OK\0",0)]
    [InlineData("stream: Synthetic-Test FOUND\0",400)]
    [InlineData("stream: scan limit ERROR\0",503)]
    public async Task ScannerStreamsOriginalBytesAndOnlyAcceptsExplicitCleanResults(string reply,int status)
    {
        var bytes=Encoding.UTF8.GetBytes("synthetic scan bytes");
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var server=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync();await using var stream=client.GetStream();var command=new byte[10];await stream.ReadExactlyAsync(command);Assert.Equal("zINSTREAM\0",Encoding.UTF8.GetString(command));var length=new byte[4];await stream.ReadExactlyAsync(length);var content=new byte[BinaryPrimitives.ReadInt32BigEndian(length)];await stream.ReadExactlyAsync(content);Assert.Equal(bytes,content);await stream.ReadExactlyAsync(length);Assert.Equal(0,BinaryPrimitives.ReadInt32BigEndian(length));await stream.WriteAsync(Encoding.UTF8.GetBytes(reply));});
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Uploads:ScannerHost","127.0.0.1"},{"Uploads:ScannerPort",port.ToString()}}).Build();
        var scanner=new ClamUploadSafety(config,new Environment());
        if(status==0) await scanner.CheckAsync(bytes,CancellationToken.None); else Assert.Equal(status,(await Assert.ThrowsAsync<PortalBusinessException>(()=>scanner.CheckAsync(bytes,CancellationToken.None))).Status);
        await server.WaitAsync(TimeSpan.FromSeconds(10));
    }
    [Fact]
    public async Task ProductionUploadProtectionFailsClosedWhenNotConfigured() => Assert.Equal(503,(await Assert.ThrowsAsync<PortalBusinessException>(()=>new ClamUploadSafety(new ConfigurationBuilder().Build(),new Environment()).CheckAsync([1],CancellationToken.None))).Status);
}
