using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Web.Http;

namespace UniversalWebServer
{
    public class StaticFileHandler
    {
        private readonly string basePath;
        private readonly ContentTypeMapper contentTypeMapper;

        public StaticFileHandler(string basePath)
        {
            this.basePath = GetAbsoluteBasePathUri(basePath);
            contentTypeMapper = new ContentTypeMapper();
        }

        private static string GetAbsoluteBasePathUri(string relativeOrAbsoluteBasePath)
        {
            var basePathUri = new Uri(relativeOrAbsoluteBasePath, UriKind.RelativeOrAbsolute);
            if (basePathUri.IsAbsoluteUri)
                return relativeOrAbsoluteBasePath;
            else
                return Path.Combine(Package.Current.InstalledLocation.Path, relativeOrAbsoluteBasePath);
        }

        public async Task<HttpResponse> HandleRequest(Request headerLine)
        {
            var filePath = GetFilePath(headerLine);

            StorageFile item;
            try
            {
                item = await StorageFile.GetFileFromPathAsync(filePath);
            }
            catch (FileNotFoundException)
            {
                return new HttpResponse(HttpStatusCode.NotFound, $"File: {headerLine.Uri.LocalPath} not found");
            }            

            return await GetHttpResponse(item);
        }

        private async Task<HttpResponse> GetHttpResponse(StorageFile item)
        {
            var inputStream = await item.OpenSequentialReadAsync();
            using (var streamReader = new StreamReader(inputStream.AsStreamForRead()))
            {
                var fileContents = await streamReader.ReadToEndAsync();
                var contentType = contentTypeMapper.GetContentTypeForExtension(Path.GetExtension(item.Name));
                var headers = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(contentType))
                    headers.Add("Content-Type", contentType);

                return new HttpResponse(HttpStatusCode.Ok, headers, fileContents);
            }
        }

        private string GetFilePath(Request headerLine)
        {
            var localPath = ParseLocalPath(headerLine);
            var sanitizedLocalPath = localPath.Replace('/', '\\');
            var filePath = Path.Combine(basePath, sanitizedLocalPath);
            return filePath;
        }

        private static string ParseLocalPath(Request headerLine)
        {
            var localPath = headerLine.Uri.LocalPath;
            if (localPath.EndsWith("/"))
                localPath += "index.html";

            if (localPath.StartsWith("/"))
                localPath = localPath.Substring(1);
            return localPath;
        }
    }
}