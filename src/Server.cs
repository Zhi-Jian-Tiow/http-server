using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Accepting client connections");
while (true)
{
    server.BeginAcceptSocket(AcceptSocketCallback, server);
}


void AcceptSocketCallback(IAsyncResult asyncResult)
{
    var socket = server.EndAcceptSocket(asyncResult);
    var buffer = new StringBuilder();
    var requestBuffer = new byte[1024];

    var receivedBytes = socket.Receive(requestBuffer);
    buffer.Append(Encoding.ASCII.GetString(requestBuffer, 0, receivedBytes));
    var requestReceived = buffer.ToString();

    string response;

    var clientRequest = new Request(requestReceived);
    var serverResponse = new Response(clientRequest);

    var requestUrl = clientRequest.RequestUrl;

    if (requestUrl == "/")
    {
        response = serverResponse.GenerateSuccessResponse("200", "OK");
    }
    else if (requestUrl.StartsWith("/echo/"))
    {
        string target = "/echo/";
        var responseBodyContent = clientRequest.RequestUrl.Substring(target.Length);
        response = serverResponse.GenerateSuccessResponseWithBody(responseBodyContent, "text/plain");
    }
    else if (requestUrl == "/user-agent")
    {
        string responseBodyContent = "";
        var target = "User-Agent: ";
        for (int i = 0; i < clientRequest.Headers.Count; i++)
        {
            if (clientRequest.Headers[i].StartsWith(target))
            {
                responseBodyContent = clientRequest.Headers[i].Substring(target.Length);
                break;
            }
        }
        response = serverResponse.GenerateSuccessResponseWithBody(responseBodyContent, "text/plain");
    }
    else if (requestUrl.StartsWith("/files/"))
    {

        var target = "/files/";
        string fileName = requestUrl.Substring(target.Length);
        var cmdArgs = Environment.GetCommandLineArgs();
        string directory = cmdArgs[2];
        var targetFilePath = Path.Join(directory, fileName);

        if (clientRequest.Method == "POST")
        {
            var fileContent = clientRequest.Body;
            File.WriteAllText(targetFilePath, fileContent);
            response = serverResponse.GenerateSuccessResponse("201", "Created");
        }
        else
        {
            Console.WriteLine("Finding requested file: " + targetFilePath);
            if (File.Exists(targetFilePath))
            {
                Console.WriteLine("Reading requested file content");
                var fileContent = File.ReadAllText(targetFilePath);
                response = serverResponse.GenerateSuccessResponseWithBody(fileContent, "application/octet-stream");
            }
            else
            {
                Console.WriteLine("File Not Found");
                response = serverResponse.Generate404Response();
            }
        }
    }
    else
    {
        response = serverResponse.Generate404Response();
    }

    socket.Send(Encoding.ASCII.GetBytes(response));
    socket.Close();
}

public class Response
{
    private readonly Request _request;
    public Response(Request request)
    {
        _request = request;
    }

    public string GenerateSuccessResponse(string responseStatus, string responseMessage)
    {
        return $"{_request.ProtocolVersion} {responseStatus} {responseMessage}\r\n\r\n";
    }

    public string Generate404Response()
    {
        return $"{_request.ProtocolVersion} 404 Not Found\r\n\r\n";
    }

    public string Generate201Response()
    {
        return "";
    }
    public string GenerateSuccessResponseWithBody(string responseBodyContent, string contentType)
    {
        return $"{_request.ProtocolVersion} 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {responseBodyContent.Length}\r\n\r\n{responseBodyContent}";
    }
}

public class Request
{
    public string Method { get; private set; }
    public string ProtocolVersion { get; private set; }
    public string RequestUrl { get; private set; }
    public List<string> Headers { get; private set; }
    public string Body { get; private set; }

    public Request(string request)
    {
        var requestDetails = request.Split("\r\n");
        var requestLine = requestDetails[0];
        (Method, RequestUrl, ProtocolVersion) = (requestLine.Split(" ")[0], requestLine.Split(" ")[1], requestLine.Split(" ")[2]);
        Headers = ExtractHeaders(requestDetails);
        Body = ExtractRequestBody(requestDetails);
    }

    private static List<string> ExtractHeaders(string[] requestDetails)
    {
        var headers = new List<string>();
        for (int i = 1; i < requestDetails.Length; i++)
        {
            headers.Add(requestDetails[i]);
        }
        return headers;
    }

    private static string ExtractRequestBody(string[] requestDetails)
    {
        return requestDetails.Last();
    }
}