using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();
var socket = server.AcceptSocket(); // wait for client

var requestBuffer = new byte[1024];
var receivedBytes = socket.Receive(requestBuffer);

var request = Encoding.UTF8.GetString(requestBuffer);
var httpRequestLine = request.Split("\r\n")[0];
var (httpMethod, targetUrl, httpVersion) = (httpRequestLine.Split(" ")[0], httpRequestLine.Split(" ")[1], httpRequestLine.Split(" ")[2]);

var response = targetUrl == "/" ? $"{httpVersion} 200 OK\r\n\r\n" : $"{httpVersion} 404 NOT FOUND\r\n\r\n";
socket.Send(Encoding.UTF8.GetBytes(response));