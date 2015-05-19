using System;
using System.Collections.Generic;
using System.Text;
using Windows.Networking;
using Windows.Web.Http;

namespace UniversalWebServer
{
    public sealed class RequestParser
    {
        public Request ParseRequestText(string requestText, HostName localAddress, string localPort)
        {
            var lines = GetMessageLines(requestText);
            var requestLine = ParseRequestLine(lines[0], localAddress, localPort);

            var i = 1;
            var header = GetHeaders(lines, ref i);
            var content = GetBody(lines, i);

            return new Request(requestLine.Method, requestLine.Uri, requestLine.Version, header, content.ToString());
        }

        private static string[] GetMessageLines(string request)
        {
            return request.Split(new[] { "\r\n" }, StringSplitOptions.None);
        }

        private static HeaderLine ParseRequestLine(string requestLine, HostName localAddress, string localPort)
        {
            var requestParts = requestLine.Split(' ');
            var method = new HttpMethod(requestParts[0]);

            var uri = new Uri(requestParts[1], UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
                uri = new Uri(new Uri($"http://{localAddress}:{localPort}"), uri);
            var version = GetHttpVersion(requestParts[2]);

            return new HeaderLine(method, uri, version);
        }

        private static Dictionary<string, string> GetHeaders(IReadOnlyList<string> lines, ref int i)
        {
            var headers = new Dictionary<string, string>();
            for (; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    break;

                var splitLine = line.Split(':');
                var key = splitLine[0];
                var value = line.Substring(key.Length + 1).Trim();
                headers.Add(key, value);
            }
            return headers;
        }

        private static StringBuilder GetBody(IReadOnlyList<string> lines, int i)
        {
            var content = new StringBuilder();
            for (; i < lines.Count; i++)
            {
                content.AppendLine(lines[i]);
            }
            return content;
        }

        private static HttpVersion GetHttpVersion(string httpVersion)
        {
            switch (httpVersion)
            {
                case "HTTP/1.0":
                    return HttpVersion.Http10;
                case "HTTP/1.1":
                    return HttpVersion.Http11;
                case "HTTP/2.0":
                    return HttpVersion.Http20;
                default:
                    return HttpVersion.None;
            }
        }

        private sealed class HeaderLine
        {
            internal HttpMethod Method { get; }
            internal Uri Uri { get; }
            internal HttpVersion Version { get; }

            public HeaderLine(HttpMethod method, Uri uri, HttpVersion version)
            {
                Method = method;
                Uri = uri;
                Version = version;
            }
        }
    }
}