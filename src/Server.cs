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
var requestDetails = request.Split("\r\n");
var httpRequestLine = requestDetails[0];
var (httpMethod, targetUrl, httpVersion) = (httpRequestLine.Split(" ")[0], httpRequestLine.Split(" ")[1], httpRequestLine.Split(" ")[2]);

byte[] responseBuffer;
string response = "";

if (targetUrl == "/")
{
    response = $"{httpVersion} 200 OK\r\n\r\n";
}
else
{
    response = $"{httpVersion} 404 Not Found\r\n\r\n";
}


if (targetUrl.StartsWith("/echo/"))
{
    string path = targetUrl.Substring(6);
    response = $"{httpVersion} 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {path.Length}\r\n\r\n{path}";
}

if (targetUrl == "/user-agent")
{
    string userAgentContent = "";
    for (int i = 0; i < requestDetails.Length; i++)
    {
        if (requestDetails[i].StartsWith("User-Agent"))
        {
            userAgentContent = requestDetails[i].Substring(12);
        }
    }
    response = $"{httpVersion} 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {userAgentContent.Length}\r\n\r\n{userAgentContent}";
}

responseBuffer = Encoding.UTF8.GetBytes(response);
socket.Send(responseBuffer);