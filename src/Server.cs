using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

var tcpListener = new TcpListener(IPAddress.Any, 4221);
var stringBuilder = new StringBuilder();
IHttpRequestHandler httpRequestHandler = new HttpRequestHandler();
IHttpResponseHandler httpResponseHandler = new HttpResponseHandler();
var httpServer = new HttpServer(tcpListener, stringBuilder, httpRequestHandler, httpResponseHandler);
httpServer.Start();

public class HttpServer
{
    private readonly IHttpRequestHandler _httpRequestHandler;
    private readonly IHttpResponseHandler _httpResponseHandler;
    private readonly TcpListener _tcpListener;
    private readonly StringBuilder _stringBuilder;
    public HttpServer(TcpListener tcpListener, StringBuilder stringBuilder, IHttpRequestHandler httpRequestHandler, IHttpResponseHandler httpResponseHandler)
    {
        _httpRequestHandler = httpRequestHandler;
        _httpResponseHandler = httpResponseHandler;
        _tcpListener = tcpListener;
        _stringBuilder = stringBuilder;
    }
    public void Start()
    {
        _tcpListener.Start();
        while (true)
        {
            _tcpListener.BeginAcceptSocket(HandleHttpRequest, _tcpListener);
        }
    }

    private void HandleHttpRequest(IAsyncResult asyncResult)
    {
        var socket = _tcpListener.EndAcceptSocket(asyncResult);

        // Accept incoming request by allocating the received bytes into buffer
        var requestBuffer = new byte[1024];
        var receivedRequestSize = socket.Receive(requestBuffer);
        _stringBuilder.Append(Encoding.ASCII.GetString(requestBuffer, 0, receivedRequestSize));
        var requestString = _stringBuilder.ToString();

        var (httpMethod, targetUrl, httpVersion) = _httpRequestHandler.ExtractRequestLine(requestString);
        var httpsRequestHeaders = _httpRequestHandler.ExtractRequestHeaders(requestString);
        var httpRequestBody = _httpRequestHandler.ExtractRequestBody(requestString);

        string responseStatus;
        Dictionary<string, string>? responseHeader;
        string? responseBody;

        string httpResponse;

        bool requireEncoding = false;
        byte[] compressedResponse = [];

        if (targetUrl == "/")
        {
            httpResponse = _httpResponseHandler.GenerateResponse("200", httpVersion, null, null);
        }
        else if (targetUrl.StartsWith("/echo/"))
        {
            (responseStatus, responseHeader, responseBody) = HandleEchoEndpoint(targetUrl);

            var encodingFormat = httpsRequestHeaders.ContainsKey("accept-encoding") ? httpsRequestHeaders["accept-encoding"] : "";
            if (encodingFormat.Contains("gzip"))
            {
                requireEncoding = true;
                compressedResponse = GzipCompressString(responseBody);
                responseHeader.Add("Content-Encoding", "gzip");
            }


            httpResponse = _httpResponseHandler.GenerateResponse(responseStatus, httpVersion, responseHeader, responseBody);
        }
        else if (targetUrl == "/user-agent")
        {
            (responseStatus, responseHeader, responseBody) = HandleUserAgentEndpoint(httpsRequestHeaders);
            httpResponse = _httpResponseHandler.GenerateResponse(responseStatus, httpVersion, responseHeader, responseBody);
        }
        else if (targetUrl.StartsWith("/files/"))
        {
            var fileName = targetUrl.Substring("/files/".Length);
            var cmdArgs = Environment.GetCommandLineArgs();
            var directory = cmdArgs[2];
            var filePath = Path.Join(directory, fileName);

            if (httpMethod == "POST")
            {
                responseStatus = HandlePostFile(httpRequestBody, filePath);
                httpResponse = _httpResponseHandler.GenerateResponse(responseStatus, httpVersion, null, null);
            }
            else
            {
                (responseStatus, responseHeader, responseBody) = HandleGetFileContent(filePath);
                httpResponse = _httpResponseHandler.GenerateResponse(responseStatus, httpVersion, responseHeader, responseBody);
            }
        }
        else
        {
            httpResponse = _httpResponseHandler.GenerateResponse("404", httpVersion, null, null);
        }

        socket.Send(Encoding.ASCII.GetBytes(httpResponse));
        if (requireEncoding)
        {
            socket.Send(compressedResponse);
        }
        _stringBuilder.Clear();
        socket.Close();
    }

    private Tuple<string, Dictionary<string, string>, string> HandleEchoEndpoint(string targetUrl)
    {
        var responseBodyContent = targetUrl.Substring(6);
        var responseHeader = new Dictionary<string, string>() {
                {"Content-Type", "text/plain"},
                {"Content-Length", $"{responseBodyContent.Length}"}
            };
        return new Tuple<string, Dictionary<string, string>, string>("200", responseHeader, responseBodyContent);
    }

    private Tuple<string, Dictionary<string, string>, string> HandleUserAgentEndpoint(Dictionary<string, string> requestHeaders)
    {
        var targetHeader = "User-Agent";
        var responseBodyContent = requestHeaders[targetHeader.ToLower()];
        var responseHeader = new Dictionary<string, string>() {
                {"Content-Type", "text/plain"},
                {"Content-Length", $"{responseBodyContent.Length}"}
            };
        return new Tuple<string, Dictionary<string, string>, string>("200", responseHeader, responseBodyContent); ;
    }

    private Tuple<string, Dictionary<string, string>, string> HandleGetFileContent(string filePath)
    {
        Console.WriteLine("Finding requested file at " + filePath);
        if (File.Exists(filePath))
        {
            Console.WriteLine("Reading requested file content");
            var fileContent = File.ReadAllText(filePath);
            var responseHeader = new Dictionary<string, string>() {
                {"Content-Type", "application/octet-stream"},
                {"Content-Length", $"{fileContent.Length}"}
            };
            return new Tuple<string, Dictionary<string, string>, string>("200", responseHeader, fileContent); ;
        }
        else
        {
            Console.WriteLine("File Not Found");
            return new Tuple<string, Dictionary<string, string>, string>("404", null, null); ;
        }
    }

    private string HandlePostFile(string requestBody, string filePath)
    {
        File.WriteAllText(filePath, requestBody);
        return "201";
    }

    private byte[] GzipCompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using (var memoryStreamOutput = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(memoryStreamOutput, CompressionMode.Compress))
            {
                gzipStream.Write(bytes, 0, bytes.Length);
            }
            return memoryStreamOutput.ToArray();
        }
    }
}

public interface IHttpResponseHandler
{
    public string GenerateResponse(string responseStatus, string httpVersion, Dictionary<string, string>? responseHeader, string? responseBody);
}

public class HttpResponseHandler : IHttpResponseHandler
{
    private readonly Dictionary<string, string> _statusCodeToMessage = new() {
        {"200", "OK"},
        {"201", "Created"},
        {"404", "Not Found"},
    };

    public string GenerateResponse(string responseStatus, string httpVersion, Dictionary<string, string>? responseHeader, string? responseBody)
    {
        var response = $"{httpVersion} {responseStatus} {_statusCodeToMessage[responseStatus]}";
        if (responseHeader != null)
        {
            response += "\r\n";
            foreach (var header in responseHeader.Keys)
            {
                response += $"{header}: {responseHeader[header]}\r\n";
            }
        }
        else
        {
            response += "\r\n\r\n";
        }

        if (responseBody != null)
        {
            response += $"\r\n{responseBody}";
        }

        return response;
    }

}

public interface IHttpRequestHandler
{
    public Tuple<string, string, string> ExtractRequestLine(string request);
    public Dictionary<string, string> ExtractRequestHeaders(string request);
    public string ExtractRequestBody(string request);
}

public class HttpRequestHandler : IHttpRequestHandler
{
    private readonly string headerBodySeparation = "\r\n\r\n";

    public Tuple<string, string, string> ExtractRequestLine(string request)
    {
        var requestDetails = request.Split("\r\n");
        var requestLine = requestDetails[0];
        return new Tuple<string, string, string>(requestLine.Split(" ")[0], requestLine.Split(" ")[1], requestLine.Split(" ")[2]);
    }
    public Dictionary<string, string> ExtractRequestHeaders(string request)
    {
        var separationIndex = request.IndexOf(headerBodySeparation);
        var requestDetails = request.Substring(0, separationIndex).Split("\r\n");

        var headers = new Dictionary<string, string>();
        for (int i = 1; i < requestDetails.Length; i++)
        {
            var (requestHeader, headerValue) = (requestDetails[i].Split(": ")[0].ToLower(), requestDetails[i].Split(": ")[1]);
            headers.Add(requestHeader, headerValue);
        }

        return headers;
    }
    public string ExtractRequestBody(string request)
    {
        var separationIndex = request.IndexOf(headerBodySeparation);
        if (separationIndex != -1)
        {
            var requestBody = request.Substring(separationIndex + headerBodySeparation.Length);
            return requestBody;
        }
        return "";
    }
}