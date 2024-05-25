using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Accepting client connections");
var socket = server.AcceptSocket(); // wait for client

var requestBuffer = new byte[1024];
var receivedBytes = socket.Receive(requestBuffer);

var request = Encoding.UTF8.GetString(requestBuffer);
var httpRequestLine = request.Split("\r\n")[0];
var (httpMethod, targetUrl, httpVersion) = (httpRequestLine.Split(" ")[0], httpRequestLine.Split(" ")[1], httpRequestLine.Split(" ")[2]);
// Console.WriteLine(httpMethod);
// Console.WriteLine(targetUrl);
// Console.WriteLine(httpVersion);

var responseStatus = "";
var responseBody = "";

if (targetUrl.Length > 1)
{
    var paths = targetUrl.Split("/");
    if (paths.Length == 3 & paths[1] == "echo")
    {
        responseBody += $"\r\nContent-Type: text/plain\r\nContent-Length: 3\r\n\r\n{paths[2]}";
    }
}

if (responseBody != "" | targetUrl == "/")
{
    responseStatus = $"{httpVersion} 200 OK\r\n\r\n";
}
else
{
    responseStatus = $"{httpVersion} 404 Not Found\r\n\r\n";
}

// Console.WriteLine(responseStatus + responseBody);
socket.Send(Encoding.UTF8.GetBytes(responseStatus + responseBody));