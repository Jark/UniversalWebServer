using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace UniversalWebServer
{
    public sealed class HttpServer : IDisposable
    {
        private const uint BufferSize = 8192;

        private readonly StreamSocketListener listener;
        private readonly RequestParser requestParser;
        private readonly int port;
        private readonly StaticFileHandler staticFileHandler;

        public HttpServer(int serverPort, string staticFilesFolder)
        {
            listener = new StreamSocketListener();
            requestParser = new RequestParser();
            port = serverPort;
            staticFileHandler = new StaticFileHandler(staticFilesFolder);
            listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }        

        public void StartServer()
        {
#pragma warning disable CS4014
            listener.BindServiceNameAsync(port.ToString());
#pragma warning restore CS4014
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            Request request;
            try
            {
                var requestText = await ReadRequest(socket);
                request = requestParser.ParseRequestText(requestText, socket.Information.LocalAddress, socket.Information.LocalPort);
            }
            catch (Exception ex)
            {
                await WriteInternalServerErrorResponse(socket, ex);
                return;
            }

            if (request.Method.Method == HttpMethod.Get.Method)
            {
                HttpResponse response;
                try
                {
                    response = await staticFileHandler.HandleRequest(request);
                }
                catch (Exception ex)
                {
                    await WriteInternalServerErrorResponse(socket, ex);
                    return;
                }
                await WriteResponse(response, socket);
            }
        }

        private static async Task WriteInternalServerErrorResponse(StreamSocket socket, Exception ex)
        {
            var httpResponse = GetInternalServerError(ex);
            await WriteResponse(httpResponse, socket);
        }

        private static HttpResponse GetInternalServerError(Exception exception)
        {
            var errorMessage = "Internal server error occurred.";
            if (Debugger.IsAttached)
                errorMessage += Environment.NewLine + exception;

            var httpResponse = new HttpResponse(HttpStatusCode.InternalServerError, errorMessage);
            return httpResponse;
        }

        private static async Task<string> ReadRequest(StreamSocket socket)
        {
            var httpStreamContent = new HttpStreamContent(socket.InputStream);

            var stringContent = await httpStreamContent.ReadAsInputStreamAsync();
            var request = new StringBuilder();
            using (var input = stringContent)
            {
                var data = new byte[BufferSize];
                var buffer = data.AsBuffer();
                var dataRead = BufferSize;
                while (dataRead == BufferSize)
                {
                    await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                    request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;
                }
            }
            return request.ToString();
        }

        private static async Task WriteResponse(HttpResponse response, StreamSocket socket)
        {
            using (var resp = socket.OutputStream.AsStreamForWrite())
            {
                var bodyArray = Encoding.UTF8.GetBytes(response.Content);
                var stream = new MemoryStream(bodyArray);
                var headerBuilder = new StringBuilder();
                headerBuilder.AppendLine($"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}");
                headerBuilder.AppendLine("Connection: close");
                headerBuilder.AppendLine($"Content-Length: {stream.Length}");

                foreach (var header1 in response.Headers)
                {
                    headerBuilder.AppendLine($"{header1.Key}: {header1.Value}");
                }
                headerBuilder.AppendLine();

                var headerArray = Encoding.UTF8.GetBytes(headerBuilder.ToString());
                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                await stream.CopyToAsync(resp);
                await resp.FlushAsync();
            }
        }

        public void Dispose()
        {
            listener.Dispose();
        }
    }
}